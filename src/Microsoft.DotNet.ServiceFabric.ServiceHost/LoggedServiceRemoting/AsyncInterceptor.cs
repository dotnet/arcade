using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    /// <summary>
    /// An <see cref="IInterceptor"/> implementation that handles Task returning methods correctly.
    /// </summary>
    public abstract class AsyncInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            var retType = invocation.Method.ReturnType;
            if (retType == typeof(Task))
            {
                invocation.ReturnValue = InterceptAsync(invocation, () =>
                {
                    invocation.Proceed();
                    return (Task)invocation.ReturnValue;
                });
            }
            else if (IsTaskOfT(retType, out Type t))
            {
                invocation.ReturnValue = s_interceptAsyncMethod.MakeGenericMethod(t).Invoke(this, new[]
                {
                    invocation,
                    s_makeCallAsyncMethodMethod.MakeGenericMethod(t).Invoke(null, new object[]
                    {
                        invocation
                    })
                });
            }
            else
            {
                invocation.ReturnValue = s_interceptMethod.MakeGenericMethod(retType).Invoke(this, new[]
                {
                    invocation,
                    s_makeCallMethodMethod.MakeGenericMethod(retType).Invoke(null, new object[]
                    {
                        invocation
                    })
                });
            }
        }

        private static bool IsTaskOfT(Type type, out Type t)
        {
            if (type.IsConstructedGenericType)
            {
                var td = type.GetGenericTypeDefinition();
                if (td == typeof(Task<>))
                {
                    t = type.GetGenericArguments()[0];
                    return true;
                }
            }
            t = null;
            return false;
        }

        protected abstract Task InterceptAsync(IInvocation invocation, Func<Task> call);

        private static readonly MethodInfo s_interceptAsyncMethod = typeof(AsyncInterceptor)
            .GetTypeInfo().DeclaredMethods
            .Where(m => m.Name == "InterceptAsync")
            .Where(m => m.GetParameters().Length == 2)
            .First(m => m.GetGenericArguments().Length == 1);


        protected abstract Task<T> InterceptAsync<T>(IInvocation invocation, Func<Task<T>> call);

        private static readonly MethodInfo s_makeCallAsyncMethodMethod = typeof(AsyncInterceptor)
            .GetTypeInfo().DeclaredMethods
            .Where(m => m.Name == "MakeCallAsyncMethod")
            .Where(m => m.GetParameters().Length == 1)
            .First(m => m.GetGenericArguments().Length == 1);

        private static Func<Task<T>> MakeCallAsyncMethod<T>(IInvocation invocation)
        {
            return () =>
            {
                invocation.Proceed();
                return (Task<T>)invocation.ReturnValue;
            };
        }

        private static readonly MethodInfo s_interceptMethod = typeof(AsyncInterceptor)
            .GetTypeInfo().DeclaredMethods
            .Where(m => m.Name == "Intercept")
            .Where(m => m.GetParameters().Length == 2)
            .First(m => m.GetGenericArguments().Length == 1);

        protected abstract T Intercept<T>(IInvocation invocation, Func<T> call);

        private static readonly MethodInfo s_makeCallMethodMethod = typeof(AsyncInterceptor)
            .GetTypeInfo().DeclaredMethods
            .Where(m => m.Name == "MakeCallMethod")
            .Where(m => m.GetParameters().Length == 1)
            .First(m => m.GetGenericArguments().Length == 1);

        private static Func<T> MakeCallMethod<T>(IInvocation invocation)
        {
            return () =>
            {
                invocation.Proceed();
                return (T)invocation.ReturnValue;
            };
        }
    }
}
