using Microsoft.DotNet.Darc.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.DotNet.Darc.Helpers
{
    public class UxManager
    {
        private readonly string _editorPath;
        private readonly string _gitDir;
        private readonly ILogger _logger;
        private bool _popUpClosed = false;

        public UxManager(ILogger logger)
        {
            _editorPath = GetEditorPath();
            _gitDir = GetGitDir();
            _logger = logger;
        }

        public int PopUp(EditorPopUp popUp)
        {
            if (string.IsNullOrEmpty(_editorPath))
            {
                _logger.LogError("Filed to define an editor for the pop ups...");
                return -1;
            }

            if (string.IsNullOrEmpty(_gitDir))
            {
                _logger.LogError("Filed to get git's directory...");
                return -1;
            }

            int result = 0;

            ParsedCommand parsedCommand = GetParsedCommand(_editorPath);

            try
            {
                string path = Path.Join(_gitDir, popUp.Path);
                string dirPath = Path.GetDirectoryName(path);

                Directory.CreateDirectory(dirPath);
                File.WriteAllLines(path, popUp.Contents.Select(l => l.Text));

                using (Process process = new Process())
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += (sender, e) =>
                    {
                        IList<Line> contents = popUp.OnClose(path);

                        result = popUp.Execute(contents);

                        Directory.Delete(dirPath, true);

                        _popUpClosed = true;
                    };
                    process.StartInfo.FileName = parsedCommand.FileName;
                    process.StartInfo.Arguments = $"{parsedCommand.Arguments} {path}";
                    process.Start();

                    int waitForMilliseconds = 100;
                    while (!_popUpClosed)
                    {
                        Thread.Sleep(waitForMilliseconds);
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.LogError($"There was an excpetion while trying to pop up an editor window. Exception: {exc.Message}");
                result = -1;
            }

            return result;
        }

        private string GetEditorPath()
        {
            string editor = ExecuteCommand("git.exe", "config --get core.editor");

            // If there is nothing set in core.editor we try to default it to notepad if running in Windows, if not default it to
            // vim
            if (string.IsNullOrEmpty(editor))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    editor = ExecuteCommand("where", "notepad");
                }
                else
                {
                    editor = ExecuteCommand("which", "vim");
                }
            }

            return editor;
        }

        private string GetGitDir()
        {
            string dir = ExecuteCommand("git.exe", "rev-parse --absolute-git-dir");

            if (string.IsNullOrEmpty(dir))
            {
                dir = Constants.DarcDirectory;

                Directory.CreateDirectory(dir);
            }

            return dir;
        }

        private string ExecuteCommand(string fileName, string arguments)
        {
            string output = null;

            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    FileName = fileName,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                using (Process process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.StartInfo.Arguments = arguments;
                    process.Start();

                    output = process.StandardOutput.ReadToEnd().Trim();

                    process.WaitForExit();
                }

                if (string.IsNullOrEmpty(output))
                {
                    _logger.LogError($"There was an error while running git.exe {arguments}");
                }

                string[] paths = output.Split(Environment.NewLine);

                output = paths[0];
            }
            catch (Exception exc)
            {
                _logger.LogWarning($"Something failed while trying to execute '{fileName} {arguments}'. Exception: {exc.Message}");
            }

            return output;
        }

        private ParsedCommand GetParsedCommand(string command)
        {
            ParsedCommand parsedCommand = new ParsedCommand();

            if (command.Contains("'"))
            {
                int start = command.IndexOf("'") + 1;
                int end = command.LastIndexOf("'");
                parsedCommand.FileName = command.Substring(start, end - start);
                parsedCommand.Arguments = command.Substring(end + 1);
            }
            else
            {
                int fileNameEnd = command.IndexOf(" ");
                parsedCommand.FileName = command.Substring(0, fileNameEnd);
                parsedCommand.Arguments = command.Substring(fileNameEnd);
            }

            return parsedCommand;
        }
    }

    /// <summary>
    /// Process needs the file name and the arguments splitted apart. This represent these two.
    /// </summary>
    public class ParsedCommand
    {
        public string FileName { get; set; }

        public string Arguments { get; set; }
    }
}
