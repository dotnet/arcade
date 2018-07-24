using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Maestro.Web
{
    public class ApplicationUser : IdentityUser<int>
    {
        public List<ApplicationUserPersonalAccessToken> PersonalAccessTokens { get; set; }

        [PersonalData]
        public string FullName { get; set; }
    }
}
