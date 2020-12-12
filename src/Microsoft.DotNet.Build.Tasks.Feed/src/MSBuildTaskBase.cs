// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MSBuild = Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class MSBuildTaskBase : MSBuild.Task
    {
        private const string ExecuteMethodName = "ExecuteTask";
        protected IServiceCollection services = new ServiceCollection();

        public override sealed bool Execute()
        {
            // create the DI collection and build the provider based on the parameters
            ServiceCollection collection = new();
            ConfigureServices(collection);
            using var provider = services.BuildServiceProvider();
            return (bool)GetExecuteMethod().Invoke(this, GetExecuteArguments(provider));
        }

        public ParameterInfo[] GetExecuteParameterTypes()
        {
            return GetType().GetMethod(ExecuteMethodName).GetParameters();
        }

        public object[] GetExecuteArguments(ServiceProvider serviceProvider)
        {
            return GetExecuteParameterTypes().Select(t => serviceProvider.GetRequiredService(t.ParameterType)).ToArray();
        }

        private MethodInfo GetExecuteMethod()
        {
            return GetType().GetMethod(ExecuteMethodName);
        }

        public abstract void ConfigureServices(IServiceCollection collection);

        public const string AssetsVirtualDir = "assets/";
    }
}
