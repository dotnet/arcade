// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MSBuild = Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Linq;

namespace Microsoft.Arcade.Common
{
    public abstract class MSBuildTaskBase : MSBuild.Task
    {
        #region Common Variables
        protected const string AssetsVirtualDir = "assets/";
        #endregion

        private const string ExecuteMethodName = "ExecuteTask";

        // TODO: comment all the things in this class :)  

        public override sealed bool Execute()
        {
            ServiceCollection collection = new();
            ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();
            return InvokeExecute(provider);
        }

        public bool InvokeExecute(ServiceProvider provider)
        {
            return (bool)GetExecuteMethod().Invoke(this, GetExecuteArguments(provider));
        }

        public virtual void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton(Log);
        }

        private ParameterInfo[] GetExecuteParameterTypes()
        {
            return GetType().GetMethod(ExecuteMethodName).GetParameters();
        }

        private object[] GetExecuteArguments(ServiceProvider serviceProvider)
        {
            return GetExecuteParameterTypes().Select(t => serviceProvider.GetRequiredService(t.ParameterType)).ToArray();
        }

        private MethodInfo GetExecuteMethod()
        {
            return GetType().GetMethod(ExecuteMethodName);
        }
    }
}
