// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.DotNet.RemoteExecutor.Tests
{
    public class RemoteExecutorTests
    {
        [Fact(Skip = "Remote executor is broken in VS test explorer")]
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

        [Fact(Skip = "Remote executor is broken in VS test explorer")]
        public void AsyncAction()
        {
            RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);
            }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose();
        }

        [Fact(Skip = "Remote executor is broken in VS test explorer")]
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

        [Fact(Skip = "Remote executor is broken in VS test explorer")]
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

        [Fact(Skip = "Remote executor is broken in VS test explorer")]
        public void AsyncFunc_NoThrow_ValidReturnCode()
        {
            RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);
                return RemoteExecutor.SuccessExitCode;
            }, new RemoteInvokeOptions { RollForward = "Major" }).Dispose();
        }

        [Fact]
        public void Invoke_SingleStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("abc", stringParam);
            }, "abc").Dispose();
        }

        [Fact]
        public void Invoke_SingleStringWithSpaceParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("abc def", stringParam);
            }, "abc def").Dispose();
        }

        [Fact]
        public void Invoke_SingleEscapedZeroStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("abc\0def", stringParam);
            }, "abc\0def").Dispose();
        }

        [Fact]
        public void Invoke_SingleEscapedNewLineStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("abc\ndef", stringParam);
            }, "abc\ndef").Dispose();
        }

        [Fact]
        public void Invoke_SingleEscapedNoSlashStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("$", stringParam);
            }, "$").Dispose();
        }

        [Fact]
        public void Invoke_SingleEscapedNoCharStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("$\\", stringParam);
            }, "$\\").Dispose();
        }

        [Fact]
        public void Invoke_SingleEscapedUnknownStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Equal("abc$\\def", stringParam);
            }, "abc$\\def").Dispose();
        }

        [Fact]
        public void Invoke_SingleEmptyStringParam_Success()
        {
            RemoteExecutor.Invoke((stringParam) =>
            {
                Assert.Empty(stringParam);
            }, string.Empty).Dispose();
        }

        [Fact]
        public void Invoke_SingleCharParam_Success()
        {
            RemoteExecutor.Invoke((charParam) =>
            {
                Assert.Equal('1', charParam);
            }, '1').Dispose();
        }

        [Fact]
        public void Invoke_SingleEmptyCharParam_Success()
        {
            RemoteExecutor.Invoke((charParam) =>
            {
                Assert.Equal('\0', charParam);
            }, '\0').Dispose();
        }

        [Fact]
        public void Invoke_SingleNewLineCharParam_Success()
        {
            RemoteExecutor.Invoke((charParam) =>
            {
                Assert.Equal('\n', charParam);
            }, '\n').Dispose();
        }

        [Fact]
        public void Invoke_SinglePrimitiveParam_Success()
        {
            RemoteExecutor.Invoke((primitiveParam) =>
            {
                Assert.Equal(1, primitiveParam);
            }, 1).Dispose();
        }

        [Fact]
        public void Invoke_SingleEnumParam_Success()
        {
            RemoteExecutor.Invoke((enumParam) =>
            {
                Assert.Equal(ConsoleColor.Red, enumParam);
            }, ConsoleColor.Red).Dispose();
        }

        [Fact]
        public void Invoke_SingleUnknownEnumParam_Success()
        {
            RemoteExecutor.Invoke((enumParam) =>
            {
                Assert.Equal(ConsoleColor.Black - 1, enumParam);
            }, ConsoleColor.Black - 1).Dispose();
        }

        [Fact]
        public void Invoke_SingleNullParam_Success()
        {
            RemoteExecutor.Invoke((nullParam) =>
            {
                Assert.Null(nullParam);
            }, (object)null).Dispose();
        }

        [Fact]
        public void Invoke_MultipleStringParams_Success()
        {
            RemoteExecutor.Invoke((stringParam1, stringParam2, stringParam3) =>
            {
                Assert.Equal("abc", stringParam1);
                Assert.Equal("", stringParam2);
                Assert.Equal("def", stringParam3);
            }, "abc", "", "def").Dispose();
        }

        [Fact]
        public void Invoke_MultiplePrimitiveParams_Success()
        {
            RemoteExecutor.Invoke((primitiveParam1, primitiveParam2, primitiveParam3) =>
            {
                Assert.Equal(1, primitiveParam1);
                Assert.Equal(2.0, primitiveParam2);
                Assert.Equal('3', primitiveParam3);
            }, 1, 2.0, '3').Dispose();
        }

        [Fact]
        public void Invoke_MultipleNullParams_Success()
        {
            object nullParam = null;
            string nullString = null;
            RemoteExecutor.Invoke((nullParam1, nullParam2) =>
            {
                Assert.Null(nullParam1);
                Assert.Null(nullParam2);
            }, nullParam, nullString).Dispose();
        }

        [Fact]
        public void Invoke_MultipleMixedParams_Success()
        {
            object nullParam = null;
            RemoteExecutor.Invoke((stringParam, primitiveParam, nullParam, enumParam) =>
            {
                Assert.Equal("abc", stringParam);
                Assert.Equal(1, primitiveParam);
                Assert.Null(nullParam);
                Assert.Equal(ConsoleColor.Red, enumParam);
            }, "abc", 1, nullParam, ConsoleColor.Red).Dispose();
        }

        [Fact]
        public void Invoke_NoParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke(() =>
            {
            }).Dispose();
        }

        [Fact]
        public void Invoke_NoParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(() =>
            {
                return 42;
            });
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_NoParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);
            }).Dispose();
        }

        [Fact]
        public void Invoke_NoParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async () =>
            {
                await Task.Delay(1);

                return 42;
            });
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_OneParamNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1) =>
            {
                Assert.Equal("param1", param1);
            }, "param1").Dispose();
        }

        [Fact]
        public void Invoke_OneParamReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1) =>
            {
                Assert.Equal("param1", param1);

                return 42;
            }, "param1");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_OneParamAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1) =>
            {
                Assert.Equal("param1", param1);
                await Task.Delay(1);
            }, "param1").Dispose();
        }

        [Fact]
        public void Invoke_OneParamAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1) =>
            {
                Assert.Equal("param1", param1);
                await Task.Delay(1);

                return 42;
            }, "param1");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_TwoParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
            }, "param1", "param2").Dispose();
        }

        [Fact]
        public void Invoke_TwoParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);

                return 42;
            }, "param1", "param2");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_TwoParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                await Task.Delay(1);
            }, "param1", "param2").Dispose();
        }

        [Fact]
        public void Invoke_TwoParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_ThreeParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2, param3) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
            }, "param1", "param2", "param3").Dispose();
        }

        [Fact]
        public void Invoke_ThreeParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2, param3) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);

                return 42;
            }, "param1", "param2", "param3");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_ThreeParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2, param3) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                await Task.Delay(1);
            }, "param1", "param2", "param3").Dispose();
        }

        [Fact]
        public void Invoke_ThreeParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2, param3) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2", "param3");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_FourParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2, param3, param4) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
            }, "param1", "param2", "param3", "param4").Dispose();
        }

        [Fact]
        public void Invoke_FourParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2, param3, param4) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);

                return 42;
            }, "param1", "param2", "param3", "param4");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_FourParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2, param3, param4) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                await Task.Delay(1);
            }, "param1", "param2", "param3", "param4").Dispose();
        }

        [Fact]
        public void Invoke_FourParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2, param3, param4) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2", "param3", "param4");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_FiveParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2, param3, param4, param5) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
            }, "param1", "param2", "param3", "param4", "param5").Dispose();
        }

        [Fact]
        public void Invoke_FiveParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2, param3, param4, param5) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_FiveParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                await Task.Delay(1);
            }, "param1", "param2", "param3", "param4", "param5").Dispose();
        }

        [Fact]
        public void Invoke_FiveParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_SixParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2, param3, param4, param5, param6) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
            }, "param1", "param2", "param3", "param4", "param5", "param6").Dispose();
        }

        [Fact]
        public void Invoke_SixParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2, param3, param4, param5, param6) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5", "param6");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_SixParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5, param6) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                await Task.Delay(1);
            }, "param1", "param2", "param3", "param4", "param5", "param6").Dispose();
        }

        [Fact]
        public void Invoke_SixParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5, param6) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5", "param6");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_SevenParamsNoReturn_Success()
        {
            RemoteExecutor.Invoke((param1, param2, param3, param4, param5, param6, param7) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                Assert.Equal("param7", param7);
            }, "param1", "param2", "param3", "param4", "param5", "param6", "param7").Dispose();
        }

        [Fact]
        public void Invoke_SevenParamsReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke((param1, param2, param3, param4, param5, param6, param7) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                Assert.Equal("param7", param7);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5", "param6", "param7");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_SevenParamsAsyncNoReturn_Success()
        {
            RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5, param6, param7) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                Assert.Equal("param7", param7);
                await Task.Delay(1);
            }, "param1", "param2", "param3", "param4", "param5", "param6", "param7").Dispose();
        }

        [Fact]
        public void Invoke_SevenParamsAsyncReturn_Success()
        {
            RemoteInvokeHandle handle = RemoteExecutor.Invoke(async (param1, param2, param3, param4, param5, param6, param7) =>
            {
                Assert.Equal("param1", param1);
                Assert.Equal("param2", param2);
                Assert.Equal("param3", param3);
                Assert.Equal("param4", param4);
                Assert.Equal("param5", param5);
                Assert.Equal("param6", param6);
                Assert.Equal("param7", param7);
                await Task.Delay(1);

                return 42;
            }, "param1", "param2", "param3", "param4", "param5", "param6", "param7");
            Assert.Equal(42, handle.ExitCode);
            handle.Dispose();
        }

        [Fact]
        public void Invoke_AssertionFailureInMethod_Rethrows()
        {
            RemoteExecutionException ex = Assert.Throws<RemoteExecutionException>(() => RemoteExecutor.Invoke(() =>
            {
                Assert.False(true);
            }).Dispose());
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public void Invoke_AssertionFailureInAsyncMethod_Rethrows()
        {
            RemoteExecutionException ex = Assert.Throws<RemoteExecutionException>(() => RemoteExecutor.Invoke(async () =>
            {
                Assert.False(true);
                await Task.Delay(1);
            }).Dispose());
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public void Invoke_AssertionFailureInAsyncReturnMethod_Rethrows()
        {
            RemoteExecutionException ex = Assert.Throws<RemoteExecutionException>(() => RemoteExecutor.Invoke(async () =>
            {
                Assert.False(true);
                await Task.Delay(1);

                return 42;
            }).Dispose());
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public void Invoke_InvalidValuePassedToMethod_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => RemoteExecutor.Invoke((objectParam) => { }, new object()));
        }
    }
}
