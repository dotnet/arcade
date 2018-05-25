// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace XliffTasks.Tasks
{
    internal sealed class BuildErrorException : Exception
    {
        /// <summary>
        /// Well-known key for associating a file path with this
        /// <see cref="BuildErrorException"/>.
        /// 
        /// When a file path is added to the <see cref="Exception.Data"/> dictionary
        /// using this value as the key, the file will be associated with the error in
        /// the build logs.
        /// </summary>
        public const string RelatedFile = "RelatedFile";

        public BuildErrorException(string message) : base(message)
        {
        }

        public BuildErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
