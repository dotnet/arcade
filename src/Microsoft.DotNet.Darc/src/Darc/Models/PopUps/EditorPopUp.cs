// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Darc.Models
{
    public abstract class EditorPopUp
    {
        public EditorPopUp(string path, IList<Line> contents)
        {
            Path = path;
            Contents = contents;
        }

        [JsonIgnore]
        public string Path { get; set; }

        [JsonIgnore]
        public IList<Line> Contents { get; set; }

        public IList<Line> OnClose(string path)
        {
            string[] updatedFileContents = File.ReadAllLines(path);
            return GetContentValues(updatedFileContents);
        }

        public abstract bool Validate();

        public abstract int ProcessContents(IList<Line> contents);

        private List<Line> GetContentValues(IEnumerable<string> contents)
        {
            List<Line> values = new List<Line>();

            foreach (string content in contents)
            {
                if (!content.Contains("#") && !string.IsNullOrEmpty(content))
                {
                    values.Add(new Line(content));
                }
            }

            return values;
        }
    }

    public class Line
    {
        public Line(string text, bool isComment = false)
        {
            Text = !isComment ? text : $"# {text}";
        }

        public string Text { get; set; }
    }
}
