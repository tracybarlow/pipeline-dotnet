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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class CloudRequestEngineTests
    {
        HttpClient _httpClient;
        private static ILoggerFactory _loggerFactory = new LoggerFactory();
        private Mock<HttpMessageHandler> _handlerMock;

        private Uri expectedUri = new Uri("https://cloud.51degrees.com/api/v4/json");

        private string _jsonResponse = "{'device':{'value':'1'}}";
        private string _evidenceKeysResponse = "['query.User-Agent']";
        private string _accessiblePropertiesResponse = 
            "{'Products': {'device': {'DataTier': 'tier','Properties': [{'Name': 'value','Type': 'String','Category': 'Device'}]}}}";


        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// and returns the response in the ElementData
        /// </summary>
        [TestMethod]
        public void Process()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            ConfigureMockedClient(r => 
                r.Content.ReadAsStringAsync().Result.Contains($"resource={resourceKey}") // content contains resource key
                && r.Content.ReadAsStringAsync().Result.Contains($"User-Agent={userAgent}") // content contains licenseKey
            );

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();

                var result = data.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);

                dynamic obj = JValue.Parse(result);
                Assert.AreEqual(1, (int)obj.device.value);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// and returns the response in the ElementData
        /// </summary>
        [TestMethod]
        public void Process_LicenseKey()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string licenseKey = "ABCDEFG";
            ConfigureMockedClient(r =>
                  r.Content.ReadAsStringAsync().Result.Contains($"resource={resourceKey}") // content contains resource key
                  && r.Content.ReadAsStringAsync().Result.Contains($"license={licenseKey}") // content contains licenseKey
                  && r.Content.ReadAsStringAsync().Result.Contains($"User-Agent={userAgent}") // content contains user agent
            );

#pragma warning disable CS0618 // Type or member is obsolete
            // SetLicensekey is obsolete but we still want to test that
            // it works as intended.
            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetLicenseKey(licenseKey)
#pragma warning restore CS0618 // Type or member is obsolete
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();

                var result = data.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);

                dynamic obj = JValue.Parse(result);
                Assert.AreEqual(1, (int)obj.device.value);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that the CloudRequestEngine can correctly parse a 
        /// response from the accessible properties endpoint that contains
        /// meta-data for sub-properties.
        /// </summary>
        [TestMethod]
        public void SubProperties()
        {
            _accessiblePropertiesResponse = @"
{
    ""Products"": {
        ""device"": {
            ""DataTier"": ""CloudV4TAC"",
            ""Properties"": [
                {
                    ""Name"": ""IsMobile"",
                    ""Type"": ""Boolean"",
                    ""Category"": ""Device""
                },
                {
                    ""Name"": ""IsTablet"",
                    ""Type"": ""Boolean"",
                    ""Category"": ""Device""
                }
            ]
        },
        ""devices"": {
            ""DataTier"": ""CloudV4TAC"",
            ""Properties"": [
                {
                    ""Name"": ""Devices"",
                    ""Type"": ""Array"",
                    ""Category"": ""Unspecified"",
                    ""ItemProperties"": [
                        {
                            ""Name"": ""IsMobile"",
                            ""Type"": ""Boolean"",
                            ""Category"": ""Device""
                        },
                        {
                            ""Name"": ""IsTablet"",
                            ""Type"": ""Boolean"",
                            ""Category"": ""Device""
                        }
                    ]
                }
            ]
        }
    }
}";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey("key")
                .Build();

            Assert.AreEqual(2, engine.PublicProperties.Count);
            var deviceProperties = engine.PublicProperties["device"];
            Assert.AreEqual(2, deviceProperties.Properties.Count);
            Assert.IsTrue(deviceProperties.Properties.Any(p => p.Name.Equals("IsMobile")));
            Assert.IsTrue(deviceProperties.Properties.Any(p => p.Name.Equals("IsTablet")));
            var devicesProperties = engine.PublicProperties["devices"];
            Assert.AreEqual(1, devicesProperties.Properties.Count);
            Assert.AreEqual("Devices", devicesProperties.Properties[0].Name);
            Assert.IsTrue(devicesProperties.Properties[0].ItemProperties.Any(p => p.Name.Equals("IsMobile")));
            Assert.IsTrue(devicesProperties.Properties[0].ItemProperties.Any(p => p.Name.Equals("IsTablet")));
        }

        /// <summary>
        /// Setup _httpClient to respond with the configured messages.
        /// </summary>
        private void ConfigureMockedClient(
            Func<HttpRequestMessage, bool> expectedJsonParameters)
        {
            // ARRANGE
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            // Set up the JSON response.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r => expectedJsonParameters(r)
                      && r.RequestUri.AbsolutePath.ToLower().EndsWith("json")),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(_jsonResponse),
               })
               .Verifiable();
            // Set up the evidencekeys response.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("evidencekeys")),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(_evidenceKeysResponse),
               })
               .Verifiable();
            // Set up the accessibleproperties response.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("accessibleproperties")),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(_accessiblePropertiesResponse),
               })
               .Verifiable();

            // use real http client with mocked handler here
            _httpClient = new HttpClient(_handlerMock.Object);
        }
    }
}
