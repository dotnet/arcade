// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Rest;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class RichExceptionTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ExceptionTelemetry exceptionTelemetry)
            {
                Exception ex = exceptionTelemetry.Exception;
                if (ex is HttpOperationException httpEx)
                {
                    HttpResponseMessageWrapper res = httpEx.Response;
                    exceptionTelemetry.Properties["statusCode"] = ((int) res.StatusCode).ToString();
                    string content = res.Content;
                    if (content.Length > 512)
                    {
                        content = content.Substring(0, 512);
                    }

                    exceptionTelemetry.Properties["responseText"] = content;
                }
            }
        }
    }
}
