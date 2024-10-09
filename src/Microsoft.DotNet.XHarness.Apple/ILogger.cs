// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Apple;

/// <summary>
/// Simple interface that can be implemented either via Microsoft.Extensions.Logging logger or
/// with some other means when not invoked from command line.
/// The purpose is to not have dependency on Microsoft.Extensions.Logging specifically in this project.
/// </summary>
public interface ILogger
{
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogCritical(string message);
}
