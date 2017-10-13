// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.IO;
using XliffTasks.Model;

namespace XliffTasks.Tasks
{
    public sealed class TranslateSource : XlfTask
    {
        [Required]
        public ITaskItem XlfFile { get; set; }

        protected override void ExecuteCore()
        {
            string sourcePath = XlfFile.GetMetadataOrThrow(MetadataKey.XlfSource);
            string sourceFormat = XlfFile.GetMetadataOrThrow(MetadataKey.XlfSourceFormat);
            string language = XlfFile.GetMetadataOrThrow(MetadataKey.XlfLanguage);
            string translatedFullPath = XlfFile.GetMetadataOrThrow(MetadataKey.XlfTranslatedFullPath);

            TranslatableDocument sourceDocument = LoadSourceDocument(sourcePath, XlfFile.GetMetadata(MetadataKey.XlfSourceFormat));
            XlfDocument xlfDocument = LoadXlfDocument(XlfFile.ItemSpec);
            sourceDocument.Translate(xlfDocument.GetTranslations());

            Directory.CreateDirectory(Path.GetDirectoryName(translatedFullPath));

            sourceDocument.RewriteRelativePathsToAbsolute(Path.GetFullPath(sourcePath));
            sourceDocument.Save(translatedFullPath);
        }
    }
}