// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.RemoteExecutor
{
    internal static class PlatformDetection
    {
        private static int s_isInAppContainer = -1;

        public static bool IsInAppContainer
        {
            // This actually checks whether code is running in a modern app. 
            // Currently this is the only situation where we run in app container.
            // If we want to distinguish the two cases in future,
            // EnvironmentHelpers.IsAppContainerProcess in desktop code shows how to check for the AC token.
            get
            {
                if (s_isInAppContainer != -1)
                    return s_isInAppContainer == 1;

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                    Environment.OSVersion.Version < new Version(6, 2))
                {
                    s_isInAppContainer = 0;
                    return false;
                }

                byte[] buffer = Array.Empty<byte>();
                uint bufferSize = 0;
                try
                {
                    int result = GetCurrentApplicationUserModelId(ref bufferSize, buffer);
                    switch (result)
                    {
                        case 15703: // APPMODEL_ERROR_NO_APPLICATION
                            s_isInAppContainer = 0;
                            break;
                        case 0:     // ERROR_SUCCESS
                        case 122:   // ERROR_INSUFFICIENT_BUFFER
                                    // Success is actually insufficent buffer as we're really only looking for
                                    // not NO_APPLICATION and we're not actually giving a buffer here. The
                                    // API will always return NO_APPLICATION if we're not running under a
                                    // WinRT process, no matter what size the buffer is.
                            s_isInAppContainer = 1;
                            break;
                        default:
                            throw new InvalidOperationException($"Failed to get AppId, result was {result}.");
                    }
                }
                catch (Exception e)
                {
                    // We could catch this here, being friendly with older portable surface area should we
                    // desire to use this method elsewhere.
                    if (e.GetType().FullName.Equals("System.EntryPointNotFoundException", StringComparison.Ordinal))
                    {
                        // API doesn't exist, likely pre Win8
                        s_isInAppContainer = 0;
                    }
                    else
                    {
                        throw;
                    }
                }

                return s_isInAppContainer == 1;
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern int GetCurrentApplicationUserModelId(ref uint applicationUserModelIdLength, byte[] applicationUserModelId);
    }
}