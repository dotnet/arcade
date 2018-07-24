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
    public class
        PersonalAccessTokenAuthenticationHandler<TUser> : AuthenticationHandler<
            PersonalAccessTokenAuthenticationOptions<TUser>> where TUser : class
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
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
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

        public async Task<string> CreateToken(TUser user, string name)
        {
            byte[] passwordBytes = GeneratePassword();
            string password = WebEncoders.Base64UrlEncode(passwordBytes);
            string hash = PasswordHasher.HashPassword(user, password);
            int tokenId = await Events.NewToken(Context, user, name, hash);
            return EncodeToken(tokenId, passwordBytes);
        }

        public async Task<TUser> VerifyToken(string token)
        {
            (int tokenId, string password)? decoded = DecodeToken(token);
            if (!decoded.HasValue)
            {
                return null;
            }

            (int tokenId, string password) = decoded.Value;

            (string tokenHash, TUser user)? hashUser = await Events.GetTokenHash(Context, tokenId);
            if (!hashUser.HasValue)
            {
                return null;
            }

            (string hash, TUser user) = hashUser.Value;

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
                return AuthenticateResult.Success(ticket);
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
