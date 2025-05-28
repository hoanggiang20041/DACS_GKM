using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class CallHub : Hub
{
    public async Task CallUser(string targetUserId, object offer, string callerId, string callerName)
    {
        try
        {
            // Kiểm tra targetUserId có hợp lệ
            if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(callerId))
            {
                throw new HubException("Invalid user IDs.");
            }

            // Kiểm tra offer không null
            if (offer == null)
            {
                throw new HubException("Offer cannot be null.");
            }

            // Kiểm tra xem targetUserId có đang kết nối
            // SignalR tự động ánh xạ userId tới connectionId thông qua UserIdentifier
            await Clients.User(targetUserId).SendAsync("ReceiveCall", callerId, callerName, Context.User?.Identity?.Name, offer);
        }
        catch (Exception ex)
        {
            // Log lỗi chi tiết
            Console.WriteLine($"Error in CallUser: {ex.Message}");
            throw new HubException($"Failed to call user: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        // Gán UserId cho kết nối
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            Console.WriteLine($"User {userId} connected with ConnectionId: {Context.ConnectionId}");
        }
        await base.OnConnectedAsync();
    }

    public async Task GetUserConnections()
    {
        var userId = Context.UserIdentifier;
        var connections = Clients.Users(new[] { userId });
        await Clients.Caller.SendAsync("ReceiveConnections", connections);
    }

    public async Task AnswerCall(string callerId, object answer)
    {
        await Clients.User(callerId).SendAsync("ReceiveAnswer", answer);
    }

    public async Task SendIceCandidate(string targetUserId, object candidate)
    {
        await Clients.User(targetUserId).SendAsync("ReceiveIceCandidate", candidate);
    }

    public async Task RejectCall(string callerId)
    {
        await Clients.User(callerId).SendAsync("CallRejected");
    }

    public async Task EndCall(string targetUserId)
    {
        await Clients.User(targetUserId).SendAsync("CallEnded");
    }
}