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

using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    public interface IOnPremiseAspectEngine : IAspectEngine
    {
        /// <summary>
        /// Details of the data files used by this engine.
        /// </summary>
        IReadOnlyList<IAspectEngineDataFile> DataFiles { get; }

        /// <summary>
        /// Causes the engine to reload data from the file at 
        /// <see cref="IAspectEngineDataFile.DataFilePath"/> for the
        /// data file matching the given identifier.
        /// Where the engine is built from a byte[], the overload with the 
        /// byte[] parameter should be called instead.
        /// This method is thread-safe so parallel calls to 'Process' will 
        /// resolve as normal.
        /// </summary>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to update. Must match the 
        /// value in <see cref="IAspectEngineDataFile.Identifier"/>.
        /// If the engine only has a single data file, this parameter 
        /// is ignored.
        /// If null is passed then all data files should be refreshed.
        /// </param>
        void RefreshData(string dataFileIdentifier);

        /// <summary>
        /// Causes the engine to reload data from the specified byte[].
        /// Where the engine is built from a data file on disk, this will
        /// also update the data file with the new data.
        /// This method is thread-safe so parallel calls to 'Process' will 
        /// resolve as normal.
        /// </summary>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to update. Must match the 
        /// value in <see cref="IAspectEngineDataFile.Identifier"/>.
        /// If the engine only has a single data file, this parameter 
        /// is ignored.
        /// If null is passed then all data files should be refreshed.
        /// </param>
        /// <param name="data">
        /// An in-memory representation of the new data file contents.
        /// </param>
        void RefreshData(string dataFileIdentifier, byte[] data);

        /// <summary>
        /// The complete file path to the directory that is used by the
        /// engine to store temporary copies of any data files that it uses.
        /// </summary>
        string TempDataDirPath { get; }

        /// <summary>
        /// Get the details of a specific data file used by this engine.
        /// </summary>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to get meta data for.
        /// This parameter is ignored if the engine only has one data file.
        /// </param>
        /// <returns>
        /// The meta data associated with the specified data file.
        /// Returns null if the engine has no associated data files.
        /// </returns>
        IAspectEngineDataFile GetDataFileMetaData(string dataFileIdentifier = null);

        /// <summary>
        /// Add the specified data file to the engine.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to add.
        /// </param>
        void AddDataFile(IAspectEngineDataFile dataFile);
    }
}