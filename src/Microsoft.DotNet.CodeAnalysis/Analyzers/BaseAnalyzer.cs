// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.CodeAnalysis.Analyzers
{
    public abstract class BaseAnalyzer : DiagnosticAnalyzer
    {
        private const string ConfigFileName = @"disabledAnalyzers.config";

        public sealed override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(InitializeAnalyzer);
        }

        private void InitializeAnalyzer(CompilationStartAnalysisContext context)
        {
            var configFile = context.Options.AdditionalFiles.FirstOrDefault(file => file.Path.Contains(ConfigFileName));

            if (configFile != null)
            {
                foreach (var line in configFile.GetText().Lines)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(line.ToString(), GetType().Name))
                    {
                        return;
                    }
                }
            }

            OnCompilationStart(context);
        }

        /// <summary>
        /// This is going to be called only if the analyzer was not disabled
        /// </summary>
        /// <param name="context"></param>
        public abstract void OnCompilationStart(CompilationStartAnalysisContext context);
    }
}
