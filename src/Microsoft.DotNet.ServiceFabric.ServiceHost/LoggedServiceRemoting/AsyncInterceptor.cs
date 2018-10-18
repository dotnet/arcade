// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    /// <summary>
    ///     An <see cref="IInterceptor" /> implementation that handles Task returning methods correctly.
    /// </summary>
    public abstract class AsyncInterceptor : IInterceptor
    {
        private static readonly MethodInfo s_interceptAsyncMethod = typeof(AsyncInterceptor).GetTypeInfo()
            .DeclaredMethods.Where(m => m.Name == "InterceptAsync")
            .Where(m => m.GetParameters().Length == 2)
            .First(m => m.GetGenericArguments().Length == 1);

        private static readonly MethodInfo s_makeCallAsyncMethodMethod = typeof(AsyncInterceptor).GetTypeInfo()
            .DeclaredMethods.Where(m => m.Name == "MakeCallAsyncMethod")
            .Where(m => m.GetParameters().Length == 1)
            .First(m => m.GetGenericArguments().Length == 1);

        private static readonly MethodInfo s_interceptMethod = typeof(AsyncInterceptor).GetTypeInfo()
            .DeclaredMethods.Where(m => m.Name == "Intercept")
            .Where(m => m.GetParameters().Length == 2)
            .First(m => m.GetGenericArguments().Length == 1);

        private static readonly MethodInfo s_makeCallMethodMethod = typeof(AsyncInterceptor).GetTypeInfo()
            .DeclaredMethods.Where(m => m.Name == "MakeCallMethod")
            .Where(m => m.GetParameters().Length == 1)
            .First(m => m.GetGenericArguments().Length == 1);

        public virtual void Intercept(IInvocation invocation)
        {
            Type retType = invocation.Method.ReturnType;
            if (retType == typeof(Task))
            {
                invocation.ReturnValue = InterceptAsync(
                    invocation,
                    () =>
                    {
                        Proceed(invocation);
                        return (Task) invocation.ReturnValue;
                    });
            }
            else if (IsTaskOfT(retType, out Type t))
            {
                invocation.ReturnValue = s_interceptAsyncMethod.MakeGenericMethod(t)
                    .Invoke(
                        this,
                        new[]
                        {
                            invocation,
                            s_makeCallAsyncMethodMethod.MakeGenericMethod(t).Invoke(this, new object[] {invocation})
                        });
            }
            else
            {
                invocation.ReturnValue = s_interceptMethod.MakeGenericMethod(retType)
                    .Invoke(
                        this,
                        new[]
                        {
                            invocation,
                            s_makeCallMethodMethod.MakeGenericMethod(retType)
                                .Invoke(this, new object[] {invocation})
                        });
            }
        }

        private static bool IsTaskOfT(Type type, out Type t)
        {
            if (type.IsConstructedGenericType)
            {
                Type td = type.GetGenericTypeDefinition();
                if (td == typeof(Task<>))
                {
                    t = type.GetGenericArguments()[0];
                    return true;
                }
            }

            t = null;
            return false;
        }

        protected virtual void Proceed(IInvocation invocation)
        {
            invocation.Proceed();
        }

        protected abstract Task InterceptAsync(IInvocation invocation, Func<Task> call);


        protected abstract Task<T> InterceptAsync<T>(IInvocation invocation, Func<Task<T>> call);

        private Func<Task<T>> MakeCallAsyncMethod<T>(IInvocation invocation)
        {
            return () =>
            {
                Proceed(invocation);
                return (Task<T>) invocation.ReturnValue;
            };
        }

        protected abstract T Intercept<T>(IInvocation invocation, Func<T> call);

        private Func<T> MakeCallMethod<T>(IInvocation invocation)
        {
            return () =>
            {
                Proceed(invocation);
                return (T) invocation.ReturnValue;
            };
        }
    }
}
