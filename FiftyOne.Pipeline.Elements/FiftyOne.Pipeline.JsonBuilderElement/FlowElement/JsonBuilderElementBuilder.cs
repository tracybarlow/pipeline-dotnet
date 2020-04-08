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

using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using FiftyOne.Pipeline.Core.Data;
using Newtonsoft.Json;
using System.Linq;

namespace FiftyOne.Pipeline.JsonBuilder.FlowElement
{
    /// <summary>
    /// 
    /// </summary>
    public class JsonBuilderElementBuilder
    {
        private ILoggerFactory _loggerFactory;
        private IEnumerable<JsonConverter> _jsonConverters = Enumerable.Empty<JsonConverter>();

        public JsonBuilderElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

            public JsonBuilderElementBuilder(ILoggerFactory loggerFactory, IEnumerable<JsonConverter> jsonConverters)
        {
            _loggerFactory = loggerFactory;
            _jsonConverters = jsonConverters;
        }

        public JsonBuilderElement Build()
        {
            return new JsonBuilderElement(_loggerFactory.CreateLogger<JsonBuilderElement>(), _jsonConverters, CreateData);
        }

        private IJsonBuilderElementData CreateData(
            IPipeline pipeline,
            FlowElementBase<IJsonBuilderElementData, IElementPropertyMetaData> jsonBuilderElement)
        {
            return new JsonBuilderElementData(
                _loggerFactory.CreateLogger<JsonBuilderElementData>(),
                pipeline);
        }

    }
}
