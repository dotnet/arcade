// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Error that occurred during signing.
    /// </summary>
    public sealed class SigningError
    {
        /// <summary>
        /// File that caused the error (if applicable).
        /// </summary>
        public string? FilePath { get; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Exception that caused the error (if applicable).
        /// </summary>
        public Exception? Exception { get; }

        public SigningError(string message, string? filePath = null, Exception? exception = null)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            FilePath = filePath;
            Exception = exception;
        }

        public override string ToString()
        {
            return FilePath != null ? $"{FilePath}: {Message}" : Message;
        }
    }
}
