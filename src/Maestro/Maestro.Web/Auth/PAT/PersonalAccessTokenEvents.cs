using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Maestro.Web
{
    public class PersonalAccessTokenEvents<TUser>
    {
        public delegate Task<(string tokenHash, TUser user)?> GetTokenHashCallback(HttpContext context, int tokenId);

        public delegate Task<int> NewTokenCallback(HttpContext context, TUser user, string name, string hash);

        public NewTokenCallback NewToken { get; set; }
        public GetTokenHashCallback GetTokenHash { get; set; }
    }
}
