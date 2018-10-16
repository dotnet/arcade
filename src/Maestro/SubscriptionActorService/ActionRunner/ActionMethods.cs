using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SubscriptionActorService
{

    public static class ActionMethods
    {
        public static ConditionalWeakTable<Type, IImmutableDictionary<string, ActionMethod>> _cache =
            new ConditionalWeakTable<Type, IImmutableDictionary<string, ActionMethod>>();

        public static IImmutableDictionary<string, ActionMethod> Get<T>()
        {
            return _cache.GetValue(typeof(T), GetActionMethods);
        }

        public static IImmutableDictionary<string, ActionMethod> Get(Type type)
        {
            return _cache.GetValue(type, GetActionMethods);
        }

        private static IImmutableDictionary<string, ActionMethod> GetActionMethods(Type type)
        {
            var methods = ImmutableDictionary.CreateBuilder<string, ActionMethod>();
            foreach (var method in GetAllMethods(type))
            {
                var attr = method.GetCustomAttribute<ActionMethodAttribute>();
                if (attr != null)
                {
                    methods.Add(method.Name, new ActionMethod(method));
                }
            }

            return methods.ToImmutable();
        }

        private static IEnumerable<MethodInfo> GetAllMethods(Type type)
        {
            while (type != null && type != typeof(object))
            {
                foreach (var method in type.GetTypeInfo().DeclaredMethods)
                {
                    yield return method;
                }
                type = type.BaseType;
            }
        }
    }
}
