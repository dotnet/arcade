// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

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
            ImmutableDictionary<string, ActionMethod>.Builder methods =
                ImmutableDictionary.CreateBuilder<string, ActionMethod>();
            foreach (MethodInfo method in GetAllMethods(type))
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
                foreach (MethodInfo method in type.GetTypeInfo().DeclaredMethods)
                {
                    yield return method;
                }

                type = type.BaseType;
            }
        }
    }
}
