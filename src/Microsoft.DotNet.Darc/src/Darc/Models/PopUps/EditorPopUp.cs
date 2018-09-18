using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DotNet.Darc.Models
{
    abstract public class EditorPopUp
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

        public List<Line> GetContentValues(IEnumerable<string> contents)
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

        abstract public List<Line> Parse(bool isComment);

        abstract public bool Validate();

        abstract public int OnClose(string path);
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
