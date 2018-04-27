// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
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

                var untranslatedResourceSet = new SortedSet<string>(StringComparer.Ordinal);

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

                    untranslatedResourceSet.UnionWith(xlfDocument.GetUntranslatedResourceIDs());
                }

                if (untranslatedResourceSet.Count > 0)
                {
                    string untranslatedResourceNames = string.Join(Environment.NewLine, untranslatedResourceSet);
                    Log.LogError($"File '{sourceDocumentPath}' has {untranslatedResourceSet.Count} untranslated resource(s):{Environment.NewLine}{untranslatedResourceNames}");
                }
            }
        }
    }
}
