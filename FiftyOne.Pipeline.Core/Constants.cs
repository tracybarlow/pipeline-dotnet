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

using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.Core
{
    public static class Constants
    {
        // Evidence Separator
        public const string EVIDENCE_SEPERATOR = ".";

        // Evidence Prefixes
        public const string EVIDENCE_HTTPHEADER_PREFIX = "header";
        public const string EVIDENCE_COOKIE_PREFIX = "cookie";
        public const string EVIDENCE_QUERY_PREFIX = "query";
        public const string EVIDENCE_SERVER_PREFIX = "server";
        public const string EVIDENCE_SESSION_PREFIX = "session";

        // Evidence Suffixes
        public static string EVIDENCE_USERAGENT = "User-Agent";

        // Evidence Keys
        public static string EVIDENCE_CLIENTIP_KEY = EVIDENCE_SERVER_PREFIX + EVIDENCE_SEPERATOR + "client-ip";

        public static string EVIDENCE_QUERY_USERAGENT_KEY = EVIDENCE_QUERY_PREFIX + EVIDENCE_SEPERATOR + EVIDENCE_USERAGENT;

        public static string EVIDENCE_HEADER_USERAGENT_KEY = EVIDENCE_HTTPHEADER_PREFIX + EVIDENCE_SEPERATOR + EVIDENCE_USERAGENT;

        public static string EVIDENCE_SESSION_KEY = EVIDENCE_SESSION_PREFIX + EVIDENCE_SEPERATOR + "session";
    }
}