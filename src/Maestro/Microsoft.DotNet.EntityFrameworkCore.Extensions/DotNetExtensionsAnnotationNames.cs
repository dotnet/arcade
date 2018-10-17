// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.EntityFrameworkCore.Extensions
{
    public static class DotNetExtensionsAnnotationNames
    {
        public const string Columnstore = "SqlServer:ColumnstoreIndex";
        public const string SystemVersioned = "SqlServer:SystemVersioned";
        public const string HistoryTable = "SqlServer:HistoryTable";
        public const string RetentionPeriod = "SqlServer:HistoryRetentionPeriod";
    }
}
