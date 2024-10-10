// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common;

public interface IDiagnosticsData
{
    ExitCode ExitCode { get; set; }

    /// <summary>
    /// Target the user/app specified when executing the command.
    /// </summary>
    string? Target { get; set; }

    /// <summary>
    /// OS version of the actual test target (simulator, device) that was used for the run.
    /// </summary>
    string? TargetOS { get; set; }

    /// <summary>
    /// Name of the used device.
    /// </summary>
    string? Device { get; set; }

    /// <summary>
    /// True when the target is a real HW device, false for simulators, maccatalyst..
    /// </summary>
    bool? IsDevice { get; set; }
}

/// <summary>
/// Class responsible for gathering of diagnostics data and saving them into a file.
/// </summary>
public class CommandDiagnostics : IDiagnosticsData
{
    private readonly ILogger _logger;
    private readonly Stopwatch _timer = Stopwatch.StartNew();

    public string Platform { get; }

    public string Command { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public ExitCode ExitCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetOS { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Device { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDevice { get; set; }

    public int Duration => (int)Math.Round(_timer.Elapsed.TotalSeconds);

    public CommandDiagnostics(ILogger logger, TargetPlatform platform, string command)
    {
        _logger = logger;
        Command = command;
        Platform = platform switch
        {
            TargetPlatform.Android => "android",
            TargetPlatform.Apple => "apple",
            TargetPlatform.WASM => "wasm",
            TargetPlatform.WASI => "wasi",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };
    }

    /// <summary>
    /// Saves the data to a JSON file as an object in a JSON array.
    /// If the file exists already, it is appended at the end of the array.
    /// </summary>
    /// <param name="targetFile">JSON file where to save the data</param>
    public void SaveToJsonFile(string targetFile)
    {
        _timer.Stop();

        var options = new JsonSerializerOptions
        {
#if DEBUG
            WriteIndented = true,
#else
                WriteIndented = false,
#endif
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        _logger.LogDebug("Saving diagnostics data to '{path}'", targetFile);

        try
        {
            // Either append current data to the JSON array or create a new file
            if (File.Exists(targetFile))
            {
                var data = JsonDocument.Parse(File.ReadAllText(targetFile));

                var writerOptions = new JsonWriterOptions
                {
                    Indented = options.WriteIndented,
                };

                using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                using var jsonWriter = new Utf8JsonWriter(fileStream, writerOptions);

                jsonWriter.WriteStartArray();

                // Copy the existing elements without going into details of what they are
                var newData = new List<JsonElement>();
                var enumerator = data.RootElement.EnumerateArray();
                while (enumerator.MoveNext())
                {
                    enumerator.Current.WriteTo(jsonWriter);
                }

                // New element
                JsonSerializer.Serialize(jsonWriter, this, options);

                jsonWriter.WriteEndArray();
            }
            else
            {
                var data = new[]
                {
                        this
                    };

                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(targetFile, json);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to save diagnostics data to '{pathToFile}': {error}", targetFile, e);
        }
    }
}
