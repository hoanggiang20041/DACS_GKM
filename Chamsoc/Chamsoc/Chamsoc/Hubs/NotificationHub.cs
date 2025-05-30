using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Chamsoc.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var connectionId = Context.ConnectionId;
                await Groups.AddToGroupAsync(connectionId, connectionId);
                _logger.LogInformation($"User connected with connection ID: {connectionId}");
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnConnectedAsync: {ex.Message}");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var connectionId = Context.ConnectionId;
                await Groups.RemoveFromGroupAsync(connectionId, connectionId);
                _logger.LogInformation($"User disconnected. Reason: {exception?.Message ?? "Normal disconnect"}");
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnDisconnectedAsync: {ex.Message}");
                throw;
            }
        }

        public async Task SendNotification(string userId, object notification)
        {
            try
            {
                _logger.LogInformation($"Sending notification to user {userId}");
                await Clients.User(userId).SendAsync("ReceiveNotification", notification);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending notification: {ex.Message}");
                throw;
            }
        }

        // Xử lý yêu cầu cuộc gọi
        public async Task RequestCall(string targetUserId)
        {
            try
            {
                var callerId = Context.ConnectionId;
                _logger.LogInformation($"Call request from {callerId} to {targetUserId}");

                if (string.IsNullOrEmpty(targetUserId))
                {
                    _logger.LogError("Invalid target user ID");
                    throw new ArgumentException("Invalid target user ID");
                }

                await Clients.User(targetUserId).SendAsync("ReceiveCallRequest", callerId, "Người gọi");
                _logger.LogInformation($"Call request sent to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling call request: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý chấp nhận cuộc gọi
        public async Task AcceptCall(string callerId)
        {
            try
            {
                var userId = Context.ConnectionId;
                _logger.LogInformation($"Call accepted by {userId} from {callerId}");

                if (string.IsNullOrEmpty(callerId))
                {
                    _logger.LogError("Invalid caller ID");
                    throw new ArgumentException("Invalid caller ID");
                }

                await Clients.User(callerId).SendAsync("CallAccepted", userId);
                _logger.LogInformation($"Call acceptance sent to {callerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling call acceptance: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý từ chối cuộc gọi
        public async Task RejectCall(string callerId)
        {
            try
            {
                var userId = Context.ConnectionId;
                _logger.LogInformation($"Call rejected by {userId} from {callerId}");

                if (string.IsNullOrEmpty(callerId))
                {
                    _logger.LogError("Invalid caller ID");
                    throw new ArgumentException("Invalid caller ID");
                }

                await Clients.User(callerId).SendAsync("CallRejected");
                _logger.LogInformation($"Call rejection sent to {callerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling call rejection: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý sự kiện nhận offer từ người gọi
        public async Task HandleCallOffer(string targetUserId, string offerJson)
        {
            try
            {
                var callerId = Context.ConnectionId;
                _logger.LogInformation($"Received call offer from {callerId} to {targetUserId}");
                _logger.LogInformation($"Offer JSON: {offerJson}");
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    _logger.LogError("Invalid target user ID");
                    throw new ArgumentException("Invalid target user ID");
                }

                if (string.IsNullOrEmpty(offerJson))
                {
                    _logger.LogError("Invalid offer JSON");
                    throw new ArgumentException("Invalid offer JSON");
                }

                // Validate JSON format
                try
                {
                    var offer = JsonSerializer.Deserialize<object>(offerJson);
                    _logger.LogInformation($"Valid offer JSON received: {offerJson}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Invalid JSON format: {ex.Message}");
                    throw new ArgumentException($"Invalid JSON format for offer: {ex.Message}");
                }

                await Clients.User(targetUserId).SendAsync("ReceiveCallOffer", offerJson, callerId);
                _logger.LogInformation($"Call offer forwarded to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling call offer: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý sự kiện nhận answer từ người nhận
        public async Task HandleCallAnswer(string targetUserId, string answerJson)
        {
            try
            {
                _logger.LogInformation($"Received call answer for {targetUserId}");
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    _logger.LogError("Invalid target user ID");
                    throw new ArgumentException("Invalid target user ID");
                }

                if (string.IsNullOrEmpty(answerJson))
                {
                    _logger.LogError("Invalid answer JSON");
                    throw new ArgumentException("Invalid answer JSON");
                }

                // Validate JSON format
                try
                {
                    var answer = JsonSerializer.Deserialize<object>(answerJson);
                    _logger.LogInformation($"Valid answer JSON received: {answerJson}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Invalid JSON format: {ex.Message}");
                    throw new ArgumentException($"Invalid JSON format for answer: {ex.Message}");
                }

                await Clients.User(targetUserId).SendAsync("ReceiveCallAnswer", answerJson);
                _logger.LogInformation($"Call answer forwarded to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling call answer: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý sự kiện nhận ICE candidate
        public async Task HandleIceCandidate(string targetUserId, string candidateJson)
        {
            try
            {
                _logger.LogInformation($"Received ICE candidate for {targetUserId}");
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    _logger.LogError("Invalid target user ID");
                    throw new ArgumentException("Invalid target user ID");
                }

                if (string.IsNullOrEmpty(candidateJson))
                {
                    _logger.LogError("Invalid candidate JSON");
                    throw new ArgumentException("Invalid candidate JSON");
                }

                // Validate JSON format
                try
                {
                    var candidate = JsonSerializer.Deserialize<object>(candidateJson);
                    _logger.LogInformation($"Valid ICE candidate JSON received: {candidateJson}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Invalid JSON format: {ex.Message}");
                    throw new ArgumentException($"Invalid JSON format for ICE candidate: {ex.Message}");
                }

                await Clients.User(targetUserId).SendAsync("ReceiveIceCandidate", candidateJson);
                _logger.LogInformation($"ICE candidate forwarded to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling ICE candidate: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Xử lý sự kiện kết thúc cuộc gọi
        public async Task EndCall(string targetUserId)
        {
            try
            {
                _logger.LogInformation($"Ending call for {targetUserId}");
                
                if (string.IsNullOrEmpty(targetUserId))
                {
                    _logger.LogError("Invalid target user ID");
                    throw new ArgumentException("Invalid target user ID");
                }

                await Clients.User(targetUserId).SendAsync("CallEnded");
                _logger.LogInformation($"Call ended notification sent to {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ending call: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
} 