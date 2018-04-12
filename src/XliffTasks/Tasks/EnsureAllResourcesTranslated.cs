// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.IO;
using XliffTasks.Model;

namespace XliffTasks.Tasks
{
    public sealed class EnsureAllResourcesTranslated : XlfTask
    {
        [Required]
        public ITaskItem[] Sources { get; set; }

        [Required]
        public string[] Languages { get; set; }

        protected override void ExecuteCore()
        {
            foreach (var item in Sources)
            {
                string sourceDocumentPath = item.GetMetadataOrDefault(MetadataKey.SourceDocumentPath, item.ItemSpec);

                foreach (var language in Languages)
                {
                    string xlfPath = GetXlfPath(sourceDocumentPath, language);
                    XlfDocument xlfDocument;

                    try
                    {
                        xlfDocument = LoadXlfDocument(xlfPath);
                    }
                    catch (FileNotFoundException)
                    {
                        // If the file doesn't exist, we don't need to worry about it having
                        // untranslated resources.
                        continue;
                    }

                    int untranslatedResourceCount = xlfDocument.GetUntranslatedResourceCount();
                    if (untranslatedResourceCount > 0)
                    {
                        Log.LogError($"Xliff file '{xlfPath}' has {untranslatedResourceCount} untranslated resources.");
                    }
                }
            }
        }
    }
}
