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

using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.Templates;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using NUglify;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Linq;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using Stubble.Core.Builders;
using Stubble.Core;
using System.IO;
using System.Reflection;
using Stubble.Core.Settings;
using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Pipeline.JavaScriptBuilder.FlowElement
{
    /// <summary>
    /// JavaScript Builder Element generates a JavaScript include to be run on 
    /// the client device.
    /// </summary>
	public class JavaScriptBuilderElement : 
        FlowElementBase<IJavaScriptBuilderElementData, IElementPropertyMetaData>
	{
        /// <summary>
        /// Evidence filter containing required evidence.
        /// </summary>
		private IEvidenceKeyFilter _evidenceKeyFilter;

        /// <summary>
        /// Properties returned by the engine.
        /// </summary>
		private IList<IElementPropertyMetaData> _properties;

        /// <summary>
        /// The host name to use when creating a callback URL.
        /// </summary>
        protected string Host { get; private set; }
        /// <summary>
        /// The end point (i.e. the relative URL) to use when creating 
        /// a callback URL.
        /// </summary>
        protected string Endpoint { get; private set; }
        /// <summary>
        /// The protocol to use when creating a callback URL.
        /// </summary>
        protected string Protocol { get; private set; }
        /// <summary>
        /// The name of the JavaScript object that will be created.
        /// </summary>
        protected string ObjName { get; private set; }
        /// <summary>
        /// If set to false, the JavaScript will automatically delete
        /// any cookies prefixed with 51D_
        /// </summary>
        protected bool EnableCookies { get; private set; }

        private bool _minify;
        private StubbleVisitorRenderer _stubble;
        private Assembly _assembly;
        private string _template;
        private RenderSettings _renderSettings;

        /// <summary>
        /// Flag set if last request resulted in an error, stops processing if 
        /// true.
        /// </summary>
        private bool _lastRequestWasError;

        /// <summary>
        /// Key to identify engine.
        /// </summary>
        public override string ElementDataKey
		{
			get { return "javascriptbuilderelement"; }
		}

        /// <summary>
        /// Publicly accessible EvidenceKeyFilter
        /// </summary>
		public override IEvidenceKeyFilter EvidenceKeyFilter => 
			_evidenceKeyFilter;

        /// <summary>
        /// Publicly accessible Property list.
        /// </summary>
		public override IList<IElementPropertyMetaData> Properties => 
			_properties;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger.
        /// </param>
        /// <param name="elementDataFactory">
        /// The element data factory.
        /// </param>
        /// <param name="endpoint">
        /// Set the endpoint which will be queried on the host. e.g /api/v4/json
        /// </param>
        /// <param name="objectName">
        /// The default name of the object instantiated by the client 
        /// JavaScript.
        /// </param>
        /// <param name="enableCookies">
        /// Set whether the client JavaScript stored results of client side
        /// processing in cookies.
        /// </param>
        /// <param name="minify">
        /// If true, the resulting JavaScript will be minified
        /// </param>
        /// <param name="host">
        /// The host that the client JavaScript should query for updates.
        /// If null or blank then the host from the request will be used
        /// </param>
        /// <param name="protocol">
        /// The protocol (HTTP or HTTPS) that the client JavaScript will use when 
        /// querying for updates.
        /// If null or blank then the protocol from the request will be used
        /// </param>
		public JavaScriptBuilderElement(
            ILogger<JavaScriptBuilderElement> logger,
            Func<IPipeline,
                FlowElementBase<IJavaScriptBuilderElementData, IElementPropertyMetaData>,
                IJavaScriptBuilderElementData> elementDataFactory,
            string endpoint,
            string objectName,
            bool enableCookies,
            bool minify,
            string host = null,
            string protocol = null)
            : base(logger, elementDataFactory)
        {
            // Set the evidence key filter for the flow data to use.
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(
                new List<string>() {
                    Constants.EVIDENCE_HOST_KEY,
                    Constants.EVIDENCE_PROTOCOL,
                    Constants.EVIDENCE_OBJECT_NAME
                });

            _properties = new List<IElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this,
                        "javascript",
                        typeof(string),
                        true)
                };

            Host = host;
            Endpoint = endpoint;
            Protocol = protocol;
            ObjName = string.IsNullOrEmpty(objectName) ? Constants.DEFAULT_OBJECT_NAME : objectName;
            EnableCookies = enableCookies;
            _minify = minify;

            _stubble = new StubbleBuilder().Build();
            _assembly = Assembly.GetExecutingAssembly();
            _renderSettings = new RenderSettings() { SkipHtmlEncoding = true };

            using (Stream stream = _assembly.GetManifestResourceStream(Constants.TEMPLATE))
            using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
            {
                _template = streamReader.ReadToEnd();
            }
        }

        /// <summary>
        /// Cleanup any managed resources.
        /// </summary>
		protected override void ManagedResourcesCleanup()
		{ }

        /// <summary>
        /// Default process method.
        /// </summary>
        /// <param name="data"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            SetUp(data);
        }

        private void SetUp(IFlowData data)
        {
            var host = Host;
            var protocol = Protocol;
            bool supportsPromises;

            if (string.IsNullOrEmpty(host))
            {
                // Try and get the request host name so it can be used to request
                // the Json refresh in the JavaScript code.
                data.TryGetEvidence(Constants.EVIDENCE_HOST_KEY, out host);
            }
            if (string.IsNullOrEmpty(protocol))
            {
                // Try and get the request protocol so it can be used to request
                // the JSON refresh in the JavaScript code.
                data.TryGetEvidence(Constants.EVIDENCE_PROTOCOL, out protocol);
            }
            // Couldn't get protocol from anywhere 
            if (string.IsNullOrEmpty(protocol))
            {
                protocol = Constants.DEFAULT_PROTOCOL;
            }

            // If device detection is in the Pipeline then we can check
            // if the client's browser supports promises.
            // This can be used to customize the JavaScript response. 
            try
            {
                var promise = data.GetAs<IAspectPropertyValue<string>>("Promise");
                supportsPromises = promise != null && promise.HasValue && promise.Value == "Full";
            }
            catch (PipelineDataException) { supportsPromises = false; }
            catch (InvalidCastException) { supportsPromises = false; }
            catch (KeyNotFoundException) { supportsPromises = false; }

            // Get the JSON include to embed into the JavaScript include.
            string jsonObject = string.Empty;
            try
            {
                jsonObject = data.Get<IJsonBuilderElementData>().Json;
            }
            catch (KeyNotFoundException ex)
            {
                throw new PipelineConfigurationException(
                    Messages.ExceptionJsonBuilderNotRun, ex);
            }

            // Generate any required parameters for the JSON request.
            List<string> parameters = new List<string>();

            // Any query parameters from this request that were ingested by 
            // the Pipeline are added to the request URL that will appear 
            // in the JavaScript.
            var queryEvidence = data
                .GetEvidence()
                .AsDictionary()
                .Where(e => e.Key.StartsWith(Core.Constants.EVIDENCE_QUERY_PREFIX, 
                    StringComparison.OrdinalIgnoreCase))
                .Select(k =>
                {
                    var dotPos = k.Key.IndexOf(Core.Constants.EVIDENCE_SEPERATOR,
                        StringComparison.OrdinalIgnoreCase);
                    return $"{WebUtility.UrlEncode(k.Key.Remove(0, dotPos + 1))}" +
                        $"={WebUtility.UrlEncode(k.Value.ToString())}";
                });

            parameters.AddRange(queryEvidence);

            string queryParams = string.Join("&", parameters);
            string endpoint = Endpoint;

            Uri url = null;

            if (string.IsNullOrWhiteSpace(protocol) == false &&
                string.IsNullOrWhiteSpace(host) == false &&
                string.IsNullOrWhiteSpace(endpoint) == false)
            {
                var endpointHasSlash = endpoint[0] == '/';
                var hostHasSlash = host[host.Length - 1] == '/';
                // if there is no slash between host and endpoint then add one.
                if (endpointHasSlash == false && hostHasSlash == false)
                {
                    endpoint = $"/{endpoint}";
                }
                // if there are two slashes between host and endpoint then remove one.
                else if (endpointHasSlash == true && hostHasSlash == true)
                {
                    endpoint = endpoint.Substring(1);
                }

                url = new Uri($"{protocol}://{host}{endpoint}" +
                    (String.IsNullOrEmpty(queryParams) ? "" : $"?{queryParams}"));
            }

            // With the gathered resources, build a new JavaScriptResource.
            BuildJavaScript(data, jsonObject, supportsPromises, url);
        }

        /// <summary>
        /// Build the JavaScript content and add it to the supplied
        /// <see cref="IFlowData"/> instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance to populate with the
        /// resulting <see cref="JavaScriptBuilderElementData"/> 
        /// </param>
        /// <param name="jsonObject">
        /// The JSON data object to include in the JavaScript.
        /// </param>
        /// <param name="supportsPromises">
        /// True to build JavaScript that uses promises. False to
        /// build JavaScript that does not use promises.
        /// </param>
        /// <param name="url">
        /// The callback URL for the JavaScript to send a request to
        /// when it has new evidence values to supply.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected void BuildJavaScript(IFlowData data, string jsonObject, bool supportsPromises, string url)
        {
            BuildJavaScript(data, jsonObject, supportsPromises, new Uri(url));
        }

        /// <summary>
        /// Build the JavaScript content and add it to the supplied
        /// <see cref="IFlowData"/> instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance to populate with the
        /// resulting <see cref="JavaScriptBuilderElementData"/> 
        /// </param>
        /// <param name="jsonObject">
        /// The JSON data object to include in the JavaScript.
        /// </param>
        /// <param name="supportsPromises">
        /// True to build JavaScript that uses promises. False to
        /// build JavaScript that does not use promises.
        /// </param>
        /// <param name="url">
        /// The callback URL for the JavaScript to send a request to
        /// when it has new evidence values to supply.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected void BuildJavaScript(IFlowData data, string jsonObject, bool supportsPromises, Uri url)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            JavaScriptBuilderElementData elementData = (JavaScriptBuilderElementData)
                data.GetOrAdd(
                ElementDataKeyTyped,
                CreateElementData);

            // Try and get the requested object name from evidence.
            if (data.TryGetEvidence(Constants.EVIDENCE_OBJECT_NAME, out string objectName) == false ||
                string.IsNullOrWhiteSpace(objectName))
            {
                objectName = ObjName;
            }

            var ubdateEnabled = url != null &&
                url.AbsoluteUri.Length > 0;

            JavaScriptResource javaScriptObj = new JavaScriptResource(
                objectName,
                jsonObject,
                supportsPromises,
                url,
                EnableCookies,
                ubdateEnabled);

            string content = _stubble.Render(_template, javaScriptObj.AsDictionary()/*, _renderSettings*/);

            string minifiedContent = content;

            if (_minify)
            {
                // Minimize the script.
                var ugly = Uglify.Js(content);

                if (ugly.HasErrors)
                {
                    // If there were are errors then log them and
                    // return the non-minified response.

                    minifiedContent = content;

                    if (_lastRequestWasError == false)
                    {
                        StringBuilder errorText = new StringBuilder();
                        errorText.AppendLine("Errors occurred when minifying JavaScript.");
                        foreach (var error in ugly.Errors)
                        {
                            errorText.AppendLine($"{error.ErrorCode}: {error.Message}. " +
                                $"Line(s) {error.StartLine}-{error.EndLine}. " +
                                $"Column(s) {error.StartColumn}-{error.EndColumn}");
                        }
                        errorText.AppendLine(content);
                        Logger.LogError(errorText.ToString());
                        _lastRequestWasError = true;
#pragma warning disable CS0618 // Type or member is obsolete
                        // This usage should be replaced with the 
                        // CancellationToken implementation once it 
                        // is available.
                        data.Stop = true;
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                }
                else
                {
                    minifiedContent = ugly.Code;
                }
            }

            elementData.JavaScript = minifiedContent;
        }

        /// <summary>
        /// Cleanup any unmanaged resources.
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
		{ }
	}
}
