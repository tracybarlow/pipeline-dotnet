﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace FiftyOne.Pipeline.Engines.FiftyOne {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("FiftyOne.Pipeline.Engines.FiftyOne.Messages", typeof(Messages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The minimum entries per message cannot be larger than the maximum size of the queue.
        /// </summary>
        internal static string ExceptionShareUsageMinimumEntriesTooLarge {
            get {
                return ResourceManager.GetString("ExceptionShareUsageMinimumEntriesTooLarge", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot add ShareUsageElement to multiple pipelines.
        /// </summary>
        internal static string ExceptionShareUsageSinglePipeline {
            get {
                return ResourceManager.GetString("ExceptionShareUsageSinglePipeline", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to increment usage sequence number.
        /// </summary>
        internal static string MessageFailSequenceNumberIncrement {
            get {
                return ResourceManager.GetString("MessageFailSequenceNumberIncrement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to retrieve sequence number.
        /// </summary>
        internal static string MessageFailSequenceNumberRetreive {
            get {
                return ResourceManager.GetString("MessageFailSequenceNumberRetreive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Usage sharing was canceled due to an error.
        /// </summary>
        internal static string MessageShareUsageCancelled {
            get {
                return ResourceManager.GetString("MessageShareUsageCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Share usage was canceled after failing to add data to the collection. This may mean that the max collection size is too low for the amount of traffic / min devices to send, or that the &apos;send&apos; thread has stopped taking data from the collection.
        /// </summary>
        internal static string MessageShareUsageFailedToAddData {
            get {
                return ResourceManager.GetString("MessageShareUsageFailedToAddData", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuration for &apos;{0}&apos; is invalid.{1}.
        /// </summary>
        internal static string MessageShareUsageInvalidConfig {
            get {
                return ResourceManager.GetString("MessageShareUsageInvalidConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Share usage element not registered to any Pipelines. Unable to populate flow element information.
        /// </summary>
        internal static string MessageShareUsageNoPipelines {
            get {
                return ResourceManager.GetString("MessageShareUsageNoPipelines", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Share usage element registered to {0} Pipelines. Unable to populate flow element information.
        /// </summary>
        internal static string MessageShareUsageTooManyPipelines {
            get {
                return ResourceManager.GetString("MessageShareUsageTooManyPipelines", resourceCulture);
            }
        }
    }
}
