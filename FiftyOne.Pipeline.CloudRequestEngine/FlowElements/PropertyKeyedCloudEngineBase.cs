﻿/* *********************************************************************
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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// A specialised type of <see cref="CloudAspectEngineBase{T}"/>
    /// that has functionality to support returning a list of matching 
    /// <see cref="IAspectData"/> profiles rather than a single item.
    /// </summary>
    /// <typeparam name="TData">
    /// The type of <see cref="IAspectData"/> returned by this engine.
    /// </typeparam>
    /// <typeparam name="TProfile">
    /// The type of the items in the list returned by this engine.
    /// </typeparam>
    public abstract class PropertyKeyedCloudEngineBase<TData, TProfile> : CloudAspectEngineBase<TData>
        where TData : IMultiProfileData<TProfile>
        where TProfile : IAspectData
    {
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            // This engine needs no evidence. 
            // It works from the cloud request data.
            new EvidenceKeyFilterWhitelist(new List<string>());
        
        private static JsonConverter[] JSON_CONVERTERS = new JsonConverter[]
        {
            new CloudJsonConverter()
        };

        public PropertyKeyedCloudEngineBase(
            ILogger<PropertyKeyedCloudEngineBase<TData, TProfile>> logger,
            Func<IPipeline, FlowElementBase<TData, IAspectPropertyMetaData>, TData> deviceDataFactory)
            : base(logger, deviceDataFactory)
        {
        }

        protected override void ProcessEngine(IFlowData data, TData aspectData)
        {
            var requestData = data.GetFromElement(RequestEngine.Instance);
            var json = requestData?.JsonResponse;

            if (string.IsNullOrEmpty(json))
            {
                throw new PipelineConfigurationException(
                    $"Json response from cloud request engine is null. " +
                    $"This is probably because there is not a " +
                    $"'CloudRequestEngine' before the '{GetType().Name}' " +
                    $"in the Pipeline. This engine will be unable " +
                    $"to produce results until this is corrected.");
            }
            else
            {
                // Extract data from json to the aspectData instance.
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                // Access the data relating to this engine.
                var propertyKeyed = dictionary[ElementDataKey] as JObject;
                // Access the 'Profiles' property
                foreach(var entry in propertyKeyed["profiles"])
                {
                    // Iterate through the devices, parsing each one and
                    // adding it to the result.
                    var propertyValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.ToString(),
                        new JsonSerializerSettings()
                        {
                            Converters = JSON_CONVERTERS,
                        });
                    var device = CreateProfileData();
                    // Get the meta-data for properties on device instances.
                    var propertyMetaData = Properties
                        .Single(p => p.Name == "Profiles").ItemProperties;

                    var deviceData = CreateAPVDictionary(
                        propertyValues, 
                        propertyMetaData);

                    device.PopulateFromDictionary(deviceData);
                    //device.SetNoValueReasons(nullReasons);
                    aspectData.AddProfile(device);
                }
            }
        }

        protected abstract TProfile CreateProfileData();

    }
}