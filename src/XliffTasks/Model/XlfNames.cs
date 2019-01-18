// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace XliffTasks.Model
{
    internal static class XlfNames
    {
        public static XNamespace XliffNS = "urn:oasis:names:tc:xliff:document:1.2";
        public static XName Xliff = XliffNS + "xliff";
        public static XName File = XliffNS + "file";
        public static XName Body = XliffNS + "body";
        public static XName Group = XliffNS + "group";
        public static XName TransUnit = XliffNS + "trans-unit";
        public static XName Source = XliffNS + "source";
        public static XName Target = XliffNS + "target";
        public static XName Note = XliffNS + "note";
    }
}
