// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Arcade.Sdk;

internal static class GlobalJson
{
    private static readonly JsonDocumentOptions s_options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Reads the .NET SDK version that Arcade installs from global.json content: 'sdk.version' if
    /// 'tools.downloadSdkVersion' is true, otherwise 'tools.dotnet'.
    /// Keep in sync with eng/common/tools.ps1 and eng/common/tools.sh.
    /// </summary>
    public static bool TryGetDotNetSdkVersion(string globalJsonContent, string globalJsonPath, out string version, out string error)
    {
        version = null;
        error = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(globalJsonContent, s_options);
        }
        catch (JsonException e)
        {
            error = $"Unable to parse file '{globalJsonPath}': {e.Message}";
            return false;
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (TryGetProperty(root, "tools", out JsonElement tools) &&
                TryGetProperty(tools, "downloadSdkVersion", out JsonElement downloadSdkVersion) &&
                downloadSdkVersion.ValueKind == JsonValueKind.True)
            {
                if (TryGetNonEmptyString(root, "sdk", "version", out version))
                {
                    return true;
                }

                error = $"File '{globalJsonPath}' must specify 'sdk.version' when 'tools.downloadSdkVersion' is true.";
                return false;
            }

            if (TryGetNonEmptyString(root, "tools", "dotnet", out version))
            {
                return true;
            }

            error = $"Unable to determine dotnet version from file '{globalJsonPath}'.";
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
    }

    private static bool TryGetNonEmptyString(JsonElement root, string objectName, string propertyName, out string value)
    {
        value = null;
        if (TryGetProperty(root, objectName, out JsonElement obj) &&
            TryGetProperty(obj, propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
        }

        return !string.IsNullOrEmpty(value);
    }
}
