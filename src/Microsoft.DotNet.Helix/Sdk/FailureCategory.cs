// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Helix
{
    public struct FailureCategory
    {
        public string Value { get; }

        public static FailureCategory Build { get; } = new FailureCategory("Build");
        public static FailureCategory Test { get; } = new FailureCategory("Test");
        public static FailureCategory Infrastructure { get; } = new FailureCategory("Infrastructure");

        private FailureCategory(string value)
        {
            Value = value;
        }
    }
}