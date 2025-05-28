using Chamsoc.Data;
using Chamsoc.Hubs;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Caregiver,Admin,Senior")]
    public class SeniorsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public SeniorsController(AppDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub ?? throw new ArgumentNullException(nameof(notificationHub));
        }

        public async Task<IActionResult> ListSeniors(string searchNeeds)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            // Cho phép Caregiver, Senior và Admin truy cập
            if (userRole != "Caregiver" && userRole != "Senior" && userRole != "Admin") return AccessDenied();

            // Kiểm tra yêu cầu đang chờ chỉ cho Caregiver
            if (userRole == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                if (caregiver == null) return RedirectWithError("Không tìm thấy thông tin Caregiver.");

                var hasPendingRequest = await _context.CareJobs.AnyAsync(j => j.CaregiverId == caregiver.Id &&
                    (j.Status == "Đang chờ" || j.Status == "Đang chờ xác nhận từ Senior" ||
                     j.Status == "Đang chờ xác nhận từ Caregiver" || j.Status == "Đang chờ Người chăm sóc thanh toán cọc" ||
                     j.Status == "Đang thực hiện"));
                if (hasPendingRequest)
                {
                    TempData["PendingMessage"] = "Bạn đã có một yêu cầu đang chờ xử lý hoặc đang thực hiện. Không thể chọn thêm người cần chăm sóc cho đến khi yêu cầu hiện tại hoàn tất hoặc bị hủy.";
                }
            }

            var seniorsQuery = _context.Seniors
                .Where(s => s.Status == true && s.IsVerified == true) // Chỉ hiển thị Seniors rảnh và đã xác minh
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchNeeds))
                seniorsQuery = seniorsQuery.Where(s => s.CareNeeds.Contains(searchNeeds));

            return View(await seniorsQuery.ToListAsync());
        }
        private IActionResult AccessDenied()
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult BookSenior(int seniorId)
        {
            var senior = _context.Seniors.Find(seniorId);
            if (senior == null) return NotFound();
            return View(senior);
        }
        [HttpPost]
        public async Task<IActionResult> BookSenior(int seniorId, string serviceType, DateTime startTime)
        {
            var caregiverId = HttpContext.Session.GetString("UserId");
            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == caregiverId);
            var senior = await _context.Seniors.FindAsync(seniorId);
            if (caregiver == null || senior == null) return RedirectWithError("Không tìm thấy thông tin.");

            // Kiểm tra trạng thái và xác minh Senior
            if (!senior.Status || !senior.IsVerified)
            {
                TempData["ErrorMessage"] = $"Khách hàng {senior.Name} đang bận hoặc chưa được xác minh. Vui lòng liên hệ lại sau.";
                return RedirectToAction("ListSeniors");
            }

            var (totalBill, endTime) = CalculateBillAndEndTime(serviceType, caregiver.Pricing, startTime);
            if (totalBill == 0) return RedirectWithError("Loại dịch vụ không hợp lệ.");

            var careJob = new CareJob
            {
                SeniorId = senior.Id,
                CaregiverId = caregiver.Id,
                CaregiverName = caregiver.Name,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Đang chờ",
                TotalBill = totalBill,
                Deposit = totalBill * 0.3m,
                RemainingAmount = 0,
                ServiceType = serviceType,
                CreatedByRole = "Caregiver"
            };

            _context.CareJobs.Add(careJob);
            await _context.SaveChangesAsync();

            var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
            if (seniorUser != null)
            {
                var notification = new Notification
                {
                    UserId = seniorUser.Id,
                    JobId = careJob.Id,
                    Message = $"Bạn nhận được yêu cầu công việc từ người chăm sóc #{caregiver.Id}. Dịch vụ: {serviceType}, Thời gian: {startTime.ToString("dd/MM/yyyy HH:mm")}. Vui lòng xác nhận.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
            }

            TempData["SuccessMessage"] = "Đã gửi yêu cầu đến khách hàng. Vui lòng chờ xác nhận.";
            return RedirectToAction("Index", "CareJobs");
        }
        private (decimal, DateTime) CalculateBillAndEndTime(string serviceType, string pricingJson, DateTime startTime)
        {
            var pricing = new Dictionary<string, decimal>
            {
                { "1Hour", 1000000 }, { "2Hours", 1800000 }, { "5Sessions", 4500000 }
            };
            if (!string.IsNullOrEmpty(pricingJson))
            {
                try { pricing = JsonSerializer.Deserialize<Dictionary<string, decimal>>(pricingJson); }
                catch { }
            }
            switch (serviceType)
            {
                case "1Hour": return (pricing["1Hour"], startTime.AddHours(1));
                case "2Hours": return (pricing["2Hours"], startTime.AddHours(2));
                case "5Sessions": return (pricing["5Sessions"], startTime.AddHours(5));
                default: return (0, startTime);
            }
        }

        private async Task SendNotificationToSenior(Senior senior, CareJob job)
        {
            var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId);
            if (seniorUser != null)
            {
                var notification = new Notification
                {
                    UserId = seniorUser.Id,
                    JobId = job.Id,
                    Message = $"Bạn nhận được yêu cầu công việc #{job.Id}. Dịch vụ: {job.ServiceType}, Thời gian: {job.StartTime}. Xác nhận hoặc từ chối.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }
        }

        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("ListSeniors");
        }
    }
}