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
    public class ActionMethod
    {
        public ActionMethod(MethodInfo methodInfo)
        {
            MethodInfo = methodInfo;
            ParameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            var taskReturnType = methodInfo.ReturnType;
            var returnType = taskReturnType.GetGenericArguments()[0];
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
            var jArray = JArray.Parse(arguments);
            if (jArray.Count != ParameterTypes.Length)
            {
                throw new TargetParameterCountException(
                    $"Method '{MethodInfo.Name}' requires '{ParameterTypes.Length}' arguments.");
            }

            var args = new object[ParameterTypes.Length];
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                args[i] = jArray[i].ToObject(ParameterTypes[i]);
            }

            return args;
        }
    }
}
