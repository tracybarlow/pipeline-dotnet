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
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    public class MockFlowData
    {
        /// <summary>
        /// Helper method used to create an IFlowData from the given evidence.
        /// </summary>
        /// <param name="evidenceData">
        /// A Dictionary containing the evidence that needs to be in the 
        /// <see cref="IFlowData"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Mock{IFlowData}"/> instance that will return the  
        /// supplied evidence data when GetEvidence() is called.
        /// </returns>
        public static Mock<IFlowData> CreateFromEvidence(
            Dictionary<string, object> evidenceData,
            bool dataKeyFromAllEvidence)
        {
            Mock<IEvidence> evidence = new Mock<IEvidence>();
            evidence.Setup(e => e.AsDictionary()).Returns(evidenceData);
            Mock<IFlowData> data = new Mock<IFlowData>();
            data.Setup(d => d.GetEvidence()).Returns(evidence.Object);

            if (dataKeyFromAllEvidence)
            {
                var keyBuilder = new DataKeyBuilder();
                foreach (var entry in evidenceData)
                {
                    keyBuilder.Add(0, entry.Key, entry.Value);
                }
                DataKey key = keyBuilder.Build();
                data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>()))
                    .Returns(key);
            }

            return data;
        }

    }
}
