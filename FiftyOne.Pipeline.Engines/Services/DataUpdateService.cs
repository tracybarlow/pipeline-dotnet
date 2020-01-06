/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2019 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 * 
 * If using the Work as, or as part of, a network application, by 
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading, 
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Services
{
	/// <summary>
	/// Service that manages updates to data files that are used by
	/// <see cref="IAspectEngine"/> instances.
	/// </summary>
	public class DataUpdateService : IDataUpdateService
	{
		#region Fields
		private ILogger<DataUpdateService> _logger;

		/// <summary>
		/// The HttpClient to use when checking for updates.
		/// </summary>
		private HttpClient _httpClient;

		// System wrappers
		private IFileWrapper _fileWrapper;

		// random number generator
		private Random _rnd = new Random();

		// All registered configurations
		private List<AspectEngineDataFile> _configurations;

		// The factory function used to create Timer objects.
		private Func<TimerCallback, object, TimeSpan, Timer> _timerFactory;

		/// <summary>
		/// The event handler fired when a call to CheckForUpdate is completed.
		/// </summary>
		public event EventHandler<DataUpdateCompleteArgs> CheckForUpdateComplete;
		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="logger">
		/// The logger to use
		/// </param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> to use when requesting an update
		/// from a URL.
		/// Note that only one HttpClient instance should be used throughout 
		/// the application, as described in the documentation: 
		/// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
		/// </param>
		public DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient) : this(logger, httpClient, null, null)
		{ }

		/// <summary>
		/// Internal constructor. Should only be called directly 
		/// by unit tests 
		/// </summary>
		/// <param name="logger">
		/// The logger to use
		/// </param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> to use when requesting an update
		/// from a URL.
		/// Note that only one HttpClient instance should be used throughout 
		/// the application, as described in the documentation: 
		/// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
		/// </param>
		/// <param name="fileWrapper">
		/// A wrapper for file system operations on individual files.
		/// </param>
		/// <param name="timerFactory">
		/// A factory method for creating <see cref="Timer"/> instances.
		/// Parameters are: callback method, state, time until callback 
		/// triggered.
		/// </param>
		internal DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient,
			IFileWrapper fileWrapper,
			Func<TimerCallback, object, TimeSpan, Timer> timerFactory)
		{
			_logger = logger;
			_httpClient = httpClient;
			_configurations = new List<AspectEngineDataFile>();

			if(fileWrapper == null)
			{
				_fileWrapper = new FileWrapper();
			}
			else
			{
				_fileWrapper = fileWrapper;
			}
			if (timerFactory == null)
			{
				_timerFactory = TimerFactory;
			}
			else
			{
				_timerFactory = timerFactory;
			}
		}

		#region Public methods
		/// <summary>
		/// Register an data file for automatic updates.
		/// </summary>
		/// <param name="dataFile">
		/// The details of the data file to register.
		/// </param>
		public void RegisterDataFile(AspectEngineDataFile dataFile)
		{
			if (dataFile == null)
			{
				throw new ArgumentNullException("dataFile");
			}

			bool alreadyRegistered = dataFile.IsRegistered;
			dataFile.SetDataUpdateService(this);

			if (dataFile != null)
			{
				// If the data file is configured to refresh the data
				// file on startup then download an update immediately.
				// We also want to do this synchronously so that execution
				// will block until the engine is ready.
				if (dataFile.Configuration.UpdateOnStartup &&
					alreadyRegistered == false)
				{
					var result = CheckForUpdate(dataFile, true);
                    if(result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
                    {
                        throw new DataUpdateException("Update on startup failed. See log for details.");
                    }
				}
				else
				{
					// Only create an automatic update timer if auto updates are 
					// enabled for this engine and there is not already an associated 
					// timer.
					if (dataFile.AutomaticUpdatesEnabled &&
						dataFile.Timer == null)
					{
						TimeSpan timeToUpdate = GetInterval(dataFile.Configuration);
                        if (dataFile.UpdateAvailableTime > DateTime.UtcNow)
                        {
                            timeToUpdate = dataFile.UpdateAvailableTime
                                .Subtract(DateTime.UtcNow);
                            timeToUpdate = ApplyIntervalRandomisation(
                                timeToUpdate, dataFile.Configuration);
                        }
						// Create a timer that will go off when the engine expects
						// updated data to be available.
						var timer = _timerFactory(
							CheckForUpdate,
							dataFile,
							timeToUpdate);
						dataFile.Timer = timer;
					}

					// If file system watcher is enabled then set it up.
					if (dataFile.Configuration.FileSystemWatcherEnabled &&
						dataFile.FileWatcher == null &&
						string.IsNullOrEmpty(dataFile.DataFilePath) == false)
					{
						FileSystemWatcher watcher = new FileSystemWatcher(
							Path.GetDirectoryName(dataFile.DataFilePath),
							Path.GetFileName(dataFile.DataFilePath));
						watcher.NotifyFilter = NotifyFilters.LastWrite;
						watcher.Changed += DataFileUpdated;
						watcher.EnableRaisingEvents = true;
						dataFile.FileWatcher = watcher;
					}

					lock (_configurations)
					{
						// Add the configuration to the list of configurations.
						if (_configurations.Contains(dataFile) == false)
						{
							_configurations.Add(dataFile);
						}
					}
				}
			}
		}

		/// <summary>
		/// Unregister a data file.
		/// </summary>
		/// <param name="dataFile">
		/// The data file to unregister
		/// </param>
		public void UnRegisterDataFile(AspectEngineDataFile dataFile)
		{
			lock (_configurations)
			{
				var configs = _configurations.Where(c => c == dataFile);
				foreach (var config in configs)
				{
					_configurations.Remove(config);
				}
			}
		}

        /// <summary>
        /// Check if there are updates for the specified engine.
        /// </summary>
        /// <param name="engine">
        /// The engine to check for
        /// </param>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to check for.
        /// If the engine has only one data file then this parameter is ignored.
        /// </param>
        /// <returns>
        /// The result of the update check.
        /// </returns>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
        public AutoUpdateStatus CheckForUpdate(
			IOnPremiseAspectEngine engine, 
			string dataFileIdentifier = null)
		{
			AutoUpdateStatus result;
			var dataFile = engine.GetDataFileMetaData(dataFileIdentifier);
			if(dataFile != null)
			{ 
				dataFile.Configuration.AutomaticUpdatesEnabled = false;
				result = CheckForUpdate(dataFile, true);
			}
			else
			{
				result = AutoUpdateStatus.AUTO_UPDATE_NO_CONFIGURATION;
			}
			return result;
		}

		/// <summary>
		/// Update the specified data file from a byte[] held in memory.
		/// </summary>
		/// <param name="dataFile">
		/// The data file to update.
		/// </param>
		/// <param name="data">
		/// The data file to update with.
		/// </param>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
		public AutoUpdateStatus UpdateFromMemory(AspectEngineDataFile dataFile, byte[] data)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			if (string.IsNullOrEmpty(dataFile.DataFilePath) == false)
			{
				// The engine has an associated data file so update it first.
				try
				{
					_fileWrapper.WriteAllBytes(dataFile.DataFilePath, data);
				}
				catch (Exception ex)
				{
                    throw new DataUpdateException($"An error occurred when writing to " +
						$"'{dataFile.DataFilePath}'. The engine will be updated " +
						$"to use the new data but the file on disk will still " +
						$"contain old data.", ex);
				}
			}

			if (dataFile.Engine != null)
			{
				try
				{
					// Refresh the engine using the new data.
					dataFile.Engine.RefreshData(dataFile.Identifier, data);
					result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
				}
				catch (Exception ex)
				{
					result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
                    throw new DataUpdateException($"An error occurred when applying a " +
						$"data update to engine '{dataFile.Engine.GetType().Name}'.", ex);
				}
			}
			else
			{
				result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
			}

			return result;
		}
		#endregion

		#region Private methods
		/// <summary>
		/// Default method used to create Timer instances when a timer factory
		/// is not provided in the constructor.
		/// </summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <param name="dueTime"></param>
		/// <returns></returns>
		private Timer TimerFactory(TimerCallback callback, object state, TimeSpan dueTime)
		{
			return new Timer(callback, state, dueTime, TimeSpan.FromMilliseconds(-1));
		}

		/// <summary>
		/// Called when the 'CheckForUpdate' method is finished.
		/// </summary>
		/// <param name="args"></param>
		private void OnUpdateComplete(DataUpdateCompleteArgs args)
		{
			CheckForUpdateComplete?.Invoke(this, args);
		}

		/// <summary>
		/// Event handler that is called when the data file is updated.
		/// </summary>
		/// <remarks>
		/// The <see cref="FileSystemWatcher"/> will raise multiple events 
		/// in many cases, for example, if a file is copied over an existing 
		/// file then 3 'changed' events will be raised.
		/// This handler deals with the extra events by using synchronisation 
		/// with a double-check lock to ensure that the update will only be 
		/// done once.
		/// </remarks>
		/// <param name="sender">
		/// The <see cref="FileSystemWatcher"/> sender of the event.
		/// </param>
		/// <param name="e">
		/// The event arguments.
		/// </param>
		private void DataFileUpdated(object sender, FileSystemEventArgs e)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			// Get the associated update configuration
			AspectEngineDataFile dataFile = null;
			try
			{
				dataFile = _configurations
					.Where(c => c.FileWatcher != null &&
						c.FileWatcher == sender)
					.SingleOrDefault();
			}
			catch (InvalidOperationException)
			{
				dataFile = null;
			}

			if (dataFile != null)
			{
				// Get the creation time of the new data file
				DateTime createTime = _fileWrapper.GetCreationTimeUtc(e.FullPath);
				// Use a lock with a double check on file creation time to make
				// sure we only run the update once even if multiple events fire
				// for a single file.
				if (dataFile.LastUpdateFileCreateTime < createTime)
				{
					lock (dataFile.UpdateSyncLock)
					{
						if (dataFile.LastUpdateFileCreateTime < createTime)
						{
							dataFile.LastUpdateFileCreateTime = createTime;

							// Make sure we can actually open the file for reading
							// before notifying the engine, otherwise the copy 
							// may still be in progress.
							bool fileLockable = false;
							while (fileLockable == false)
							{
								try
								{
									using (_fileWrapper.OpenRead(e.FullPath)) { }
									fileLockable = true;
								}
								catch { }

								// Complete the update
								status = UpdatedFileAvailable(dataFile);
							}
						}
					}
				}
			}

			OnUpdateComplete(new DataUpdateCompleteArgs()
			{
				DataFile = dataFile,
				UpdateApplied = status == AutoUpdateStatus.AUTO_UPDATE_SUCCESS
			});
		}

		/// <summary>
		/// Private method called by update timers when an update is believed
		/// to be available.
		/// </summary>
		/// <param name="state">
		/// The <see cref="AspectEngineDataFile"/>
		/// </param>
		private void CheckForUpdate(object state)
        {
            // This method is called from a background thread so
            // we need to make sure any exceptions that occur
            // are handled here.
            try
            {
                CheckForUpdate(state, false);
            }
            catch (DataUpdateException ex)
            {
                _logger.LogError("Error during check for data update", ex);
            }
            catch (Exception ex)
            {
                AspectEngineDataFile dataFile = state as AspectEngineDataFile;
                var engine = dataFile?.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name;
                _logger.LogError($"An unhandled error occurred while " +
                    $"checking for automatic updates for engine '{engine}'", ex);
            }
        }

        /// <summary>
        /// Private method that performs the following actions:
        /// 1. Checks for an update to the data file on disk.
        /// 2. Checks for an update using the update URL.
        /// 3. Refresh engine with new data if available.
        /// 4. Schedule the next update check if needed.
        /// </summary>
        /// <param name="state">
        /// The <see cref="AspectEngineDataFile"/>
        /// </param>
        private AutoUpdateStatus CheckForUpdate(object state, bool manualUpdate)
        {
            AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            AspectEngineDataFile dataFile = state as AspectEngineDataFile;
            bool newDataAvailable = false;

            try
            {
                if (dataFile != null)
                {
                    // Only check the file system if the file system watcher
                    // is not enabled and the engine is using a temporary file.
                    if (dataFile.Configuration.FileSystemWatcherEnabled == false &&
                        string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
                        string.IsNullOrEmpty(dataFile.TempDataFilePath) == false)
                    {
                        var fileCreateTime = _fileWrapper.GetCreationTimeUtc(
                            dataFile.DataFilePath);
                        var tempFileCreateTime = _fileWrapper.GetCreationTimeUtc(
                            dataFile.TempDataFilePath);

                        // If the data file is newer than the temp file currently
                        // being used by the engine the we need to tell the engine
                        // to refresh itself.
                        if (fileCreateTime > tempFileCreateTime)
                        {
                            newDataAvailable = true;
                        }
                    }

                    if (newDataAvailable == false &&
                        string.IsNullOrEmpty(dataFile.Configuration.DataUpdateUrl) == false)
                    {
                        result = CheckForUpdateFromUrl(dataFile);
                        newDataAvailable =
                            result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS ||
                            result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
                    }

                    if (newDataAvailable &&
                        result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
                    {
                        // Data update was available but engine has not 
                        // yet been refreshed.
                        result = UpdatedFileAvailable(dataFile);
                    }

                    if (newDataAvailable == false)
                    {
                        // No update available.
                        // If this was a manual call to update then do nothing.
                        // If it was triggered by the timer expiring then modify
                        // the timer to check again after the configured interval.
                        // This will repeat until the update is acquired.
                        if (manualUpdate == false &&
                            dataFile != null &&
                            dataFile.Timer != null)
                        {
                            dataFile.Timer.Change(
                                GetInterval(dataFile.Configuration),
                                TimeSpan.FromMilliseconds(-1));
                        }
                    }

                    if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS ||
                        result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
                    {
                        // Re-register the engine with the data update service 
                        // so it knows when next set of data should be available.
                        RegisterDataFile(dataFile);
                        result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
                    }
                }
            }
            finally
            {
                OnUpdateComplete(new DataUpdateCompleteArgs()
                {
                    DataFile = dataFile,
                    UpdateApplied = result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS
                });
            }

            return result;
        }

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			if (string.IsNullOrEmpty(dataFile.DataFilePath))
			{
				// There is no data file path specified so perform the 
				// update entirely in memory.
				using (var compressedStream = new MemoryStream())
				using (var uncompressedStream = new MemoryStream())
				{
					result = CheckForUpdateFromUrl(dataFile,
						compressedStream,
						uncompressedStream);

					result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
                    // Note that the uncompressed buffer will be longer 
                    // than it needs to be so we can't just copy the whole 
                    // thing.
                    // The 'Length' property will get the true number
                    // of elements we need to take from the buffer.
                    var buffer = new byte[uncompressedStream.Length];
                    Array.Copy(uncompressedStream.GetBuffer(),
                        buffer, uncompressedStream.Length);

                    if (dataFile.Engine != null)
					{
						try
						{
							// Tell the engine to refresh itself with
							// the new data.
							dataFile.Engine.RefreshData(dataFile.Identifier, buffer);
						}
						catch (Exception ex)
						{
							result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
                            throw new DataUpdateException($"An error occurred when applying a " +
									$"data update to engine '{dataFile.Engine.GetType().Name}'.", ex);
						}
					}
					else
					{
                        // No associated engine at the moment so just set 
                        // the value of the data array.
                        dataFile.Configuration.Data = buffer;
                    }
				}
			}
			else
			{
				// There is a data file path so use the temporary
				// file location to store data while we work on downloading
				// and decompressing it.
				string compressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");
				string uncompressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");

				try
				{
					using (var compressedStream = _fileWrapper.Create(compressedTempFile))
					using (var uncompressedStream = _fileWrapper.Create(uncompressedTempFile))
					{
						result = CheckForUpdateFromUrl(dataFile,
							compressedStream,
							uncompressedStream);
					}

					if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						// If this engine has a data file watcher then we need to
						// disable it while the update is occurring.
						if (dataFile != null &&
							dataFile.FileWatcher != null)
						{
							dataFile.FileWatcher.EnableRaisingEvents = false;
						}

						try
						{
							// Copy the uncompressed file to the engine's 
							// data file location
							_fileWrapper.Copy(uncompressedTempFile,
								dataFile.DataFilePath, true);
						}
						catch (Exception ex)
						{
							result = AutoUpdateStatus.AUTO_UPDATE_NEW_FILE_CANT_RENAME;
                            throw new DataUpdateException($"An error occurred when copying a " +
								$"data file to replace the existing one at " +
								$"'{dataFile.DataFilePath}'.", ex);
						}
						finally
						{
							// Make sure to enable the file watcher again 
							// if needed.
							if (dataFile != null &&
								dataFile.FileWatcher != null)
							{
								dataFile.FileWatcher.EnableRaisingEvents = true;
							}
						}
					}
				}
				finally
				{
					// Make sure the temp files are cleaned up
					if (_fileWrapper.Exists(compressedTempFile))
					{
						_fileWrapper.Delete(compressedTempFile);
					}
					if (_fileWrapper.Exists(uncompressedTempFile))
					{
						_fileWrapper.Delete(uncompressedTempFile);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <param name="compressedDataStream">
		/// A <see cref="Stream"/> to write the data to as it is downloaded.
		/// </param>
		/// <param name="uncompressedDataStream">
		/// A <see cref="Stream"/> to write the uncompressed data to once
		/// download is complete.
		/// If the <see cref="DataFileConfiguration.DecompressContent"/>
		/// flag is set to false then this can be null.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(
			AspectEngineDataFile dataFile,
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

            string expectedMd5Hash = null;
			// Check if there is an update and download it if there is                   
			result = DownloadFile(dataFile, compressedDataStream, out expectedMd5Hash);
			// Check data integrity
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS &&
				dataFile.Configuration.VerifyMd5)
			{
				result = VerifyMd5(dataFile, expectedMd5Hash, compressedDataStream);
			}
			// decompress the file
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
			{
				if (dataFile.Configuration.DecompressContent)
				{
					result = Decompress(
						compressedDataStream, uncompressedDataStream);
				}
				else
				{
					// If decompression is not needed then just replace
					// the uncompressed stream object with the 'compressed' one
					compressedDataStream.Seek(0, 0);
					compressedDataStream.CopyTo(uncompressedDataStream);
				}
			}

			return result;
		}

		/// <summary>
		/// Called when a data update is available and the file at 
		/// engine.DataFilePath contains this new data.
		/// 1. Refresh the engine.
		/// 2. Dispose of the existing update timer if there is one.
		/// 3. Re-register the engine with the update service.
		/// </summary>
		/// <param name="dataFile"></param>
		/// <returns></returns>
		private AutoUpdateStatus UpdatedFileAvailable(
			AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			Exception exception = null;
			int tries = 0;

			if (dataFile.Engine != null)
			{
				// Try to update the file multiple times to ensure the file is not 
				// locked.
				while (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS && tries < 10)
				{
					try
					{
						dataFile.Engine.RefreshData(dataFile.Identifier);
						result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
					}
					catch (Exception ex)
					{
						exception = ex;
						result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
						Thread.Sleep(200);
					}
					tries++;
				}

				if (tries == 10)
				{
                    throw new DataUpdateException($"An error occurred when applying a " +
							$"data update to engine '{dataFile.Engine.GetType().Name}' " +
							$"after {tries} tries.", exception);
				}
			}

			if (dataFile != null &&
				dataFile.Timer != null)
			{
				// Dispose of the old timer object
				dataFile.Timer.Dispose();
				dataFile.Timer = null;
			}

			return result;
		}

		/// <summary>
		/// Get the most recent data file available from the configured 
		/// update URL.
		/// If the data currently used by the engine is the newest available
		/// then nothing will be downloaded.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <param name="tempStream">
		/// The stream to write the data to.
		/// </param>
		/// <param name="expectedMd5Hash">
		/// Used to output the md5 hash for the file from the 
		/// 'Content-MD5' header.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// </returns>
		private AutoUpdateStatus DownloadFile(
			AspectEngineDataFile dataFile,
			Stream tempStream,
			out string expectedMd5Hash)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			expectedMd5Hash = null;
			
			string url = dataFile.FormattedUrl;

			HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, url);

            // Get data file published date from meta-data.
            // If it's not been set for some reason then use the 
            // creation date of the file instead.
            DateTime publishDate = dataFile.DataPublishedDateTime;
            if(dataFile.DataPublishedDateTime <= DateTime.MinValue &&
                string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
                _fileWrapper.Exists(dataFile.DataFilePath))
            {
                publishDate = _fileWrapper.GetCreationTimeUtc(dataFile.DataFilePath);
            }

            // Set last-modified header to ensure that a file will only
            // be downloaded if it is newer than the data we already have.
            if (dataFile.Configuration.VerifyModifiedSince == true)
			{
				message.Headers.Add(
					 "If-Modified-Since",
					 publishDate.ToString("R"));
			}

            HttpResponseMessage response = null;
			try
			{
				// Send the message
				response = _httpClient.SendAsync(message).Result;
			}
			catch (Exception ex)
			{
				result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
                throw new DataUpdateException($"Error accessing data update service at " +
					$"'{url}' for engine " +
					$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'", ex);
			}

			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
			{
				if (response == null)
				{
					result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
                    throw new DataUpdateException($"No response from data update service at " +
						$"'{url}' for engine " +
						$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");
				}
				else
				{
					if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation(
                            $"Downloaded new data from '{url}' for engine " +
                            $"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");

                        // If the response is successful then save the content to a 
                        // temporary file
                        using (var dataStream = response.Content.ReadAsStreamAsync().Result)
						{
							dataStream.CopyTo(tempStream);
						}
						if (dataFile.Configuration.VerifyMd5)
						{
							IEnumerable<string> values;
							if (response.Content.Headers.TryGetValues("Content-MD5", out values))
							{
								expectedMd5Hash = values.SingleOrDefault();
							}
							else
							{
								_logger.LogWarning(
									$"No MD5 hash included in data update response for " +
									$"'{url}'. Unable to verify data integrity");
							}
						}
					}
					else
					{
						switch (response.StatusCode)
						{
							// Note: needed because TooManyRequests is not available 
							// in some versions of the HttpStatusCode enum.
							case ((HttpStatusCode)429):
								result = AutoUpdateStatus.
									AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS;
                                throw new DataUpdateException($"Too many requests to " +
									$"'{url}' for engine " +
									$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");
							case HttpStatusCode.NotModified:
								result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
                                _logger.LogInformation($"No data newer than " +
                                    $"{publishDate} found at '{url}' for engine " +
									$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");
                                break;
							case HttpStatusCode.Forbidden:
								result = AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN;
                                throw new DataUpdateException($"Access denied to data update service at " +
									$"'{url}' for engine " +
									$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");
							default:
								result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
                                throw new DataUpdateException($"HTTP status code '{response.StatusCode}' " +
									$"from data update service at " +
									$"'{url}' for engine " +
									$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}'");
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Reads a source GZip stream and writes the uncompressed data to 
		/// destination stream.
		/// </summary>
		/// <param name="compressedDataStream">
		/// Stream containing GZipped data to be uncompressed
		/// </param>
		/// <param name="uncompressedDataStream">
		/// Stream to write the uncompressed data to.
		/// </param>
		/// <returns>The current state of the update process.</returns>
		private AutoUpdateStatus Decompress(
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			compressedDataStream.Position = 0;
			using (var fis = new GZipStream(
				compressedDataStream, CompressionMode.Decompress, true))
			{
				fis.CopyTo(uncompressedDataStream);
			}
			return status;
		}

		/// <summary>
		/// Check that the specified file matches the specified md5 hash
		/// </summary>
		/// <param name="dataFile">
		/// The meta-data relating to the data file to verify
		/// </param>
		/// <param name="serverHash">
		/// The expected md5 hash value
		/// </param>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>
		/// True if the hashes match, false if not.
		/// </returns>
		private AutoUpdateStatus VerifyMd5(
			AspectEngineDataFile dataFile, 
			string serverHash, Stream compressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			string downloadHash = GetMd5(compressedDataStream);
			if (serverHash == null ||
				serverHash.Equals(downloadHash) == false)
			{
				status = AutoUpdateStatus.AUTO_UPDATE_ERR_MD5_VALIDATION_FAILED;
                throw new DataUpdateException(
					$"Integrity check failed. MD5 hash in HTTP response " +
					$"'{serverHash}' for " +
					$"'{(dataFile.Engine == null ? "Unknown" : dataFile.Engine.GetType().Name)}' " +
					$"data update does not match calculated hash for the " +
					$"downloaded file '{downloadHash}'.");
			}
			return status;
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(Stream compressedDataStream)
		{
			using (MD5 md5Hash = MD5.Create())
			{
				compressedDataStream.Position = 0;
				return GetMd5(md5Hash, compressedDataStream);
			}
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="stream">calculate MD5 of this stream</param>
		/// <param name="md5Hash">instance of MD5 hash calculator</param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(MD5 md5Hash, Stream stream)
		{
			// Convert the input string to a byte array and compute the hash.
			byte[] data = md5Hash.ComputeHash(stream);

			// Create a new stringbuilder to collect the bytes
			// and create a string.
			StringBuilder sb = new StringBuilder();

			// Loop through each byte of the hashed data 
			// and format each one as a hexadecimal string.
			for (int i = 0; i < data.Length; i++)
			{
				sb.Append(data[i].ToString("x2"));
			}

			// Return the hexadecimal string.
			return sb.ToString();
		}
		
		/// <summary>
		/// Get an interval in the form of a <see cref="TimeSpan"/> based
		/// on the configuration object provided.
		/// If configured, a random additional number of seconds is added 
		/// between 0 and the specified maximum.
		/// </summary>
		/// <param name="config">
		/// The configuration to use to generate the interval
		/// </param>
		/// <returns>
		/// A TimeSpan representing time interval.
		/// </returns>
		private TimeSpan GetInterval(IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.PollingIntervalSeconds > 0)
			{
				seconds = config.PollingIntervalSeconds;
			}
			return ApplyIntervalRandomisation(
				TimeSpan.FromSeconds(seconds), config);
		}

		/// <summary>
		/// Add a random amount of time to the specified interval
		/// </summary>
		/// <param name="interval">
		/// The <see cref="TimeSpan"/> to add a random amount of time to.
		/// </param>
		/// <param name="config">
		/// The <see cref="IDataFileConfiguration"/> object that 
		/// specifies the maximum number of seconds to add.
		/// </param>
		private TimeSpan ApplyIntervalRandomisation(TimeSpan interval, 
			IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.MaxRandomisationSeconds > 0)
			{
				seconds = (int)(_rnd.NextDouble() *
					config.MaxRandomisationSeconds);
			}
			return interval.Add(TimeSpan.FromSeconds(seconds));
		}
		#endregion
	}
}