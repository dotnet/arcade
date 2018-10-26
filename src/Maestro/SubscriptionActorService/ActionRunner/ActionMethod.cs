// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace SubscriptionActorService
{
    public class ActionMethod
    {
        public ActionMethod(MethodInfo methodInfo)
        {
            MethodInfo = methodInfo;
            ParameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            Type taskReturnType = methodInfo.ReturnType;
            Type returnType = taskReturnType.GetGenericArguments()[0];
            ResultType = returnType.GetGenericArguments()[0];

            var attr = methodInfo.GetCustomAttribute<ActionMethodAttribute>();
            MessageFormat = attr.Format;
        }

        public string MessageFormat { get; }

        public Type[] ParameterTypes { get; }
        public MethodInfo MethodInfo { get; }
        public Type ResultType { get; }
        public string Name => MethodInfo.Name;

        public object[] DeserializeArguments(string arguments)
        {
            JArray jArray = JArray.Parse(arguments);
            if (jArray.Count != ParameterTypes.Length)
            {
                throw new TargetParameterCountException(
                    $"Method '{MethodInfo.Name}' requires '{ParameterTypes.Length}' arguments.");
            }

            var args = new object[ParameterTypes.Length];
            for (var i = 0; i < ParameterTypes.Length; i++)
            {
                args[i] = jArray[i].ToObject(ParameterTypes[i]);
            }

            return args;
        }
    }
}
