using System.Security.Claims;

namespace FUEN42Team3.Frontend.WebApi.Helpers
{
    public static class ClaimsExtensions
    {
        //抽取使用者ID的擴充方法
        public static int? GetCurrentMemberId(this ClaimsPrincipal user)
        {
            var claim = user?.Claims?.FirstOrDefault(c => c.Type == "MemberId");
            if (claim == null) return null;
            return int.TryParse(claim.Value, out var id) ? id : null;
        }
    }
}
