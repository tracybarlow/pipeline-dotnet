/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2020 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
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

using FiftyOne.Pipeline.Core.Data;
using SimpleClientSideElement.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using FiftyOne.Pipeline.Core.Attributes;

namespace Examples.ClientSideEvidence.Shared
{
    //! [class]
    [AlternateName("SimpleClientSideElement")]
    public class SimpleClientSideElementBuilder
    {
        private ILoggerFactory _loggerFactory;

        public SimpleClientSideElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public SimpleClientSideElement Build()
        {
            return new SimpleClientSideElement(
                _loggerFactory.CreateLogger<SimpleClientSideElement>(),
                CreateData);
        }

        // This is the element data factory, and is called in the Process method of the element.
        private IStarSignData CreateData(IPipeline pipeline, 
            FlowElementBase<IStarSignData,IElementPropertyMetaData> element)
        {
            return new StarSignData(_loggerFactory.CreateLogger<StarSignData>(), pipeline);
        }
    }
    //! [class]
}
