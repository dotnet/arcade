// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Models;
using Microsoft.DotNet.DarcLib.Helpers;
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
            _editorPath = LocalHelpers.GetEditorPath(logger);
            _gitDir = LocalHelpers.GetGitDir(logger);
            _logger = logger;
        }

        public int PopUp(EditorPopUp popUp)
        {
            if (string.IsNullOrEmpty(_editorPath))
            {
                _logger.LogError("Failed to define an editor for the pop ups...");
                return Constants.ErrorCode;
            }

            if (string.IsNullOrEmpty(_gitDir))
            {
                _logger.LogError("Failed to get git's directory...");
                return Constants.ErrorCode;
            }

            int result = Constants.ErrorCode;
            int tries = Constants.MaxPopupTries;

            ParsedCommand parsedCommand = GetParsedCommand(_editorPath);

            try
            {
                string path = Path.Combine(_gitDir, popUp.Path);
                string dirPath = Path.GetDirectoryName(path);

                Directory.CreateDirectory(dirPath);
                File.WriteAllLines(path, popUp.Contents.Select(l => l.Text));

                while (tries-- > 0 && result != Constants.SuccessCode)
                {
                    using (Process process = new Process())
                    {
                        _popUpClosed = false;
                        process.EnableRaisingEvents = true;
                        process.Exited += (sender, e) =>
                        {
                            IList<Line> contents = popUp.OnClose(path);

                            result = popUp.ProcessContents(contents);

                            // If succeeded, delete the temp file, otherwise keep it around
                            // for another popup iteration.
                            if (result == Constants.SuccessCode)
                            {
                                Directory.Delete(dirPath, true);
                            }
                            else if (tries > 0)
                            {
                                _logger.LogError("Inputs were invalid, please try again...");
                            }
                            else
                            {
                                Directory.Delete(dirPath, true);
                                _logger.LogError("Maximum number of tries reached, aborting.");
                            }

                            _popUpClosed = true;
                        };
                        process.StartInfo.FileName = parsedCommand.FileName;
                        process.StartInfo.UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                        process.StartInfo.Arguments = $"{parsedCommand.Arguments} {path}";
                        process.Start();

                        int waitForMilliseconds = 100;
                        while (!_popUpClosed)
                        {
                            Thread.Sleep(waitForMilliseconds);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, $"There was an exception while trying to pop up an editor window.");
                result = Constants.ErrorCode;
            }

            return result;
        }

        public static ParsedCommand GetParsedCommand(string command)
        {
            ParsedCommand parsedCommand = new ParsedCommand();

            // If it's quoted then find the end of the quoted string.
            // If non quoted find a space or the end of the string.
            command = command.Trim();
            if (command.StartsWith("'") || command.StartsWith("\""))
            {
                int start = 1;
                int end = command.IndexOf("'", start);
                if (end == -1)
                {
                    end = command.IndexOf("\"", start);
                    if (end == -1)
                    {
                        // Unterminated quoted string.  Use full command as file name
                        parsedCommand.FileName = command.Substring(1);
                        return parsedCommand;
                    }
                }
                parsedCommand.FileName = command.Substring(start, end - start);
                parsedCommand.Arguments = command.Substring(end + 1);
                return parsedCommand;
            }
            else
            {
                // Find a space after the command name, if there are args, then parse them out,
                // otherwise just return the whole string as the filename.
                int fileNameEnd = command.IndexOf(" ");
                if (fileNameEnd != -1)
                {
                    parsedCommand.FileName = command.Substring(0, fileNameEnd);
                    parsedCommand.Arguments = command.Substring(fileNameEnd);
                }
                else
                {
                    parsedCommand.FileName = command;
                }
                return parsedCommand;
            }
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
