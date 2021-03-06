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
using FiftyOne.Pipeline.Engines.FlowElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Meta-data relating to properties that are populated by Aspect Engines.
    /// </summary>
    public class AspectPropertyMetaData : ElementPropertyMetaData, 
        IAspectPropertyMetaData
    {
        /// <summary>
        /// A list of the data tiers that can be used to determine values 
        /// for this property.
        /// Examples values are:
        /// Lite
        /// Premium
        /// Enterprise
        /// </summary>
        public IList<string> DataTiersWherePresent { get; private set; }

        /// <summary>
        /// Full description of the property.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="element">
        /// The <see cref="IAspectEngine"/> that this property is associated 
        /// with.
        /// </param>
        /// <param name="name">
        /// The name of the property. Must match the string key used to
        /// store the property value in the <see cref="IAspectData"/> instance.
        /// </param>
        /// <param name="type">
        /// The type of the property values.
        /// </param>
        /// <param name="category">
        /// The category the property belongs to.
        /// </param>
        /// <param name="dataTiersWherePresent">
        /// A list of the data tiers that can be used to determine values 
        /// for this property.
        /// </param>
        /// <param name="available">
        /// True if the property is available in the results for the
        /// associated <see cref="IAspectEngine"/>, false otherwise.
        /// </param>
        /// <param name="description">
        /// Full description of the property.
        /// </param>
        /// <param name="itemProperties">
        /// The meta-data for properties that are stored in sub-items.
        /// Only relevant if this meta-data instance relates to a 
        /// collection of complex objects.
        /// </param>
        public AspectPropertyMetaData(
            IAspectEngine element,
            string name,
            Type type,
            string category,
            IList<string> dataTiersWherePresent,
            bool available,
            string description = "",
            IReadOnlyList<IElementPropertyMetaData> itemProperties = null) : 
            base(element, name, type, available, category, itemProperties)
        {
            DataTiersWherePresent = dataTiersWherePresent;
            Description = description;
        }
    }
}
