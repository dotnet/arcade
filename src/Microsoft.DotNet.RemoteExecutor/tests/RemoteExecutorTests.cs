// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.RemoteExecutor;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.RemoteExecutor.Tests
{
    public class RemoteExecutorTests
    {
        [Fact]
        public void AsyncAction_ThrowException()
        {
            Assert.Throws<RemoteExecutionException>(() =>
                RemoteExecutor.Invoke(async () =>
                {
                    Assert.True(false);
                    await Task.Delay(1);
                }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose()
            );
        }

        [Fact]
        public void AsyncAction()
        {
            RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);
            }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose();
        }

        [Fact]
        public void AsyncFunc_ThrowException()
        {
            Assert.Throws<RemoteExecutionException>(() =>
                RemoteExecutor.Invoke(async () =>
                {
                    Assert.True(false);
                    await Task.Delay(1);
                    return 1;
                }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose()
            );
        }

        [Fact]
        public void AsyncFunc_InvalidReturnCode()
        {
            Assert.Throws<TrueException>(() =>
                RemoteExecutor.Invoke(async () =>
                {
                    await Task.Delay(1);
                    return 1;
                }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose()
            );
        }

        [Fact]
        public void AsyncFunc_NoThrow_ValidReturnCode()
        {
            RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);
                return RemoteExecutor.SuccessExitCode;
            }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose();
        }
    }
}
