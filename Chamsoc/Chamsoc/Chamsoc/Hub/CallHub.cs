using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Thêm để sử dụng AppDbContext
using Chamsoc.Data;
using Chamsoc.Models;

public class CallHub : Hub
{
    private static readonly List<ConsultationRequest> _consultationRequests = new List<ConsultationRequest>();
    private readonly ILogger<CallHub> _logger;
    private readonly AppDbContext _context; // Thêm AppDbContext để lưu vào database

    public CallHub(ILogger<CallHub> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task CallUser(string targetUserId, object offer, string callerId, string callerName)
    {
        try
        {
            if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(callerId))
            {
                throw new HubException("Invalid user IDs.");
            }

            if (offer == null)
            {
                throw new HubException("Offer cannot be null.");
            }

            await Clients.User(targetUserId).SendAsync("ReceiveCall", callerId, callerName, Context.User?.Identity?.Name, offer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CallUser: {ex.Message}");
            throw new HubException($"Failed to call user: {ex.Message}");
        }
    }

    public async Task RequestConsultation(string caregiverId, string requesterId, string requesterName, string requesterAvatar = null)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(caregiverId) || string.IsNullOrEmpty(requesterId) || string.IsNullOrEmpty(requesterName))
            {
                throw new HubException("Invalid consultation request parameters.");
            }

            // Create and store consultation request
            var request = new ConsultationRequest
            {
                CaregiverId = caregiverId,
                RequesterId = requesterId,
                RequesterName = requesterName,
                RequestTime = DateTime.UtcNow // Use UTC for consistency
            };
            _consultationRequests.Add(request);

            // Create notification for Caregiver
            var caregiverNotification = new Notification
            {
                UserId = caregiverId,
                Message = $"Bạn có cuộc gọi chờ mới từ {requesterName}.\nThời gian: {request.RequestTime:dd/MM/yyyy HH:mm}",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "Call"
            };
            _context.Notifications.Add(caregiverNotification);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Lưu thông báo thành công: {caregiverNotification.Message}");

            // Send notification via SignalR to Caregiver
            await Clients.User(caregiverId).SendAsync("ReceiveNotification", caregiverNotification.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RequestConsultation for caregiverId: {CaregiverId}", caregiverId);
            Console.WriteLine($"Lỗi khi lưu thông báo: {ex.Message}");
            throw new HubException("Failed to request consultation: " + ex.Message);
        }
    }
    public List<ConsultationRequest> GetConsultationRequests(string caregiverId)
    {
        try
        {
            if (string.IsNullOrEmpty(caregiverId))
            {
                throw new HubException("Caregiver ID is required.");
            }

            return _consultationRequests
                .Where(r => r.CaregiverId == caregiverId)
                .OrderByDescending(r => r.RequestTime)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetConsultationRequests for caregiverId: {CaregiverId}", caregiverId);
            throw new HubException("Failed to retrieve consultation requests.");
        }
    }

    public override async Task OnConnectedAsync()
    {
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

    public class ConsultationRequest
    {
        public int Id { get; set; } 
        public string CaregiverId { get; set; }
        public string RequesterId { get; set; }
        public string RequesterName { get; set; }
        public DateTime RequestTime { get; set; }
    }
}