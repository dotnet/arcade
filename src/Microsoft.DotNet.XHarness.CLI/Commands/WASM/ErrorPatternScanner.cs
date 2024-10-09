using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

public class ErrorPatternScanner
{
    private readonly ILogger _logger;
    private readonly List<string> _errorPatternStrings = new();
    private readonly List<Regex> _errorPatternRegexes = new();
    private readonly bool _empty;

    public ErrorPatternScanner(string patternsFile, ILogger logger)
    {
        _logger = logger;
        if (string.IsNullOrEmpty(patternsFile))
            throw new ArgumentNullException(nameof(patternsFile));

        if (!File.Exists(patternsFile))
            throw new FileNotFoundException(patternsFile);

        foreach (string line in File.ReadAllLines(patternsFile))
        {
            if (line.Trim().Length <= 1)
                continue;

            char type = line[0];
            string pattern = line[1..];

            switch (type)
            {
                case '#':
                    // comment
                    break;

                case '@':
                    _errorPatternStrings.Add(pattern);
                    break;

                case '%':
                    {
                        try
                        {
                            _errorPatternRegexes.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException || ex is ArgumentOutOfRangeException)
                        {
                            _logger.LogWarning($"ErrorPatternScanner: Failed to compile regex error pattern '{pattern}': {ex.Message}");
                        }
                    }
                    break;

                default:
                    _logger.LogWarning($"ErrorPatternScanner: Unknown type prefix '{type}' on line '{line}'. Ignoring.");
                    break;
            }
        }

        _empty = _errorPatternRegexes.Count == 0 && _errorPatternStrings.Count == 0;
    }

    public bool IsError(string line, out string? matchedPattern)
    {
        matchedPattern = null;
        if (_empty)
            return false;

        string? patternString = _errorPatternStrings.FirstOrDefault(pattern => line.Contains(pattern, StringComparison.InvariantCultureIgnoreCase));
        if (patternString != null)
        {
            matchedPattern = patternString;
            return true;
        }

        Regex? matchedRegex = _errorPatternRegexes.FirstOrDefault(regex => regex.IsMatch(line));
        if (matchedRegex != null)
        {
            matchedPattern = matchedRegex.ToString();
            return true;
        }

        return false;
    }
}
