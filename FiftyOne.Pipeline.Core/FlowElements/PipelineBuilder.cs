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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Default pipeline builder.
    /// </summary>
    public class PipelineBuilder : PipelineBuilderBase<PipelineBuilder>,
        IPipelineBuilderFromConfiguration
    {
        /// <summary>
        /// A list of all the types that are element builders.
        /// I.e. they have build method (with or without parameters) 
        /// that returns an IFlowElement.
        /// </summary>
        private List<Type> _elementBuilders;

        /// <summary>
        /// Service collection which contains builder instances.
        /// </summary>
        private IServiceProvider _services = null;

        /// <summary>
        /// Create a new <see cref="PipelineBuilder"/> instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating logger
        /// instances.
        /// </param>
        public PipelineBuilder(ILoggerFactory loggerFactory) 
            : base(loggerFactory)
        {
            GetAvailableElementBuilders();
        }

        /// <summary>
        /// Create a new <see cref="PipelineBuilder"/> instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating logger
        /// instances.
        /// </param>
        /// <param name="services">
        /// Collection of services which contain builder instances for the
        /// required elements.
        /// </param>
        public PipelineBuilder(ILoggerFactory loggerFactory, 
            IServiceProvider services)
            : this(loggerFactory)
        {
            _services = services;
        }

        /// <summary>
        /// Build the pipeline using the specified configuration options.
        /// </summary>
        /// <param name="options">
        /// A <see cref="PipelineOptions"/> instance describing how to build
        /// the pipeline.
        /// </param>
        /// <returns>
        /// A new <see cref="IPipeline"/> instance containing the configured
        /// <see cref="IFlowElement"/> instances.
        /// </returns>
        /// <exception cref="PipelineConfigurationException"></exception>
        public virtual IPipeline BuildFromConfiguration(PipelineOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            // Clear the list of flow elements ready to be populated
            // from the configuration options.
            FlowElements.Clear();
            int counter = 0;

            try
            {
                foreach (var elementOptions in options.Elements)
                {
                    if (elementOptions.SubElements != null &&
                        elementOptions.SubElements.Count > 0)
                    {
                        // The configuration has sub elements so create
                        // a ParallelElements instance.
                        AddParallelElementsToList(FlowElements, elementOptions, counter);
                    }
                    else
                    {
                        // The configuration has no sub elements so create
                        // a flow element.
                        AddElementToList(FlowElements, elementOptions,
                            $"element {counter}");
                    }
                    counter++;
                }

                // Process any additional parameters for the pipeline
                // builder itself.
                ProcessBuildParameters(
                    options.PipelineBuilderParameters,
                    GetType(),
                    this,
                    "pipeline");
            }
            catch (PipelineConfigurationException ex)
            {
                Logger.LogCritical(ex, "Problem with pipeline configuration, " +
                    "failed to create pipeline.");
                throw;
            }
            // As the elements are all created within the builder, the user
            // will not be handling disposal so make sure the pipeline is
            // configured to do so.
            SetAutoDisposeElements(true);

            // Build and return the pipeline using the list of flow elements
            // that have been created from the configuration options.
            return Build();
        }

        /// <summary>
        /// Use reflection to get all element builders.
        /// These are defined as any type that has a Build method 
        /// where the return type is or implements IFlowElement.
        /// These will be used when building a pipeline from configuration.
        /// </summary>
        private void GetAvailableElementBuilders()
        {
            _elementBuilders = new List<Type>();
            // Get all loaded types where there is at least one..
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
#if DEBUG
                // Exclude VisualStudio assemblies
                .Where(a => !a.FullName.StartsWith("Microsoft.VisualStudio"))
#endif
                )
            {
                // Exclude dynamic assemblies
                if (assembly.IsDynamic == false)
                {
                    try
                    {
                        _elementBuilders.AddRange(assembly.GetTypes()
                            .Where(t => t.GetMethods()
                                // ..method called 'Build'..
                                .Count(m => m.Name == "Build" &&
                                // ..where the return type is or implements IFlowElement
                                (m.ReturnType == typeof(IFlowElement) ||
                                m.ReturnType.GetInterfaces().Contains(typeof(IFlowElement)))) > 0)
                        .ToList());
                    }
                    // Catch type load exceptions when assembly can't be loaded 
                    // and log a warning. 
                    catch (ReflectionTypeLoadException ex)
                    {
                        Logger.LogDebug(ex, $"Failed to get Types for {assembly.FullName}", null);
                    }
                }
            }
        }

        /// <summary>
        /// Create a new <see cref="IFlowElement"/> using the specified
        /// <see cref="ElementOptions"/> and add it to the supplied list
        /// of elements.
        /// </summary>
        /// <param name="elements">
        /// The list to add the new <see cref="IFlowElement"/> to.
        /// </param>
        /// <param name="elementOptions">
        /// The <see cref="ElementOptions"/> instance to use when creating
        /// the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementLocation">
        /// The string description of the element's location within the 
        /// <see cref="PipelineOptions"/> instance.
        /// </param>
        private void AddElementToList(
            List<IFlowElement> elements,
            ElementOptions elementOptions,
            string elementLocation)
        {
            // Check that a builder name is set
            if (string.IsNullOrEmpty(elementOptions.BuilderName))
            {
                throw new PipelineConfigurationException(
                    $"A BuilderName must be specified for " +
                    $"{elementLocation}.");
            }

            // Try to get the builder to use
            var builderType = GetBuilderType(elementOptions.BuilderName);
            if (builderType == null)
            {
                throw new PipelineConfigurationException(
                    $"Could not find builder matching " +
                    $"'{elementOptions.BuilderName}' for " +
                    $"{elementLocation}. Available builders are: " +
                    $"{string.Join(",", _elementBuilders.Select(t => t.Name))}");
            }

            // Get the methods on the builder
            var buildMethods = builderType.GetRuntimeMethods()
                .Where(m => m.Name == "Build");
            // If there are no 'Build' methods or if there is no default 
            // constructor then throw an error.
            if (buildMethods.Count() == 0)
            {
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has no 'Build' methods.");
            }

            object builderInstance = null;
            if (_services != null)
            {
                // Try to get a a builder instance from the service collection.
                builderInstance = _services.GetRequiredService(builderType);
            }
            else
            {
                // A service collection does not exist in the builder, so try
                // to construct a builder instance from the assemblies
                // currently loaded.
                builderInstance = GetBuilderFromAssemlies(builderType);
                if (builderInstance == null)
                {
                    throw new PipelineConfigurationException(
                        $"Builder '{builderType.FullName}' for " +
                        $"{elementLocation} does not have a default constructor. " +
                        $"i.e. One that takes no parameters. Or a constructor " +
                        $"which takes an ILoggerFactory parameter.");
                }
            }
            // Holds a list of the names of parameters to pass to the 
            // build method when we're ready.
            List<string> buildParameterList = new List<string>();

            if (elementOptions.BuildParameters != null)
            {
                // Call any non-build methods on the builder to set optional
                // parameters.
                buildParameterList = ProcessBuildParameters(
                    elementOptions.BuildParameters,
                    builderType,
                    builderInstance,
                    elementLocation);
            }

            // At this point, all the optional methods on the builder 
            // have been called and we're ready to call the Build method.
            // If there are no matching build methods or multiple possible 
            // build methods based on our parameters then throw an exception.
            var possibleBuildMethods = buildMethods.Where(m =>
                m.GetParameters().Count() == buildParameterList.Count &&
                m.GetParameters().All(p => buildParameterList.Contains(p.Name.ToLower())));

            StringBuilder buildSignatures = new StringBuilder();
            buildSignatures.AppendLine();
            foreach (var method in buildMethods)
            {
                buildSignatures.AppendLine(string.Join(",",
                    method.GetParameters().Select(p =>
                    $"{p.Name} ({p.ParameterType.Name})")));
            }

            if (possibleBuildMethods.Count() == 0)
            {
                StringBuilder methodSignatures = new StringBuilder();
                methodSignatures.AppendLine();
                foreach (var method in builderType.GetRuntimeMethods()
                    .Where(m => m.IsPublic && m.GetParameters().Count() == 1
                        && m.DeclaringType.Name.ToLower().Contains("pipeline")))
                {
                    methodSignatures.AppendLine($"{method.Name} ({method.GetParameters()[0].GetType().Name})");
                }
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has no " +
                    $"'Set' methods or 'Build' methods that match " +
                    $"(case-insensitively) the following parameters: " +
                    $"{string.Join(",", buildParameterList)}. " +
                    $"The available configuration methods on this builder are: " +
                    $"{methodSignatures.ToString()}" +
                    $"The available 'Build' methods have the following signatures: " +
                    $"{buildSignatures.ToString()}");
            }
            else if (possibleBuildMethods.Count() > 1)
            {
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has multiple " +
                    $"'Build' methods that match the following parameters: " +
                    $"{string.Join(",", buildParameterList)}. " +
                    $"Matching method signatures are: {buildSignatures.ToString()}");
            }

            // Get the build method parameters and add the configured
            // values to the parameter list.
            List<object> parameters = new List<object>();
            var buildMethod = possibleBuildMethods.Single();
            foreach (var parameterInfo in buildMethod.GetParameters())
            {
                var caseInsensitiveParameters = elementOptions.BuildParameters
                    .ToDictionary(d => d.Key.ToLower(), d => d.Value);
                var paramType = parameterInfo.ParameterType;
                object paramValue = caseInsensitiveParameters[parameterInfo.Name.ToLower()];
                if (paramType != typeof(string))
                {
                    paramValue = ParseToType(paramType,
                        (string)paramValue,
                        $"Method 'Build' on builder " +
                        $"'{builderType.FullName}' for " +
                        $"{elementLocation} expects a parameter of type " +
                        $"'{paramType.Name}'");
                }
                parameters.Add(paramValue);
            }
            // Call the build method with the parameters we set up above.
            object result = buildMethod.Invoke(builderInstance, parameters.ToArray());
            if (result == null)
            {
                throw new PipelineConfigurationException(
                    $"Failed to build {elementLocation} using " +
                    $"'{builderType.FullName}', reason unknown.");
            }
            IFlowElement element = result as IFlowElement;
            if (element == null)
            {
                throw new PipelineConfigurationException(
                    $"Failed to cast '{result.GetType().FullName}' to " +
                    $"'IFlowElement' for {elementLocation}");
            }

            // Add the element to the list.
            elements.Add(element);
        }

        /// <summary>
        /// Create a <see cref="ParallelElements"/> from the specified 
        /// configuration and add it to the _flowElements list.
        /// </summary>
        /// <param name="elements">
        /// The list to add the new <see cref="ParallelElements"/> to.
        /// </param>
        /// <param name="elementOptions">
        /// The <see cref="ElementOptions"/> instance to use when creating
        /// the <see cref="ParallelElements"/>.
        /// </param>
        /// <param name="elementIndex">
        /// The index of the element within the <see cref="PipelineOptions"/>.
        /// </param>
        private void AddParallelElementsToList(
            List<IFlowElement> elements,
            ElementOptions elementOptions,
            int elementIndex)
        {
            // Element contains further sub elements, this is not allowed.
            if (string.IsNullOrEmpty(elementOptions.BuilderName) == false ||
                (elementOptions.BuildParameters != null &&
                elementOptions.BuildParameters.Count > 0))
            {
                throw new PipelineConfigurationException(
                    $"ElementOptions {elementIndex} contains both " +
                    $"SubElements and other settings values. " +
                    $"This is invalid");
            }
            List<IFlowElement> parallelElements = new List<IFlowElement>();

            // Iterate through the sub elements, creating them and
            // adding them to the list.
            int subCounter = 0;
            foreach (var subElement in elementOptions.SubElements)
            {
                if (subElement.SubElements != null &&
                    subElement.SubElements.Count > 0)
                {
                    throw new PipelineConfigurationException(
                        $"ElementOptions {elementIndex} contains nested " +
                        $"sub elements. This is not supported.");
                }
                else
                {
                    AddElementToList(parallelElements, subElement,
                        $"element {subCounter} in element {elementIndex}");
                }
                subCounter++;
            }
            // Now we've created all the elements, create the 
            // ParallelElements instance and add it to the pipeline's 
            // elements.
            var parallelInstance = new ParallelElements(
                LoggerFactory.CreateLogger<ParallelElements>(),
                parallelElements.ToArray());
            FlowElements.Add(parallelInstance);
        }

        /// <summary>
        /// Instantiate a new builder instance from the assemblies which are
        /// currently loaded.
        /// </summary>
        /// <param name="builderType">The type of builder to get</param>
        /// <returns></returns>
        private object GetBuilderFromAssemlies(Type builderType)
        {
            // Get the valid constructors. This means either a default
            // constructor, or a constructor taking a logger factory as an
            // argument.
            var defaultConstructors = builderType.GetConstructors()
                .Where(c => c.GetParameters().Count() == 0);
            var loggerConstructors = builderType.GetConstructors()
                .Where(c => c.GetParameters().Count() == 1 &&
                c.GetParameters()[0].ParameterType == typeof(ILoggerFactory));
            if (defaultConstructors.Count() == 0 &&
                loggerConstructors.Count() == 0)
            {
                return null;
            }

            // Create the builder instance using the constructor with a logger
            // factory, or the default constructor if one taking a logger
            // factory is not available.
            if (loggerConstructors.Count() != 0)
            {
                return Activator.CreateInstance(builderType, LoggerFactory);
            }
            else
            {
                return Activator.CreateInstance(builderType);
            }
        }

        /// <summary>
        /// Call the non-build methods on the builder that configuration
        /// options have been supplied for.
        /// </summary>
        /// <remarks>
        /// Each method must take only one parameter and the parameter type
        /// must either be a string or have a 'TryParse' method available.
        /// </remarks>
        /// <param name="buildParameters">
        /// A dictionary containing the names of the methods to call and 
        /// the value to pass as a parameters.
        /// </param>
        /// <param name="builderType">
        /// The <see cref="Type"/> of the builder that is being used to
        /// create the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="builderInstance">
        /// The instance of the builder that is being used to create the
        /// <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementConfigLocation">
        /// A string containing a description of the location of the 
        /// configuration for this element.
        /// This will be added to error messages to help the user identify
        /// any problems.
        /// </param>
        /// <returns>
        /// A list of the names of the entries from buildParameters that 
        /// are to be used as mandatory parameters to the Build method 
        /// rather than optional builder methods.
        /// </returns>
        private List<string> ProcessBuildParameters(
            IDictionary<string, object> buildParameters,
            Type builderType,
            object builderInstance,
            string elementConfigLocation)
        {
            // Holds a list of the names of parameters to pass to the 
            // build method when we're ready.
            List<string> buildParameterList = new List<string>();

            foreach (var parameter in buildParameters)
            {
                // Check if the build parameter corresponds to a method
                // on the builder.
                var methods = GetMethods(parameter.Key, builderType.GetMethods());
                if (methods == null)
                {
                    // If not then add the parameter to the list of parameters
                    // to pass to the Build method instead. 
                    buildParameterList.Add(parameter.Key.ToLower());
                }
                else
                {
                    var methodCalled = false;
                    int counter = 0;
                    while (methodCalled == false && counter < methods.Count)
                    {
                        var method = methods[counter];
                        counter++;
                        
                        // The parameter corresponds to a method on the builder
                        // so get the parameters associated with that method.
                        var methodParams = method.GetParameters();
                        if (methodParams.Count() != 1)
                        {
                            throw new PipelineConfigurationException(
                                $"Method '{method.Name}' on builder " +
                                $"'{builderType.FullName}' for " +
                                $"{elementConfigLocation} takes " +
                                $"{(methodParams.Count() == 0 ? "no parameters " : "more than one parameter. ")}" +
                                $"This is not supported.");
                        }
                        // Call any methods which relate to the build parameters
                        // supplied in the configuration.
                        try
                        {
                            if (parameter.Value.GetType().IsArray == true)
                            {
                                // The parameter is an array, so try each value
                                // individually.
                                foreach (var value in (object[])parameter.Value)
                                {
                                    TryParseAndCallMethod(
                                        value,
                                        method,
                                        builderType,
                                        builderInstance,
                                        elementConfigLocation);
                                }
                            }
                            else
                            {
                                // Not an array, so just call the method.
                                TryParseAndCallMethod(
                                    parameter.Value,
                                    method,
                                    builderType,
                                    builderInstance,
                                    elementConfigLocation);
                            }
                            methodCalled = true;
                        }
                        catch (PipelineConfigurationException)
                        {
                            if (counter == methods.Count)
                            {
                                throw;
                            }
                        }
                    }
                }
            }

            return buildParameterList;
        }

        /// <summary>
        /// Attempt to call a method on the builder using the parameter value
        /// provided. The value can be parsed to basic types (e.g. string or
        /// int) but complex types are not supported.
        /// </summary>
        /// <param name="paramValue">
        /// Value of the parameter to call the method with
        /// </param>
        /// <param name="method">Method to attempt to call</param>
        /// <param name="builderType">
        /// The <see cref="Type"/> of the builder that is being used to
        /// create the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="builderInstance">
        /// The instance of the builder that is being used to create the
        /// <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementConfigLocation">
        /// A string containing a description of the location of the 
        /// configuration for this element.
        /// This will be added to error messages to help the user identify
        /// any problems.
        /// </param>
        private void TryParseAndCallMethod(
            object paramValue,
            MethodInfo method,
            Type builderType,
            object builderInstance,
            string elementConfigLocation)
        {
            var paramType = method.GetParameters()[0].ParameterType;

            // If the method takes a string then we can just pass it
            // in. If not, we'll have to parse it to the required 
            // type.

            if (paramType != typeof(string))
            {
                paramValue = ParseToType(paramType, paramValue.ToString(),
                    $"Method '{method.Name}' on builder " +
                    $"'{builderType.FullName}' for " +
                    $"{elementConfigLocation} expects a parameter of type " +
                    $"'{paramType.Name}'");
            }

            // Invoke the method on the builder, passing the parameter
            // value that was defined in the configuration.
            method.Invoke(builderInstance, new object[] { paramValue });
        }

        /// <summary>
        /// Get the method associated with the given name.
        /// </summary>
        /// <param name="methodName">
        /// The name of the method to get.
        /// This is case insensitive and can be:
        /// 1. The exact method name
        /// 2. The method name with the text 'set' removed from the start.
        /// 3. An alternate name, as defined by an 
        /// <see cref="AlternateNameAttribute"/>
        /// </param>
        /// <param name="methods">
        /// The list of methods to try and find a match in.
        /// </param>
        /// <returns>
        /// The <see cref="MethodInfo"/> of the matching method or null if no
        /// match could be found.
        /// </returns>
        private List<MethodInfo> GetMethods(string methodName,
            IEnumerable<MethodInfo> methods)
        {
            int tries = 0;
            List<MethodInfo> matchingMethods = null;
            string lowerMethodName = methodName.ToLower();

            while (tries < 3 && matchingMethods == null)
            {
                IEnumerable<MethodInfo> potentialMethods = null;

                switch (tries)
                {
                    case 0:
                        // First try and find a method that matches the
                        // supplied name exactly.
                        potentialMethods = methods.Where(m => m.Name.ToLower() == lowerMethodName);
                        break;
                    case 1:
                        // Next, try and find a method that matches the
                        // supplied name with 'set' added to the start.
                        string tempName = "set" + lowerMethodName;
                        potentialMethods = methods.Where(m => m.Name.ToLower() == tempName);
                        break;
                    case 2:
                        // Finally, see if there is a method that has an
                        // AlternateNameAttribute with a matching name.
                        List<MethodInfo> tempMethods = new List<MethodInfo>();
                        foreach (var method in methods)
                        {
                            try
                            {
                                var attributes = method.GetCustomAttributes<AlternateNameAttribute>();
                                if (attributes.Any(a => a.Name.ToLower() == lowerMethodName))
                                {
                                    tempMethods.Add(method);
                                }
                                else if (attributes.Any(
                                    a => a.Name.ToLower() ==
                                    "set" + lowerMethodName))
                                {
                                    tempMethods.Add(method);
                                }
                            }
                            catch { }
                        }
                        potentialMethods = tempMethods;
                        break;
                    default:
                        break;
                }

                if (potentialMethods != null && potentialMethods.Count() > 0)
                {
                    matchingMethods = potentialMethods.ToList();
                }

                tries++;
            }

            return matchingMethods;
        }

        /// <summary>
        /// Get the element builder associated with the given name.
        /// </summary>
        /// <param name="builderName">
        /// The name of the builder to get.
        /// This is case insensitive and can be:
        /// 1. The builder type name
        /// 2. The builder type name with the text 'builder' removed 
        /// from the end.
        /// 3. An alternate name, as defined by an 
        /// <see cref="AlternateNameAttribute"/>
        /// </param>
        /// <returns>
        /// The <see cref="Type"/> of the element builder or null if no
        /// match could be found.
        /// </returns>
        private Type GetBuilderType(string builderName)
        {
            int tries = 0;
            Type builderType = null;
            string lowerBuilderName = builderName.ToLower();

            while (tries < 3 && builderType == null)
            {
                IEnumerable<Type> potentialBuilders = null;

                switch (tries)
                {
                    case 0:
                        // First try and find a builder that matches the
                        // supplied name exactly.
                        potentialBuilders = _elementBuilders
                            .Where(t => t.Name.ToLower() == lowerBuilderName);
                        break;
                    case 1:
                        string tempName = lowerBuilderName + "builder";
                        // Next, try and find a builder that matches the
                        // supplied name with 'builder' added to the end.
                        potentialBuilders = _elementBuilders
                            .Where(t => t.Name.ToLower() == tempName);
                        break;
                    case 2:
                        // Finally, see if there is a builder that has an
                        // AlternateNameAttribute with a matching name.
                        List<Type> builders = new List<Type>();
                        foreach (var builder in _elementBuilders)
                        {
                            try
                            {
                                var attributes = builder.GetCustomAttributes<AlternateNameAttribute>();
                                if (attributes.Any(a => a.Name.ToLower() == lowerBuilderName))
                                {
                                    builders.Add(builder);
                                }
                            }
                            catch { }
                        }
                        potentialBuilders = builders;
                        break;
                    default:
                        break;
                }

                if (potentialBuilders != null)
                {
                    // If the name matches multiple builders at any stage 
                    // then throw an error.
                    if (potentialBuilders.Count() > 1)
                    {
                        throw new PipelineConfigurationException(
                            $"The flow element builder name '{builderName}'" +
                            $"matches multiple builders: " +
                            $"[{string.Join(",", potentialBuilders.Select(t => t.FullName))}].");
                    }

                    try
                    {
                        builderType = potentialBuilders.SingleOrDefault();
                    }
                    catch (InvalidOperationException)
                    {
                        builderType = null;
                    }
                }

                tries++;
            }

            return builderType;
        }

        private object ParseToType(Type targetType, string value, string errorTextPrefix)
        {
            object result = null;
            // Check for a TryParse method on the type
            MethodInfo tryParse = null;
            try
            {
                tryParse = targetType.GetMethods()
                    .SingleOrDefault(m => m.Name == "TryParse" &&
                        m.GetParameters()[0].ParameterType == typeof(string) &&
                        m.GetParameters().Length == 2);
            }
            catch (InvalidOperationException)
            {
                tryParse = null;
            }
            if (tryParse == null)
            {
                throw new PipelineConfigurationException(
                    $"{errorTextPrefix} but this type does not have " +
                    $"the expected 'TryParse' method " +
                    $"(or has multiples that cannot be resolved).");
            }
            // Call the try parse method.
            object[] args = new object[] { value, null };
            if ((bool)tryParse.Invoke(null, args) == false)
            {
                throw new PipelineConfigurationException(
                    $"{errorTextPrefix}. Failed to parse value " +
                    $"'{value}' using the static " +
                    $"'TryParse' method.");
            }
            else
            {
                result = args[1];
            }
            return result;
        }
    }

}