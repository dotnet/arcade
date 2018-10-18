// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.ApiVersioning
{
    [PublicAPI]
    public class VersionedApiApplicationModelProvider : DefaultApplicationModelProvider, IApplicationModelProvider
    {
        public VersionedApiApplicationModelProvider(
            IOptions<MvcOptions> mvcOptions,
            IModelMetadataProvider modelMetadataProvider,
            ILogger<VersionedApiApplicationModelProvider> logger,
            VersionedControllerProvider controllerProvider,
            IOptions<ApiVersioningOptions> optionsAccessor) : base(mvcOptions, modelMetadataProvider)
        {
            Logger = logger;
            ControllerProvider = controllerProvider;
            Options = optionsAccessor.Value;
        }

        private ILogger<VersionedApiApplicationModelProvider> Logger { get; }
        private VersionedControllerProvider ControllerProvider { get; }
        private ApiVersioningOptions Options { get; }

        int IApplicationModelProvider.Order { get; } = 1000;

        public override void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            IReadOnlyDictionary<string, IReadOnlyDictionary<string, TypeInfo>> versions = ControllerProvider.Versions;
            List<TypeInfo> controllerTypesToRemove = versions.SelectMany(v => v.Value.Values).ToList();

            var modelsToRemove = new List<ControllerModel>();
            foreach (ControllerModel model in context.Result.Controllers)
            {
                if (controllerTypesToRemove.Any(t => Equals(model.ControllerType, t)))
                {
                    modelsToRemove.Add(model);
                }
            }

            foreach (ControllerModel model in modelsToRemove)
            {
                Logger.LogTrace("Removing: {controllerType}", model.ControllerType.AssemblyQualifiedName);
                context.Result.Controllers.Remove(model);
            }

            foreach (KeyValuePair<string, IReadOnlyDictionary<string, TypeInfo>> version in versions)
            {
                AddVersion(context, version.Key, version.Value);
            }
        }

        protected override bool IsAction(TypeInfo typeInfo, MethodInfo methodInfo)
        {
            if (methodInfo.IsDefined(typeof(ApiRemovedAttribute)))
            {
                return false;
            }

            return base.IsAction(typeInfo, methodInfo);
        }

        private void AddVersion(
            ApplicationModelProviderContext context,
            string version,
            IReadOnlyDictionary<string, TypeInfo> controllers)
        {
            foreach (KeyValuePair<string, TypeInfo> controller in controllers)
            {
                ControllerModel controllerModel = CreateControllerModel(controller.Value);
                controllerModel.ControllerName = $"{controller.Key}/{version}";
                controllerModel.RouteValues.Add("version", version);
                if (controllerModel.Selectors.Count > 1)
                {
                    throw new InvalidOperationException("Versioned Controllers cannot have more than one route.");
                }

                SelectorModel selector = controllerModel.Selectors.Count == 1
                    ? controllerModel.Selectors[0]
                    : GetDefaultControllerSelector(controller);
                if (selector.AttributeRouteModel.IsAbsoluteTemplate)
                {
                    throw new InvalidOperationException(
                        "versioned api controllers are not allowed to have absolute routes.");
                }

                controllerModel.Selectors.Clear();
                Options.VersioningScheme.Apply(selector, version);
                controllerModel.Selectors.Add(selector);

                context.Result.Controllers.Add(controllerModel);
                controllerModel.Application = context.Result;

                foreach (MethodInfo methodInfo in controller.Value.AsType().GetMethods())
                {
                    ActionModel actionModel = CreateActionModel(controller.Value, methodInfo);
                    if (actionModel == null)
                    {
                        continue;
                    }

                    actionModel.Controller = controllerModel;
                    controllerModel.Actions.Add(actionModel);

                    foreach (ParameterInfo parameter in actionModel.ActionMethod.GetParameters())
                    {
                        ParameterModel parameterModel = CreateParameterModel(parameter);
                        parameterModel.Action = actionModel;
                        actionModel.Parameters.Add(parameterModel);
                    }
                }
            }
        }

        private SelectorModel GetDefaultControllerSelector(KeyValuePair<string, TypeInfo> controller)
        {
            return new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(controller.Key))
            };
        }
    }
}
