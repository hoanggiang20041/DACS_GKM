using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Chamsoc.Hubs;
using System.Threading.Tasks;

namespace Chamsoc.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CallController : ControllerBase
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public CallController(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost("HandleOffer")]
        public async Task<IActionResult> HandleOffer([FromBody] WebRTCOffer offer)
        {
            await _hubContext.Clients.User(offer.TargetUserId)
                .SendAsync("ReceiveCallOffer", offer.Offer);
            return Ok();
        }

        [HttpPost("HandleAnswer")]
        public async Task<IActionResult> HandleAnswer([FromBody] WebRTCAnswer answer)
        {
            await _hubContext.Clients.User(answer.TargetUserId)
                .SendAsync("ReceiveCallAnswer", answer.Answer);
            return Ok();
        }

        [HttpPost("HandleIceCandidate")]
        public async Task<IActionResult> HandleIceCandidate([FromBody] WebRTCIceCandidate candidate)
        {
            await _hubContext.Clients.User(candidate.TargetUserId)
                .SendAsync("ReceiveIceCandidate", candidate.Candidate);
            return Ok();
        }
    }

    public class WebRTCOffer
    {
        public string TargetUserId { get; set; }
        public object Offer { get; set; }
    }

    public class WebRTCAnswer
    {
        public string TargetUserId { get; set; }
        public object Answer { get; set; }
    }

    public class WebRTCIceCandidate
    {
        public string TargetUserId { get; set; }
        public object Candidate { get; set; }
    }
} 