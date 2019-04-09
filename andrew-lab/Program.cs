namespace ScriptGenerator
{
    using System;
    using System.Text;
    using System.Linq;
    using System.Text.RegularExpressions;

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
                new Argument("xunit-rsp", "x", "argparser_xunit_rsp", false),
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
            StringBuilder sb = new StringBuilder();
            sb.Append(BashHeader);
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BashInitializeFlagPerArgument, arg));
            }
            sb.Append(BashBeginParsingLoop);
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BashIdentifyArgumentCasePerArgument, arg));
            }
            sb.Append(BashEndParsingLoop);
            foreach (var arg in arguments.Where(a=>a.Required))
            {
                sb.Append(ReplacePlaceHolder(BashValidateRequiredArgumentPerArgument, arg));
            }
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BashDisplayParsedArgumentValuePerArgument, arg));
            }
            Regex regex = new Regex("(\r\n)+");
            return regex.Replace(sb.ToString(), "\r\n");
        }

        private static string GenerateBatchScript(Argument[] arguments)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(BatchHeader);
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BatchInitializeFlagPerArgument, arg));
            }
            sb.Append(BatchBeginParsingLoop);
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BatchIdentifyArgumentCasePerArgument, arg));
            }
            sb.Append(BatchEndParsingLoop);
            foreach (var arg in arguments.Where(a => a.Required))
            {
                sb.Append(ReplacePlaceHolder(BatchValidateRequiredArgumentPerArgument, arg));
            }
            foreach (var arg in arguments)
            {
                sb.Append(ReplacePlaceHolder(BatchDisplayParsedArgumentValuePerArgument, arg));
            }
            sb.Append(BatchUsageFunction);
            Regex regex = new Regex("(\r\n)+");
            return regex.Replace(sb.ToString(), "\r\n");
        }

        private static string ReplacePlaceHolder(string templateString, Argument arg)
        {
            return templateString.Replace("LONGNAMEPLACEHOLDER", arg.LongName).Replace("SHORTNAMEPLACEHOLDER", arg.ShortName).Replace("OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER", arg.OutputEnvironmentVariableName);
        }
        
        #region Bash 
        private const string BashHeader = @"
#!/usr/bin/env bash

usage()
{
  echo ""Usage""
}

";
        private const string BashInitializeFlagPerArgument = @"
OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=0
";
        private const string BashBeginParsingLoop = @"
while [[ $# > 0 ]]; do
  opt=""$(echo ""${1/#--/-}"" | awk '{print tolower($0)}')""
  shift
  case ""$opt"" in
";

        private const string BashIdentifyArgumentCasePerArgument = @"
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
        private const string BashEndParsingLoop = @"
    *)
      usage
      exit 1
      ;;
  esac

  shift
done
";
        private const string BashValidateRequiredArgumentPerArgument = @"
if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '0' ]] ; then
  usage
  exit 1
fi
";
        private const string BashDisplayParsedArgumentValuePerArgument = @"
if [[ ""$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified"" == '1' ]] ; then
  echo ""The OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER is '$OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER'""
fi
";
        #endregion
        #region Batch

        private const string BatchHeader = @"
@echo off
setlocal EnableDelayedExpansion

";
        private const string BatchInitializeFlagPerArgument = @"
set ""OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified=""
";
        private const string BatchBeginParsingLoop = @"
:argparser
:argparser_start
  if ""%~1"" == """" goto argparser_end
  set ""argparser_currentarg=%~1""
  shift
  set ""argparser_currentarg_prefix=%argparser_currentarg:~0,2%""
  if ""%argparser_currentarg_prefix%"" == ""--"" (
    set ""argparser_currentarg=%argparser_currentarg:~1%""
  )
";
        private const string BatchIdentifyArgumentCasePerArgument = @"
  if /i ""%argparser_currentarg%""==""-SHORTNAMEPLACEHOLDER"" (
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
        private const string BatchEndParsingLoop = @"
  call :usage
  exit /b 0
  :argparser_break
  shift
goto argparser_start
:argparser_end
";
        private const string BatchValidateRequiredArgumentPerArgument = @"
if not defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
    call :usage
    exit /b 0
)
";
        private const string BatchDisplayParsedArgumentValuePerArgument = @"
if defined OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER_specified (
    echo The OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER is '%OUTPUTENVIRONMENTVARIABLENAMEPLACEHOLDER%'
)
";
        private const string BatchUsageFunction = @"
exit /b 0

:usage
echo Usage
exit /b 0
";
        #endregion
    }
}
