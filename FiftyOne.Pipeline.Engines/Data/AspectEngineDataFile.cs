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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// This class contains all configuration and run-time state information 
    /// that is associated with a particular data file used by an aspect 
    /// engine.
    /// </summary>
    public class AspectEngineDataFile : IAspectEngineDataFile
    {
        /// <summary>
        /// The data update service that this engine is 
        /// registered with (if any)
        /// </summary>
        protected IDataUpdateService _dataUpdateService;

        public string Identifier { get; set;  }

        public IOnPremiseAspectEngine Engine { get; set; }

        /// <summary>
        /// The complete file path to the location of the data file.
        /// This value will be null if the file has been supplied from 
        /// a byte[] in memory. 
        /// </summary>
        public virtual string DataFilePath { get { return Configuration.DataFilePath; } }

        private string _tempDataDirPath = null;
        /// <summary>
        /// The path to use when working with temporary files associated
        /// with this data file.
        /// </summary>
        public string TempDataDirPath
        {
            get
            {
                if (_tempDataDirPath == null &&
                    Engine != null &&
                    Engine.TempDataDirPath != null)
                {
                    // By default, use the temp path from the engine.
                    _tempDataDirPath = Engine.TempDataDirPath;
                }
                return _tempDataDirPath;
            }
            set { _tempDataDirPath = value; }
        }

        /// <summary>
        /// True if automatic updates are enabled, false otherwise.
        /// </summary>
        public virtual bool AutomaticUpdatesEnabled { get { return Configuration.AutomaticUpdatesEnabled; } }

        private string _tempDataFilePath = null;
        /// <summary>
        /// The complete file path to the location of the temporary
        /// copy of the data file that is currently being used by the 
        /// <see cref="IAspectEngine"/>.
        /// Engines often make a temporary copy of the data file in order
        /// to allow the original to be updated.
        /// This value will be null if the file is loaded entirely into memory.
        /// </summary>
        public virtual string TempDataFilePath
        {
            get
            {
                if (_tempDataFilePath == null &&
                    Engine != null &&
                    Engine.TempDataDirPath != null &&
                    DataFilePath != null)
                {
                    // By default, use the temp path from the engine
                    // combined with the name of the data file.
                    _tempDataFilePath = Path.Combine(Engine.TempDataDirPath,
                        Path.GetFileName(DataFilePath));
                }
                return _tempDataFilePath;
            }
            set
            {
                _tempDataFilePath = value;
            }
        }


        public DateTime UpdateAvailableTime { get; set; }

        public DateTime DataPublishedDateTime { get; set; }

        public IDataFileConfiguration Configuration { get; set; }


        // properties used internally by DataUpdateService.

        internal Timer Timer { get; set; }
        internal FileSystemWatcher FileWatcher { get; set; }
        internal object UpdateSyncLock { get; } = new object();
        internal DateTime LastUpdateFileCreateTime { get; set; }

        /// <summary>
        /// Get the data update URL complete with any query string 
        /// parameters that are needed to retrieve the data.
        /// By default, no query string parameters are added to the URL.
        /// </summary>
        public virtual string FormattedUrl
        {
            get
            {
                if (Configuration.UrlFormatter == null)
                {
                    return Configuration.DataUpdateUrl;
                }
                else
                {
                    return Configuration.UrlFormatter.GetFormattedDataUpdateUrl(this);
                }
            }
        }

        /// <summary>
        /// Returns true if this file has been registered with the 
        /// data update service. False if not.
        /// </summary>
        public bool IsRegistered
        {
            get
            {
                return _dataUpdateService != null;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public AspectEngineDataFile()
        {
        }

        /// <summary>
        /// Set the data update service that this data file is 
        /// registered with.
        /// </summary>
        /// <param name="dataUpdateService">
        /// The data update service.
        /// </param>
        public void SetDataUpdateService(IDataUpdateService dataUpdateService)
        {
            _dataUpdateService = dataUpdateService;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_dataUpdateService != null)
                    {
                        _dataUpdateService.UnRegisterDataFile(this);
                        _dataUpdateService = null;
                    }
                    if (FileWatcher != null)
                    {
                        FileWatcher.Dispose();
                    }
                    if (Timer != null)
                    {
                        Timer.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}