namespace Microsoft.DotNet.DarcLib
{
    public class PullRequestProperties
    {
        public const string TitleTag = "[Darc-Update]";
        public const string Description = "Darc is trying to update these files to the latest versions found in the Product Dependency Store";
        public static readonly string Title = $"{TitleTag} global.json, version.props and version.details.xml";
    }
}
