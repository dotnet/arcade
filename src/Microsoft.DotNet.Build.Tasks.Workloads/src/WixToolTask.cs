// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Abstract <see cref="ToolTask"/> to invoke a WiX command.
    /// </summary>
    public abstract class WixToolTask : ToolTask
    {
        protected CommandLineBuilder CommandLineBuilder
        {
            get;
        } = new CommandLineBuilder();

        public string Platform
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the collection of preprocessor definitions. Each element represents a single definition. 
        /// For example, -dSomeVar="Hello world" defines a preprocessor variable named SomeVar set to "Hello world".
        /// </summary>
        public ICollection<string> PreprocessorDefinitions
        {
            get;
        } = new List<string>();

        protected override string ToolName => throw new NotImplementedException();

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected WixToolTask(IBuildEngine engine, string wixToolsetPath)
        {
            BuildEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            Utils.CheckNullOrEmpty(nameof(wixToolsetPath), wixToolsetPath);
            ToolPath = wixToolsetPath;
        }

        protected override string GenerateFullPathToTool()
        {
            return ToolPath;
        }
    }
}
