// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maestro.Web
{
    public class PersonalAccessTokenAuthenticationHandler<TUser> :
        AuthenticationHandler<PersonalAccessTokenAuthenticationOptions<TUser>> where TUser : class
    {
        public PersonalAccessTokenAuthenticationHandler(
            IOptionsMonitor<PersonalAccessTokenAuthenticationOptions<TUser>> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IPasswordHasher<TUser> passwordHasher,
            SignInManager<TUser> signInManager) : base(options, logger, encoder, clock)
        {
            PasswordHasher = passwordHasher;
            SignInManager = signInManager;
        }

        public IPasswordHasher<TUser> PasswordHasher { get; }
        public SignInManager<TUser> SignInManager { get; }

        public new PersonalAccessTokenEvents<TUser> Events
        {
            get => (PersonalAccessTokenEvents<TUser>) base.Events;
            set => base.Events = value;
        }

        public int TokenIdByteCount => sizeof(int);

        public int TokenByteCount => TokenIdByteCount + Options.PasswordSize;

        protected override Task<object> CreateEventsAsync()
        {
            return Task.FromResult<object>(new PersonalAccessTokenEvents<TUser>());
        }

        private byte[] GeneratePassword()
        {
            var bytes = new byte[Options.PasswordSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return bytes;
        }

        private string EncodeToken(int tokenId, byte[] password)
        {
            byte[] tokenIdBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(tokenId));
            byte[] outputBytes = tokenIdBytes.Concat(password).ToArray();
            return WebEncoders.Base64UrlEncode(outputBytes);
        }

        private (int tokenId, string password)? DecodeToken(string input)
        {
            byte[] tokenBytes = WebEncoders.Base64UrlDecode(input);
            if (tokenBytes.Length != TokenByteCount)
            {
                return null;
            }

            int tokenId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(tokenBytes, 0));
            string password = WebEncoders.Base64UrlEncode(tokenBytes, TokenIdByteCount, Options.PasswordSize);
            return (tokenId, password);
        }

        public async Task<(int id, string value)> CreateToken(TUser user, string name)
        {
            byte[] passwordBytes = GeneratePassword();
            string password = WebEncoders.Base64UrlEncode(passwordBytes);
            string hash = PasswordHasher.HashPassword(user, password);
            var context = new SetTokenHashContext<TUser>(Context, user, name, hash);
            int tokenId = await Events.SetTokenHash(context);
            return (tokenId, EncodeToken(tokenId, passwordBytes));
        }

        public async Task<TUser> VerifyToken(string token)
        {
            (int tokenId, string password)? decoded = DecodeToken(token);
            if (!decoded.HasValue)
            {
                return null;
            }

            (int tokenId, string password) = decoded.Value;

            var context = new GetTokenHashContext<TUser>(Context, tokenId);
            await Events.GetTokenHash(context);
            if (!context.Succeeded)
            {
                return null;
            }

            string hash = context.Hash;
            TUser user = context.User;

            PasswordVerificationResult result = PasswordHasher.VerifyHashedPassword(user, hash, password);

            if (result == PasswordVerificationResult.Success ||
                result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                return user;
            }

            return null;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                string token = GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return AuthenticateResult.NoResult();
                }

                TUser user = await VerifyToken(token);

                if (user == null)
                {
                    return AuthenticateResult.Fail("Invalid Token");
                }

                ClaimsPrincipal principal = await SignInManager.CreateUserPrincipalAsync(user);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                var context = new PersonalAccessTokenValidatePrincipalContext<TUser>(
                    Context,
                    Scheme,
                    Options,
                    ticket,
                    user);
                await Events.ValidatePrincipal(context);
                if (context.Principal == null)
                {
                    return AuthenticateResult.Fail("No principal.");
                }

                return AuthenticateResult.Success(
                    new AuthenticationTicket(context.Principal, context.Properties, Scheme.Name));
            }
            catch (Exception ex)
            {
                return AuthenticateResult.Fail(ex);
            }
        }

        private string GetToken()
        {
            string authorization = Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authorization))
            {
                return null;
            }

            string authPrefix = Options.TokenName + " ";

            if (authorization.StartsWith(authPrefix))
            {
                return authorization.Substring(authPrefix.Length).Trim();
            }

            return null;
        }
    }
}
