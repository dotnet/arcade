using Microsoft.AspNetCore.Authentication.OAuth;

namespace Maestro.Web
{
    public class GitHubAuthenticationOptions : OAuthOptions
    {
        public GitHubAuthenticationOptions()
        {
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            TokenEndpoint = "https://github.com/login/oauth/access_token";
            UserInformationEndpoint = "https://api.github.com/user";
        }
    }
}
