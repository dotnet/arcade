// See https://aka.ms/new-console-template for more information

using Microsoft.Build.Framework;

public class PackageDiffTask : Microsoft.Build.Utilities.ToolTask
{
    [Required]
    public string BaselinePackage {get; set;} = "";

    [Required]
    public string TestPackage {get; set;} = "";

    protected override string ToolName { get; } = $"PackageDiff" + (System.Environment.OSVersion.Platform == PlatformID.Unix ? "" : ".exe");

    protected override string GenerateFullPathToTool()
    {
        return Path.Combine(typeof(PackageDiffTask).Assembly.Location, ToolName);
    }

    protected override string GenerateCommandLineCommands()
    {
        return $"\"{BaselinePackage}\" \"{TestPackage}\"";
    }
}
