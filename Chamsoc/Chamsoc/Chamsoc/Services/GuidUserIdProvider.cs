using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Chamsoc.Hubs
{
    public class GuidUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Trích xuất UserId từ Claim (nếu bạn đã set UserId là Claim)
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
