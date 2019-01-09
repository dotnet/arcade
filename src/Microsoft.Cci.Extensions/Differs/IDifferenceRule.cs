// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Mappings;
using System.Diagnostics.Contracts;

namespace Microsoft.Cci.Differs
{
    public interface IDifferenceRule
    {
        DifferenceType Diff<T>(IDifferences differences, ElementMapping<T> mapping) where T : class;
    }

    public interface IDifferenceRuleMetadata
    {
        bool MdilServicingRule { get; }

        bool OptionalRule { get; }
    }

#if COREFX
    /// <summary>
    /// Metadata views must be concrete types rather than interfaces.
    /// </summary>
    /// <remarks>https://github.com/MicrosoftArchive/mef/blob/master/Wiki/MetroChanges.md#format-of-metadata-views</remarks>
    public class DifferenceRuleMetadata : IDifferenceRuleMetadata
    {
        public bool MdilServicingRule { get; set; }

        public bool OptionalRule { get; set; }
    }
#endif
}
