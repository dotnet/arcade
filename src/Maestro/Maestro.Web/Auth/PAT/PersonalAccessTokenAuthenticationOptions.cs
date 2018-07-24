using Microsoft.AspNetCore.Authentication;

namespace Maestro.Web
{
    public class PersonalAccessTokenAuthenticationOptions<TUser> : AuthenticationSchemeOptions
    {
        public new PersonalAccessTokenEvents<TUser> Events
        {
            get => (PersonalAccessTokenEvents<TUser>) base.Events;
            set => base.Events = value;
        }

        public int PasswordSize { get; set; } = 16;

        public string TokenName { get; set; } = "Bearer";
    }
}
