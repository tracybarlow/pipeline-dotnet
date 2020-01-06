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
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Pipeline.Engines.Tests.Services
{
    [TestClass]
    public class DataUpdateServiceTests
    {
        private TestLogger<DataUpdateService> _logger;
        private Mock<IFileWrapper> _fileWrapper;
        private Mock<Func<TimerCallback, object, TimeSpan, Timer>> _timerFactory;

        private Mock<MockHttpMessageHandler> _httpHandler;
        private HttpClient _httpClient;

        private int _ignoreWranings = 0;
        private int _ignoreErrors = 0;

        private DataUpdateService _dataUpdate;

        /// <summary>
        /// Initialise the test instance.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            // Create mocks
            _logger = new TestLogger<DataUpdateService>();
            _fileWrapper = new Mock<IFileWrapper>();
            _timerFactory = new Mock<Func<TimerCallback, object, TimeSpan, Timer>>();

            // Create the HttpClient using the mock handler
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            _httpClient = new HttpClient(_httpHandler.Object);

            // Configure the mock handler to return an 'OK' status code.
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("<empty />", Encoding.UTF8, "application/xml"),
                });            

            // Create the data update service
            _dataUpdate = new DataUpdateService(
                _logger,
                _httpClient,
                _fileWrapper.Object,
                _timerFactory.Object);

        }

        /// <summary>
        /// Cleanup and do any common asserts 
        /// (e.g. checking no errors were logged)
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            _logger.AssertMaxErrors(_ignoreErrors);
            _logger.AssertMaxWarnings(_ignoreWranings);
        }

        /// <summary>
        /// Check that an argument null exception is thrown if the 
        /// configuration parameter is null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DataUpdateService_Register_Null()
        {
            _dataUpdate.RegisterDataFile(null);
        }

        /// <summary>
        /// Check that a timer will be created with the default timing values
        /// if the AutomaticUpdatesEnabled property is set.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_AutoUpdateDefaults()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

            // Act
            _dataUpdate.RegisterDataFile(file);

            // Assert
            // Make sure the time interval is as expected
            _timerFactory.Verify(f => f(
                It.IsAny<TimerCallback>(),
                It.Is<object>(o => (o as AspectEngineDataFile) == file),
                It.Is<TimeSpan>(t => t.TotalSeconds >= Constants.DATA_UPDATE_POLLING_DEFAULT &&
                    t.TotalSeconds <= Constants.DATA_UPDATE_POLLING_DEFAULT +
                    Constants.DATA_UPDATE_RANDOMISATION_DEFAULT)));
        }

        /// <summary>
        /// Check that a timer will be created with the expected interval
        /// when the engine specifies an 'UpdateAvailableTime'
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_AutoUpdateExpectedTime()
        {
            // Arrange
            TimeSpan testTime = TimeSpan.FromDays(1);
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config, 
                UpdateAvailableTime = DateTime.UtcNow.Add(testTime)
            };

            // Act
            _dataUpdate.RegisterDataFile(file);

            // Assert
            // Make sure the time interval is as expected
            var minExpectedSeconds = testTime.TotalSeconds - 2;
            var maxExpectedSeconds = testTime.TotalSeconds + config.MaxRandomisationSeconds + 2;
            _timerFactory.Verify(f => f(
                It.IsAny<TimerCallback>(),
                It.Is<object>(o => (o as AspectEngineDataFile) == file),
                It.Is<TimeSpan>(t => t.TotalSeconds >= minExpectedSeconds &&
                    t.TotalSeconds <= maxExpectedSeconds)));
        }

        /// <summary>
        /// Check that a timer will be created with the expected timing 
        /// values if the interval property is set.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_AutoUpdateConfiguredInterval()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                PollingIntervalSeconds = 0
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config,
            };

            // Act
            _dataUpdate.RegisterDataFile(file);

            // Assert
            // Make sure the time interval is as expected
            _timerFactory.Verify(f => f(
                It.IsAny<TimerCallback>(),
                It.Is<object>(o => (o as AspectEngineDataFile) == file),
                It.Is<TimeSpan>(t => t.TotalSeconds >= 0 &&
                    t.TotalSeconds <= Constants.DATA_UPDATE_RANDOMISATION_DEFAULT)));
        }

        /// <summary>
        /// Check that a timer will be created with the expected timing 
        /// values if the randomisation property is set to zero.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_AutoUpdateNoRandomisation()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                MaxRandomisationSeconds = 0
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config,
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

            // Act
            _dataUpdate.RegisterDataFile(file);

            // Assert
            // Make sure the time interval is as expected
            _timerFactory.Verify(f => f(
                It.IsAny<TimerCallback>(),
                It.Is<object>(o => (o as AspectEngineDataFile) == file),
                It.Is<TimeSpan>(t => t.TotalSeconds == Constants.DATA_UPDATE_POLLING_DEFAULT)));
        }

        /// <summary>
        /// Check that, if the same data file is registered twice, it will not
        /// result in two update timers being started.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_AutoUpdateSameEngineTwice()
        {
            // Arrange
            // Configure the timer factory to return a timer that does 
            // nothing. This is needed so that the timer can be stored against
            // the configuration, otherwise the second call to register will
            // not know that the engine is already registered.
            _timerFactory.Setup(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()))
                .Returns(new Timer((o) => { }));

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                MaxRandomisationSeconds = 0,
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config,
            };

            // Act
            _dataUpdate.RegisterDataFile(file);
            _dataUpdate.RegisterDataFile(file);

            // Assert
            // Check that the timer factory was only called once.
            _timerFactory.Verify(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()), 
                Times.Once());
        }

        /// <summary>
        /// Check that enabling the FileSystemWatcher will create a watcher
        /// and assign it to the configuration object as expected.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DataUpdateService_Register_FileSystemWatcher(bool autoUpdateEnabled)
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempData = Path.GetTempFileName();
            try
            {
                var config = new DataFileConfiguration()
                {
                    FileSystemWatcherEnabled = true,
                    DataFilePath = tempData,
                    AutomaticUpdatesEnabled = autoUpdateEnabled
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };

                // Act
                _dataUpdate.RegisterDataFile(file);

                // Assert
                Assert.IsNotNull(file.FileWatcher);
            }
            finally
            {
                // Make sure we tidy up the temp file.
                if (File.Exists(tempData)) { File.Delete(tempData); }
            }
        }

        /// <summary>
        /// Check that the FileSystemWatcher works as expected.
        /// This is not great as we need to make use of the file system. 
        /// However, in this instance, the inclusion is justified by the 
        /// interplay between the FileSystemWatcher and the operating 
        /// system. 
        /// Running the test with the FileSystemWatcher mocked out 
        /// would be fairly meaningless as it would not include the
        /// unintuitive behavior of the watcher. e.g. raising multiple events
        /// for a single copy operation.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_UpdateFromWatcher()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempData = Path.GetTempFileName();
            try
            {
                var config = new DataFileConfiguration()
                {
                    FileSystemWatcherEnabled = true,
                    DataFilePath = tempData
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
                _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.IsAny<string>()))
                    .Returns((string tempFile) => { return File.GetCreationTimeUtc(tempFile); });
                // Configure a ManualResetEvent to be set when processing
                // is complete.
                ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
                _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
                {
                    completeFlag.Set();
                };

                // Act
                _dataUpdate.RegisterDataFile(file);
                string temp2 = Path.GetTempFileName();
                File.WriteAllText(temp2, "Testing");
                File.Copy(temp2, tempData, true);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Once());
            }
            finally
            {
                // Make sure we tidy up the temp file.
                if (File.Exists(tempData)) { File.Delete(tempData); }
            }
        }

        /// <summary>
        /// This test is the same as the one above but creates a 'new' file 
        /// of around 1.6Gb in size. 
        /// This ensures that the watcher event handler logic still works 
        /// when the copy operation takes a long time.
        /// It is commented out by default as, even under good conditions,
        /// it takes 20+ seconds to run.
        /// </summary>
        //[TestMethod]
        //public void DataUpdateService_Update_FileSystemWatcher_LargeFile()
        //{
        //    // Arrange
        //    Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
        //    string tempData = Path.GetTempFileName();
        //    try
        //    {
        //        engine.Setup(e => e.DataFilePath).Returns(tempData);
        //        EngineDataUpdateConfiguration config = new EngineDataUpdateConfiguration()
        //        {
        //            FileSystemWatcherEnabled = true,
        //            Engine = engine.Object
        //        };
        //        _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.IsAny<string>()))
        //            .Returns((string file) => { return File.GetCreationTimeUtc(file); });
        //        // Configure a ManualResetEvent to be set when processing
        //        // is complete.
        //        ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
        //        _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
        //        {
        //            completeFlag.Set();
        //        };

        //        // Act
        //        _dataUpdate.RegisterEngine(config);
        //        string temp2 = Path.GetTempFileName();
        //        using (var writer = File.CreateText(temp2))
        //        {
        //            for (int i = 0; i < 40000000; i++)
        //            {
        //                writer.Write("Thequickbrownfoxjumpsoverthelazydog");
        //            }
        //        }
        //        File.Copy(temp2, tempData, true);
        //        // Wait until processing is complete.
        //        completeFlag.Wait(1000);

        //        // Assert
        //        Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
        //            "event was never fired");
        //        engine.Verify(e => e.RefreshData(), Times.Once());
        //    }
        //    finally
        //    {
        //        // Make sure we tidy up the temp file.
        //        if (File.Exists(tempData)) { File.Delete(tempData); }
        //    }
        //}

        /// <summary>
        /// Automatic check for updates. 
        /// File system check only.
        /// File is not new so nothing happens.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_UpdateFromFile_FileNotUpdated()
        {
            // Arrange
            ConfigureTimerImmediateCallback();
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            ConfigureFileNoUpdate(engine, file);
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            // Act
            _dataUpdate.RegisterDataFile(file);
            // Wait until processing is complete.
            completeFlag.Wait(1000);

            // Assert
            Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                "event was never fired");
            // Make sure that refresh is not called on the engine.
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
            // Verify that timer factory was only called once to 
            // set up the initial timer.
            _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
        }        

        /// <summary>
        /// Automatic check for updates. 
        /// File system check only.
        /// File is new, check engine refreshed and timer recreated.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_UpdateFromFile_FileUpdated()
        {
            // Arrange
            // Configure the timer to execute immediately. 
            // When subsequent timers are created, they will not execute.
            ConfigureTimerImmediateCallbackOnce();
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            ConfigureFileUpdate(engine, file);
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            // Act
            _dataUpdate.RegisterDataFile(file);
            // Wait until processing is complete.
            completeFlag.Wait(1000);

            // Assert
            Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                "event was never fired");
            // Make sure that refresh is not called on the engine.
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Once());
            // Verify that timer factory was called twice. Once for the 
            // initial timer and again after the update was complete.
            _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Exactly(2));
        }

        /// <summary>
        /// Configure the engine to have a temp path but no temp file.
        /// Therefore file system check will not occur but URL check will.
        /// Configure HTTP handler to return 'NotModified' 
        /// (i.e. no update is available)
        /// </summary>
        [TestMethod]
        public void DataUpdateService_UpdateFromUrl_NoUpdate()
        {
            // Arrange
            ConfigureTimerImmediateCallback();
            ConfigureHttpNoUpdateAvailable();
            // Getting no data from the URL will cause an error to be logged
            // so we need to ignore this
            _ignoreWranings = 1;

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                DataUpdateUrl = @"http://www.test.com",
                DataFilePath = "test.dat",
                FileSystemWatcherEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            // Act
            _dataUpdate.RegisterDataFile(file);
            // Wait until processing is complete.
            completeFlag.Wait(1000);

            // Assert
            Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                "event was never fired");
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
            // Make sure engine was not refreshed
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
        }

        /// <summary>
        /// Configure the engine to have a temp path but no temp file.
        /// Therefore file system check will not occur but URL check will.
        /// Configure HTTP handler to return an update.
        /// </summary>
        /// <remarks>
        /// This test writes to and reads from the file system temp path.
        /// This is required in this case in order to fully test the 
        /// interaction between the various temp files that are required.
        /// </remarks>
        [TestMethod]
        public void DataUpdateService_UpdateFromUrl_UpdateAvailable()
        {
            // Arrange
            // For this test we want to use the real FileWrapper to allow
            // the test to perform file system read/write operations.
            IFileWrapper fileWrapper = ConfigureRealFileSystem();
            // Configure the timer to execute immediately. 
            // When subsequent timers are created, they will not execute.
            ConfigureTimerImmediateCallbackOnce();

            // Configure the mock HTTP handler to return the test data file
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(() => {
                    // MD5 for data was calculated and verified using 
                    // https://md5file.com/calculator
                    // http://onlinemd5.com/
                    string md5 = "08527dcbdd437e7fa6c084423d06dba6";

                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StreamContent(File.OpenRead(@"Resources\file.gz")),
                    };

                    response.Content.Headers.Add("Content-MD5", md5);
                    return response;
                });
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            string dataFile = Path.GetTempFileName();
            try
            {
                // Configure the engine to return the relevant paths.
                engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
                var config = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = true,
                    DataUpdateUrl = @"http://www.test.com",
                    DataFilePath = dataFile,
                    VerifyMd5 = true,
                    DecompressContent = true,
                    FileSystemWatcherEnabled = false,
                    VerifyModifiedSince = false
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

                // Act
                _dataUpdate.RegisterDataFile(file);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
                // Make sure engine was refreshed
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Once());
                // The timer factory should have been called twice, once for
                // the initial registration and again after the update was
                // applied to the engine.
                _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(), 
                    It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Exactly(2));
            }
            finally
            {
                if (File.Exists(dataFile)) { File.Delete(dataFile); }
            }
        }

        /// <summary>
        /// Configure the engine to have a temp path but no temp file.
        /// Therefore file system check will not occur but URL check will.
        /// Configure HTTP handler to return an update but with the wrong MD5 
        /// hash.
        /// </summary>
        /// <remarks>
        /// This test writes to and reads from the file system temp path.
        /// This is required in this case in order to fully test the 
        /// interaction between the various temp files that are required.
        /// </remarks>
        [TestMethod]
        public void DataUpdateService_UpdateFromUrl_MD5Invalid()
        {
            // Arrange
            // For this test we want to use the real FileWrapper to allow
            // the test to perform file system read/write operations.
            IFileWrapper fileWrapper = ConfigureRealFileSystem();
            // Configure the timer to execute immediately. 
            // When subsequent timers are created, they will not execute.
            ConfigureTimerImmediateCallbackOnce();

            // Configure the mock HTTP handler to return the test data file
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(() => {
                    string md5 = "08527dcbdd437e7fa6c084423d06dba0";

                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StreamContent(File.OpenRead(@"Resources\file.gz")),
                    };

                    response.Content.Headers.Add("Content-MD5", md5);
                    return response;
                });
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            // The invalid MD5 will cause an error to be logged so we want
            // to ignore this
            _ignoreErrors = 1;

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            string dataFile = Path.GetTempFileName();
            try
            {
                // Configure the engine to return the relevant paths.
                engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
                var config = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = true,
                    DataUpdateUrl = @"http://www.test.com",
                    DataFilePath = dataFile,
                    VerifyMd5 = true,
                    FileSystemWatcherEnabled = false
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

                // Act
                _dataUpdate.RegisterDataFile(file);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
                // Make sure engine was not refreshed
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
                // The timer factory should only be called once when the engine
                // is registered. As the update fails, the timer will be reset
                // rather than creating a new one with new data.
                _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                    It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
            }
            finally
            {
                if (File.Exists(dataFile)) { File.Delete(dataFile); }
            }
        }



        /// <summary>
        /// Configure the engine to have a temp path but no temp file.
        /// Therefore file system check will not occur but URL check will.
        /// Configure HTTP handler to return an exception and ensure it 
        /// is handled correctly.
        /// </summary>
        /// <remarks>
        /// This test writes to and reads from the file system temp path.
        /// This is required in this case in order to fully test the 
        /// interaction between the various temp files that are required.
        /// </remarks>
        [TestMethod]
        public void DataUpdateService_UpdateFromUrl_HttpException()
        {
            // Arrange
            // For this test we want to use the real FileWrapper to allow
            // the test to perform file system read/write operations.
            IFileWrapper fileWrapper = ConfigureRealFileSystem();
            // Configure the timer to execute immediately. 
            // When subsequent timers are created, they will not execute.
            ConfigureTimerImmediateCallbackOnce();

            // Configure the mock HTTP handler to throw an exception
            var errorText = "There was an error!";
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Throws(new Exception(errorText));
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            string dataFile = Path.GetTempFileName();
            try
            {
                // Configure the engine to return the relevant paths.
                engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
                var config = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = true,
                    DataUpdateUrl = @"http://www.test.com",
                    DataFilePath = dataFile,
                    VerifyMd5 = true,
                    DecompressContent = true,
                    FileSystemWatcherEnabled = false,
                    VerifyModifiedSince = false,
                    UpdateOnStartup = false
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

                // Act
                _dataUpdate.RegisterDataFile(file);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
                // Make sure engine was not refreshed
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
                // The timer factory should have been called once for
                // the initial registration.
                // After the update fails, the same timer will be 
                // reconfigured to try again later.
                _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                    It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
                // We expect one error to be logged so make sure it's
                // ignored in cleanup and verify its presence.
                _ignoreErrors = 1;
                Assert.AreEqual(1, _logger.ErrorsLogged.Count);
            }
            finally
            {
                if (File.Exists(dataFile)) { File.Delete(dataFile); }
            }
        }

        /// <summary>
        /// Configure the engine use both file system watcher and automated 
        /// updates.
        /// Also configure it to check both files and URL.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_UpdateAllEnabled_NoUpdates()
        {
            // Arrange
            IFileWrapper fileWrapper = ConfigureRealFileSystem();
            ConfigureTimerImmediateCallbackOnce();
            ConfigureHttpNoUpdateAvailable();
            // Getting no data from the URL will cause an error to be logged
            // so we need to ignore this
            _ignoreWranings = 1;

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            string dataFile = Path.GetTempFileName();
            try
            {
                // Configure the engine to return the relevant paths.
                engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
                var config = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = true,
                    FileSystemWatcherEnabled = true,
                    DataUpdateUrl = @"http://www.test.com",
                    DataFilePath = dataFile
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
                // Configure a ManualResetEvent to be set when processing
                // is complete.
                ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
                _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
                {
                    completeFlag.Set();
                };

                // Act
                _dataUpdate.RegisterDataFile(file);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
                // Make sure engine was not refreshed
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
                // Verify that timer factory was only called once to 
                // set up the initial timer.
                _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                    It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
            }
            finally
            {
                if (File.Exists(dataFile)) { File.Delete(dataFile); }
            }
        }

        /// <summary>
        /// Manual check for updates. 
        /// File system check only.
        /// File is not new so nothing happens.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_CheckForUpdate_FileNotUpdated()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            ConfigureFileNoUpdate(engine, file);

            // Act
            _dataUpdate.CheckForUpdate(engine.Object);

            // Assert
            // Make sure that refresh is not called on the engine.
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
            // Verify that timer factory has not been called to 
            // set up a new timer.
            _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Never());
        }

        /// <summary>
        /// Manual check for updates. 
        /// File system check only.
        /// File is new so refresh is called.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_CheckForUpdate_FileUpdated()
        {
            // Arrange
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            ConfigureFileUpdate(engine, file);

            // Act
            _dataUpdate.CheckForUpdate(engine.Object);

            // Assert
            // Make sure that refresh is called on the engine.
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Once());
            // Verify that timer factory has not been called to 
            // set up a new timer.
            _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Never());
        }

        /// <summary>
        /// Manual update check, URL returns no update available.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_CheckForUpdate_UrlNoUpdate()
        {
            // Arrange
            ConfigureHttpNoUpdateAvailable();
            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
            var config = new DataFileConfiguration()
            {
                DataUpdateUrl = @"http://www.test.com",
                FileSystemWatcherEnabled = false,
                AutomaticUpdatesEnabled = false
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };

            // Act
            _dataUpdate.CheckForUpdate(engine.Object);

            // Assert
            // Make sure that refresh is not called on the engine.
            engine.Verify(e => e.RefreshData(config.Identifier), Times.Never());
        }

        /// <summary>
        /// Configure the engine to update on startup and have no existing 
        /// data file on disk.
        /// The update service should download the latest file and save it.
        /// </summary>
        /// <remarks>
        /// This test writes to and reads from the file system temp path.
        /// This is required in this case in order to fully test the 
        /// interaction between the various temp files that are required.
        /// </remarks>
        /// <remarks>
        /// The test should result in the same behavior regardless of
        /// whether auto updates and enabled or disabled.
        /// </remarks>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DataUpdateService_Register_UpdateOnStartup_NoFile(bool autoUpdateEnabled)
        {
            // Arrange
            // For this test we want to use the real FileWrapper to allow
            // the test to perform file system read/write operations.
            IFileWrapper fileWrapper = ConfigureRealFileSystem();
            // Configure the timer to execute as normal
            ConfigureTimerAccurateCallback();

            // Configure the mock HTTP handler to return the test data file
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(() => {
                    // MD5 for data was calculated and verified using 
                    // https://md5file.com/calculator
                    // http://onlinemd5.com/
                    string md5 = "08527dcbdd437e7fa6c084423d06dba6";

                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StreamContent(File.OpenRead(@"Resources\file.gz")),
                    };

                    response.Content.Headers.Add("Content-MD5", md5);
                    return response;
                });
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();
            string dataFile = Path.GetTempFileName();
            // We want to make sure there is no existing data file.
            // The update service should create it.
            File.Delete(dataFile);
            try
            {
                // Configure the engine to return the relevant paths.
                engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
                var config = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = autoUpdateEnabled,
                    DataUpdateUrl = @"http://www.test.com",
                    DataFilePath = dataFile,
                    VerifyMd5 = true,
                    DecompressContent = true,
                    FileSystemWatcherEnabled = false,
                    VerifyModifiedSince = false,
                    UpdateOnStartup = true
                };
                var file = new AspectEngineDataFile()
                {
                    Engine = engine.Object,
                    Configuration = config
                };
                engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

                // Check that files do not exist before the test starts
                Assert.IsFalse(File.Exists(file.DataFilePath), "Data file already exists before test starts");
                Assert.IsFalse(File.Exists(file.TempDataFilePath), "Temp data file already exists before test starts");

                // Act
                _dataUpdate.RegisterDataFile(file);
                // Wait until processing is complete.
                completeFlag.Wait(1000);

                // Assert
                Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                    "event was never fired");
                _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
                // Make sure engine was refreshed
                engine.Verify(e => e.RefreshData(config.Identifier), Times.Once());
                if (autoUpdateEnabled)
                {
                    // If auto update is enabled then the timer factory 
                    // should have been called once to set up the next
                    // update check.
                    _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                        It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
                }
                // Check that files exist at both the original and temporary
                // locations.
                Assert.IsTrue(File.Exists(file.DataFilePath), "Data file does not exist after test");
                Assert.IsTrue(File.Exists(file.TempDataFilePath), "Temp data file does not exist after test");
            }
            finally
            {
                if (File.Exists(dataFile)) { File.Delete(dataFile); }
            }
        }

        /// <summary>
        /// Configure the engine to update on startup and operate 
        /// entirely in memory
        /// The update service should download the latest file and
        /// use it to refresh the engine.
        /// </summary>
        [TestMethod]
        public void DataUpdateService_Register_UpdateOnStartup_InMemory()
        {
            // Arrange
            // Configure the test to have no file system. This will 
            // verify that everything takes place in memory.
            ConfigureNoFileSystem();
            // Configure the timer to execute as normal
            ConfigureTimerAccurateCallback();

            // Configure the mock HTTP handler to return the test data file
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(() => {
                    // MD5 for data was calculated and verified using 
                    // https://md5file.com/calculator
                    // http://onlinemd5.com/
                    string md5 = "08527dcbdd437e7fa6c084423d06dba6";

                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StreamContent(File.OpenRead(@"Resources\file.gz")),
                    };

                    response.Content.Headers.Add("Content-MD5", md5);
                    return response;
                });
            // Configure a ManualResetEvent to be set when processing
            // is complete.
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                completeFlag.Set();
            };            

            Mock<IOnPremiseAspectEngine> engine = new Mock<IOnPremiseAspectEngine>();
            string tempPath = Path.GetTempPath();

            // Configure the engine to return the relevant paths.
            engine.Setup(e => e.TempDataDirPath).Returns(tempPath);
            var config = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                DataUpdateUrl = @"http://www.test.com",
                VerifyMd5 = true,
                DecompressContent = true,
                FileSystemWatcherEnabled = false,
                VerifyModifiedSince = false,
                UpdateOnStartup = true,
                Data = null
            };
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = config
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

            // Add a callback to check that the data passed to the engine
            // is the uncompressed file contents.
            string dataPassedToEngine = "";
            engine.Setup(e => e.RefreshData(config.Identifier, It.IsAny<byte[]>()))
                .Callback((string identifier, byte[] data) =>
            {
                using (StreamReader reader = new StreamReader(new MemoryStream(data)))
                {
                    dataPassedToEngine = reader.ReadToEnd();
                }
            });

            // Act
            _dataUpdate.RegisterDataFile(file);
            // Wait until processing is complete.
            completeFlag.Wait(1000);

            // Assert
            Assert.IsTrue(completeFlag.IsSet, "The 'CheckForUpdateComplete' " +
                "event was never fired");
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once());
            // Make sure engine was refreshed with the expected data.
            engine.Verify(e => e.RefreshData(config.Identifier, It.IsAny<byte[]>()), Times.Once());
            Assert.AreEqual($"TESTING{Environment.NewLine}", dataPassedToEngine);
            // The timer factory should have been called once.
            _timerFactory.Verify(f => f(It.IsAny<TimerCallback>(),
                It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once());
        }

        #region Private methods
        private void ConfigureNoFileSystem()
        {
            // Configure the file wrapper to be 'strict'. This means that 
            // any calls to methods that have not been set up will throw
            // an exception.
            // We use this behavior to verify there is no interaction 
            // with the file system.
            _fileWrapper = new Mock<IFileWrapper>(MockBehavior.Strict);
            _dataUpdate = new DataUpdateService(
                _logger,
                _httpClient,
                _fileWrapper.Object,
                _timerFactory.Object);
        }
        private IFileWrapper ConfigureRealFileSystem()
        {
            // For this test we want to use the real FileWrapper to allow
            // the test to perform file system read/write operations.
            IFileWrapper fileWrapper = new FileWrapper();
            _dataUpdate = new DataUpdateService(
                _logger,
                _httpClient,
                fileWrapper,
                _timerFactory.Object);
            return fileWrapper;
        }

        private void ConfigureTimerImmediateCallback()
        {
            // Configure the timer factory to return a timer that will
            // execute the callback immediately
            _timerFactory.Setup(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()))
                .Returns((TimerCallback callback, object state, TimeSpan interval) =>
                {
                    return new Timer(callback, state, 0, Timeout.Infinite);
                });
        }

        private void ConfigureTimerAccurateCallback()
        {
            // Configure the timer factory to return a timer that will
            // execute the callback immediately
            _timerFactory.Setup(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()))
                .Returns((TimerCallback callback, object state, TimeSpan interval) =>
                {
                    return new Timer(callback, state, 
                        (int)interval.TotalMilliseconds, Timeout.Infinite);
                });
        }

        private void ConfigureTimerImmediateCallbackOnce()
        {
            // Configure the timer factory to return a timer that will
            // execute the callback immediately
            int counter = 0;
            _timerFactory.Setup(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()))
                .Returns((TimerCallback callback, object state, TimeSpan interval) =>
                {
                    // Only return the timer of the first call.
                    // For subsequent calls (when the engine is registered 
                    // again following a successful update) just return
                    // null to prevent repeated updates.
                    if (counter == 0)
                    {
                        counter++;
                        return new Timer(callback, state, 0, Timeout.Infinite);
                    }
                    else
                    {
                        return null;
                    }
                });
        }

        private void ConfigureFileNoUpdate(Mock<IOnPremiseAspectEngine> engine, 
            AspectEngineDataFile config)
        {
            string dataFile = @"C:\test\tempFile.dat";
            string tempFile = @"C:\test\dataFile.dat";
            engine.Setup(e => e.TempDataDirPath).Returns(@"C:\test");
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(config);
            config.TempDataFilePath = tempFile;
            config.Configuration.DataFilePath = dataFile;

            // Configure file wrapper to return specified creation dates 
            // for data files.
            _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.Is<string>(s => s == dataFile)))
                .Returns(new DateTime(2018, 05, 10, 12, 0, 0));
            _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.Is<string>(s => s == tempFile)))
                .Returns(new DateTime(2018, 05, 10, 12, 0, 0));
        }

        private void ConfigureFileUpdate(Mock<IOnPremiseAspectEngine> engine,
            AspectEngineDataFile config)
        {
            string dataFile = @"C:\test\tempFile.dat";
            string tempFile = @"C:\test\dataFile.dat";
            engine.Setup(e => e.TempDataDirPath).Returns(@"C:\test");
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(config);
            config.TempDataFilePath = tempFile;
            config.Configuration.DataFilePath = dataFile;

            // Configure file wrapper to return specified creation dates 
            // for data files.
            _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.Is<string>(s => s == dataFile)))
                .Returns(new DateTime(2018, 05, 11, 12, 0, 0));
            _fileWrapper.Setup(w => w.GetCreationTimeUtc(It.Is<string>(s => s == tempFile)))
                .Returns(new DateTime(2018, 05, 10, 12, 0, 0));
        }

        private void ConfigureHttpNoUpdateAvailable()
        {
            // Configure the mock HTTP handler to return an 'NotModified' 
            // status code.
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                })
                .Returns(new HttpResponseMessage(System.Net.HttpStatusCode.NotModified)
                {
                    Content = new StringContent("<empty />", Encoding.UTF8, "application/xml"),
                });
        }
        #endregion
    }
}