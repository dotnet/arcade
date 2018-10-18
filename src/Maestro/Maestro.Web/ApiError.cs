// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Maestro.Web
{
    public class ApiError
    {
        public ApiError(string message, IEnumerable<string> errors = null)
        {
            Message = message;
            Errors = errors;
        }

        public string Message { get; }
        public IEnumerable<string> Errors { get; }
    }
}
