using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Web
{
    public static class PersonalAccessTokenAuthenticationExtensions
    {
        public static AuthenticationBuilder AddPersonalAccessToken<TUser>(this AuthenticationBuilder builder)
            where TUser : class
        {
            return builder.AddPersonalAccessToken<TUser>(PersonalAccessTokenDefaults.AuthenticationScheme, _ => { });
        }

        public static AuthenticationBuilder AddPersonalAccessToken<TUser>(
            this AuthenticationBuilder builder,
            Action<PersonalAccessTokenAuthenticationOptions<TUser>> configureOptions) where TUser : class
        {
            return builder.AddPersonalAccessToken(PersonalAccessTokenDefaults.AuthenticationScheme, configureOptions);
        }

        public static AuthenticationBuilder AddPersonalAccessToken<TUser>(
            this AuthenticationBuilder builder,
            string authenticationScheme,
            Action<PersonalAccessTokenAuthenticationOptions<TUser>> configureOptions) where TUser : class
        {
            return builder.AddPersonalAccessToken(authenticationScheme, null, configureOptions);
        }

        public static AuthenticationBuilder AddPersonalAccessToken<TUser>(
            this AuthenticationBuilder builder,
            string authenticationScheme,
            string displayName,
            Action<PersonalAccessTokenAuthenticationOptions<TUser>> configureOptions) where TUser : class
        {
            return builder
                .AddScheme<PersonalAccessTokenAuthenticationOptions<TUser>,
                    PersonalAccessTokenAuthenticationHandler<TUser>>(
                    authenticationScheme,
                    displayName,
                    configureOptions);
        }

        public static Task<string> CreatePersonalAccessTokenAsync<TUser>(
            this HttpContext context,
            TUser user,
            string name) where TUser : class
        {
            return CreatePersonalAccessTokenAsync(
                context,
                PersonalAccessTokenDefaults.AuthenticationScheme,
                user,
                name);
        }

        public static async Task<string> CreatePersonalAccessTokenAsync<TUser>(
            this HttpContext context,
            string authenticationScheme,
            TUser user,
            string name) where TUser : class
        {
            var handlerProvider = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            var handler =
                await handlerProvider.GetHandlerAsync(context, authenticationScheme) as
                    PersonalAccessTokenAuthenticationHandler<TUser>;
            if (handler == null)
            {
                throw new InvalidOperationException(
                    "PersonalAccessToken authentication not configured for the given authentication scheme.");
            }

            return await handler.CreateToken(user, name);
        }
    }
}
