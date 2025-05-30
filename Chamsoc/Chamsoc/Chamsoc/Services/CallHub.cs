using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public class CallHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _activeCalls = new();

    public async Task CallUser(string targetUserId, string callerName, string callerAvatar, object offer)
    {
        try
        {
            // Kiểm tra targetUserId có hợp lệ
            if (string.IsNullOrEmpty(targetUserId))
            {
                throw new HubException("Invalid user ID.");
            }

            // Kiểm tra offer không null
            if (offer == null)
            {
                throw new HubException("Offer cannot be null.");
            }

            // Kiểm tra xem người dùng đích có đang trong cuộc gọi không
            if (_activeCalls.ContainsKey(targetUserId))
            {
                throw new HubException("User is busy.");
            }

            // Đánh dấu cuộc gọi đang diễn ra
            _activeCalls.TryAdd(targetUserId, Context.UserIdentifier);
            _activeCalls.TryAdd(Context.UserIdentifier, targetUserId);

            // Gửi cuộc gọi đến người dùng đích
            await Clients.User(targetUserId).SendAsync("ReceiveCall", Context.UserIdentifier, callerName, callerAvatar, offer);
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

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            // Nếu người dùng đang trong cuộc gọi, kết thúc cuộc gọi
            if (_activeCalls.TryRemove(userId, out string targetUserId))
            {
                await Clients.User(targetUserId).SendAsync("CallEnded");
                _activeCalls.TryRemove(targetUserId, out _);
            }
            Console.WriteLine($"User {userId} disconnected. ConnectionId: {Context.ConnectionId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task GetUserConnections()
    {
        var userId = Context.UserIdentifier;
        var connections = Clients.Users(new[] { userId });
        await Clients.Caller.SendAsync("ReceiveConnections", connections);
    }

    public async Task AnswerCall(string callerId, object answer)
    {
        try
        {
            if (!_activeCalls.ContainsKey(callerId))
            {
                throw new HubException("Call no longer exists.");
            }
            await Clients.User(callerId).SendAsync("ReceiveAnswer", answer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AnswerCall: {ex.Message}");
            throw;
        }
    }

    public async Task SendIceCandidate(string targetUserId, object candidate)
    {
        try
        {
            if (!_activeCalls.ContainsKey(targetUserId))
            {
                throw new HubException("Call no longer exists.");
            }
            await Clients.User(targetUserId).SendAsync("ReceiveIceCandidate", candidate);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendIceCandidate: {ex.Message}");
            throw;
        }
    }

    public async Task RejectCall(string callerId)
    {
        try
        {
            if (_activeCalls.TryRemove(callerId, out string targetUserId))
            {
                await Clients.User(callerId).SendAsync("CallRejected");
                _activeCalls.TryRemove(targetUserId, out _);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RejectCall: {ex.Message}");
            throw;
        }
    }

    public async Task EndCall(string targetUserId)
    {
        try
        {
            if (_activeCalls.TryRemove(Context.UserIdentifier, out _))
            {
                await Clients.User(targetUserId).SendAsync("CallEnded");
                _activeCalls.TryRemove(targetUserId, out _);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in EndCall: {ex.Message}");
            throw;
        }
    }

    public async Task SendMicState(string targetUserId, bool isEnabled)
    {
        try
        {
            if (!_activeCalls.ContainsKey(targetUserId))
            {
                throw new HubException("Call no longer exists.");
            }
            await Clients.User(targetUserId).SendAsync("ReceiveMicState", isEnabled);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendMicState: {ex.Message}");
            throw;
        }
    }
}