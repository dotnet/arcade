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
            /*
            Argument[] arguments = new Argument[]
            {
                new Argument("configuration", "c", "configuration", true),
            };
            */
            // Console.WriteLine(GenerateBashScript(arguments));
            Console.WriteLine(GenerateBatchScript(arguments));
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
OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=0
";
            string template3 = @"
while [[ $# > 0 ]]; do
  opt=""$(echo ""${1/#--/-}"" | awk '{print tolower($0)}')""
  shift
  case ""$opt"" in
";

            string template4 = @"
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
            string template5 = @"
    *)
      usage
      exit 1
      ;;
  esac

  shift
done
";
            string template6 = @"
if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '0' ]] ; then
  usage
  exit 1
fi
";
            string template7 = @"
if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '1' ]] ; then
  echo ""The OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER is '$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER'""
fi
";
            StringBuilder sb = new StringBuilder();
            sb.Append(template1);
            foreach (var arg in arguments)
            {
                sb.Append(template2.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template3);
            foreach (var arg in arguments)
            {
                sb.Append(template4.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template5);
            foreach (var arg in arguments.Where(a=>a.Required))
            {
                sb.Append(template6.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            foreach (var arg in arguments)
            {
                sb.Append(template7.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            return sb.ToString();
        }

        private static string GenerateBatchScript(Argument[] arguments)
        {
            string template1 = @"
@echo off
setlocal EnableDelayedExpansion

";
            string template2 = @"
set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=""
";
            string template3 = @"
:argparser
:argparser_start
  if ""%~1"" == """" goto argparser_end
  set ""argparser_currentarg=%~1""
  shift
  set ""argparser_currentarg_prefix=%argparser_currentarg:~0,2%""
  IF ""%argparser_currentarg_prefix%"" == ""--"" (
    set ""argparser_currentarg=%argparser_currentarg:~1%""
  )
";
            string template4 = @"
  IF /i ""%argparser_currentarg%""==""-SHORTNAMEPLACEHOLDER"" (
    if defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
        call :usage
        exit /b 0
    )
    if ""%~1"" == """" (
        call :usage
        exit /b 0
    )
    set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=1""
    set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER=%~1""
    goto :argparser_break
  )
  IF /i ""%argparser_currentarg%""==""-LONGNAMEPLACEHOLDER"" (
    if defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
        call :usage
        exit /b 0
    )
    if ""%~1"" == """" (
        call :usage
        exit /b 0
    )
    set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=1""
    set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER=%~1""
    goto :argparser_break
  )
";
            string template5 = @"
  call :usage
  exit /b 0
  :argparser_break
  shift
goto argparser_start
:argparser_end
";
            string template6 = @"
if not defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
    call :usage
    exit /b 0
)
";
            string template7 = @"
if defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
    echo The OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER is '%OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER%'
)
";
            string template8 = @"
exit /b 0

:usage
echo Usage
exit /b 0
";
            StringBuilder sb = new StringBuilder();
            sb.Append(template1);
            foreach (var arg in arguments)
            {
                sb.Append(template2.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template3);
            foreach (var arg in arguments)
            {
                sb.Append(template4.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template5);
            foreach (var arg in arguments.Where(a => a.Required))
            {
                sb.Append(template6.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            foreach (var arg in arguments)
            {
                sb.Append(template7.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName));
            }
            sb.Append(template8);
            return sb.ToString();
        }
    }
}
