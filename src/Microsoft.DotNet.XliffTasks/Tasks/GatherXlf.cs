// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System;
using System.Collections.Generic;

namespace XliffTasks.Tasks
{
    public sealed class GatherXlf : XlfTask
    {
        [Required]
        public ITaskItem[] Sources { get; set; }

        [Required]
        public string[] Languages { get; set; }

        [Required]
        public string TranslatedOutputDirectory { get; set; }

        [Output]
        public ITaskItem[] Outputs { get; private set; }

        protected override void ExecuteCore()
        {
            int index = 0;
            ITaskItem[] outputs = new ITaskItem[Sources.Length * Languages.Length];
            HashSet<string> outputPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem source in Sources)
            {
                string sourceDocumentPath = source.GetMetadataOrDefault(MetadataKey.SourceDocumentPath, source.ItemSpec);

                foreach (string language in Languages)
                {
                    string xlfPath = XlfTask.GetXlfPath(sourceDocumentPath, language);
                    TaskItem xlf = new(source) { ItemSpec = xlfPath };
                    xlf.SetMetadata(MetadataKey.XlfSource, source.ItemSpec);
                    xlf.SetMetadata(MetadataKey.XlfTranslatedFullPath, GetTranslatedOutputPath(source, language, outputPaths));
                    xlf.SetMetadata(MetadataKey.XlfLanguage, language);
                    outputs[index++] = xlf;
                }
            }

            Release.Assert(index == outputs.Length);
            Outputs = outputs;
        }

        private string GetTranslatedOutputPath(ITaskItem source, string language, HashSet<string> outputPaths)
        {
            bool preserveFileName = string.Equals(source.GetMetadata(MetadataKey.XlfPreserveFileName), "true", StringComparison.OrdinalIgnoreCase);

            string translatedFileName = source.GetMetadata(MetadataKey.XlfTranslatedFilename);
            if (string.IsNullOrEmpty(translatedFileName))
            {
                translatedFileName = Path.GetFileNameWithoutExtension(source.ItemSpec);
                source.SetMetadata(MetadataKey.XlfTranslatedFilename, translatedFileName);
            }

            string sourceDocumentPath = source.GetMetadataOrDefault(MetadataKey.SourceDocumentPath, source.ItemSpec);
            string extension = Path.GetExtension(source.ItemSpec);

            string outputPath = preserveFileName ?
                Path.Combine(TranslatedOutputDirectory, language, $"{translatedFileName}{extension}") :
                Path.Combine(TranslatedOutputDirectory, $"{translatedFileName}.{language}{extension}");

            if (!outputPaths.Add(outputPath))
            {
                throw new BuildErrorException(
                    $"Two or more source files to be translated in the same project are named {Path.GetFileName(sourceDocumentPath)}. " +
                    $"Give them unique names or set unique {MetadataKey.XlfTranslatedFilename} metadata.");
            }

            return outputPath;
        }
    }
}
