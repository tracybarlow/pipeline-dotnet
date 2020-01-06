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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    public abstract class FiftyOneOnPremiseAspectEngineBuilderBase<TBuilder, TEngine> :
        SingleFileAspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : FiftyOneOnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IFiftyOneAspectEngine
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataUpdateService"></param>
        public FiftyOneOnPremiseAspectEngineBuilderBase(
            IDataUpdateService dataUpdateService) : 
            base(dataUpdateService)
        {
            SetDataUpdateUrl("https://distributor.51degrees.com/api/v2/download");
            SetDataUpdateUrlFormatter(new FiftyOneUrlFormatter());
        }

        protected override AspectEngineDataFile NewAspectEngineDataFile()
        {
            return new FiftyOneDataFile()
            {
                DataUpdateDownloadType = DataFileType
            };
        }

        protected abstract string DataFileType { get; }
    }
}