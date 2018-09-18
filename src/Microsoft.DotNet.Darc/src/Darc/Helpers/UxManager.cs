using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.DotNet.Darc.Models;
using Microsoft.Extensions.Logging;

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
            if (string.IsNullOrEmpty(_editorPath) || string.IsNullOrEmpty(_gitDir))
            {
                return -1;
            }

            int result = 0;

            Regex editorRegex = new Regex("'(?<editor>.*)'(?<args>.*)");

            MatchCollection matches = editorRegex.Matches(_editorPath);

            if (matches.Count > 0)
            {
                try
                {
                    string editor = matches[0].Groups["editor"].Value;
                    string args = matches[0].Groups["args"].Value;
                    string path = Path.Join(_gitDir, popUp.Path);
                    string dirPath = Path.GetDirectoryName(path);

                    Directory.CreateDirectory(dirPath);
                    File.WriteAllLines(path, popUp.Contents.Select(l => l.Text));

                    using (Process process = new Process())
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited += (sender, e) =>
                        {
                            result = popUp.OnClose(path);
                            Directory.Delete(dirPath, true);
                            _popUpClosed = true;
                        };
                        process.StartInfo.FileName = editor;
                        process.StartInfo.Arguments = $"{args} {path}";
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
            }
            else
            {
                _logger.LogError("Failed to parse the 'core.editor' value from the git config");
                result = -1;
            }

            return result;
        }

        private string GetEditorPath()
        {
            return ExecuteLocalGitCommand("config --get core.editor");
        }

        private string GetGitDir()
        {
            return ExecuteLocalGitCommand("rev-parse --absolute-git-dir");
        }

        private string ExecuteLocalGitCommand(string arguments)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = "git.exe",
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            string output = null;

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

            return output;
        }
    }
}
