// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Microsoft.AspNetCore.ApiVersioning
{
    public class RequiredParameterDescriptorProvider : IApiDescriptionProvider
    {
        public void OnProvidersExecuting(ApiDescriptionProviderContext context)
        {
        }

        public void OnProvidersExecuted(ApiDescriptionProviderContext context)
        {
            foreach (ApiDescription desc in context.Results)
            {
                foreach (ApiParameterDescription param in desc.ParameterDescriptions)
                {
                    // Mvc chokes on parameters with [Required] on them. Give it a hand.
                    ControllerParameterDescriptor actionParam = desc.ActionDescriptor.Parameters
                        .OfType<ControllerParameterDescriptor>()
                        .FirstOrDefault(p => p.Name == param.Name);
                    if (actionParam != null)
                    {
                        if (actionParam.ParameterInfo.GetCustomAttributes()
                            .Any(a => a.GetType().Name == "RequiredAttribute"))
                        {
                            param.ModelMetadata = new SetRequiredModelMetadata(param.ModelMetadata);
                        }
                    }
                }
            }
        }

        public int Order { get; } = int.MinValue;
    }
}
