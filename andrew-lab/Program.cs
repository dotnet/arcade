namespace ScriptGenerator
{
    using System;
    using System.Text;
    using System.Linq;

    internal static class Program
    {
        private struct Argument
        {
            public string LongName;
            public string ShortName;
            public string OutputEnvironmentVariableName;
            public bool Required;

            public Argument(string longName, string shortName, string outputEnvironmentVariableName, bool required)
            {
                this.LongName = longName;
                this.ShortName = shortName;
                this.OutputEnvironmentVariableName = outputEnvironmentVariableName;
                this.Required = required;
            }
        }

        private static void Main(string[] args)
        {
            Argument[] arguments = new Argument[]
            {
                new Argument("runtime-path", "r", "argparser_runtime_path", true),
                new Argument("dotnet-root", "d", "argparser_dotnet_root", false),
                new Argument("global-tools-dir", "g", "argparser_global_tool_dir", false),
            };
            Console.WriteLine(GenerateBashScript(arguments));
        }

        private static string GenerateBashScript(Argument[] arguments)
        {
            string template1 = @"
#!/usr/bin/env bash

usage()
{
  echo ""Usage""
}

";

            string template2 = @"
while [[ $# > 0 ]]; do
  opt=""$(echo ""${1/#--/-}"" | awk '{print tolower($0)}')""
  shift
  case ""$opt"" in
";

            string template3 = @"
    -LONGNAMEPLACEHOLDER|-SHORTNAMEPLACEHOLDER)
      if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '1' ]] ; then
        usage
        exit 1
      fi
      OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=1
      if [[ $# == 0 ]]; then
        usage
        exit 1
      fi
      OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER=$1
      ;;
";
            string template4 = @"
    *)
      usage
      exit 1
      ;;
  esac

  shift
done
";
            string template5 = @"
if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '0' ]] ; then
  usage
  exit 1
fi
";
            StringBuilder sb = new StringBuilder();
            sb.Append(template1);
            foreach (var arg in arguments)
            {
                sb.Append(arg.OutputEnvironmentVariableName);
                sb.AppendLine("_specified=0");
            }
            sb.Append(template2);
            foreach (var arg in arguments)
            {
                sb.Append(template3.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template4);
            foreach (var arg in arguments.Where(a=>a.Required))
            {
                sb.Append(template5.Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            return sb.ToString();
        }
    }
}
