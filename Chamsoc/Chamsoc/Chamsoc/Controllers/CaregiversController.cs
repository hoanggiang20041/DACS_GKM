using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QRCoder;
using System.IO;
using Chamsoc.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Senior,Caregiver")]
    public class CaregiversController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public CaregiversController(AppDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationHub = notificationHub ?? throw new ArgumentNullException(nameof(notificationHub));
        }
  
        public async Task<IActionResult> ListCaregivers(string searchSkills, string maxPrice, string customMaxPrice)
        {
            if (HttpContext.Session.GetString("UserRole") != "Senior") return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null) return RedirectWithError("Không tìm thấy thông tin Senior.");

            // Kiểm tra xem Senior đã có yêu cầu đang chờ hoặc đang thực hiện chưa
            var hasPendingRequest = await _context.CareJobs.AnyAsync(j => j.SeniorId == senior.Id &&
                (j.Status == "Đang chờ" || j.Status == "Đang chờ xác nhận từ Senior" ||
                 j.Status == "Đang chờ xác nhận từ Caregiver" || j.Status == "Đang chờ Người chăm sóc thanh toán cọc" ||
                 j.Status == "Đang thực hiện"));
            if (hasPendingRequest)
            {
                TempData["PendingMessage"] = "Bạn đã có một yêu cầu đang chờ xử lý hoặc đang thực hiện. Không thể chọn thêm người chăm sóc cho đến khi yêu cầu hiện tại hoàn tất hoặc bị hủy.";
            }

            var caregiversQuery = _context.Caregivers
                .Where(c => c.IsVerified == true && c.IsAvailable == true) // Chỉ hiển thị Caregivers rảnh và đã xác minh
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchSkills))
            {
                caregiversQuery = caregiversQuery.Where(c => c.Skills == searchSkills);
                ViewBag.SearchSkills = searchSkills;
            }
            if (!string.IsNullOrEmpty(maxPrice))
            {
                if (maxPrice == "-1" && decimal.TryParse(customMaxPrice, out decimal customMaxPriceValue))
                {
                    caregiversQuery = caregiversQuery.Where(c => c.Price <= customMaxPriceValue);
                    ViewBag.MaxPrice = -1;
                    ViewBag.CustomMaxPrice = customMaxPriceValue;
                }
                else if (decimal.TryParse(maxPrice, out decimal maxPriceValue))
                {
                    caregiversQuery = caregiversQuery.Where(c => c.Price <= maxPriceValue);
                    ViewBag.MaxPrice = maxPriceValue;
                }
            }

            return View(await caregiversQuery.ToListAsync());
        }
        [HttpGet]
        public IActionResult BookCaregiver(string caregiverId)
        {
            if (HttpContext.Session.GetString("UserRole") != "Senior") return AccessDenied();
            if (!int.TryParse(caregiverId, out int id)) return BadRequest("Invalid caregiver ID.");
            var caregiver = _context.Caregivers.Find(id);
            if (caregiver == null) return NotFound();

            if (!caregiver.IsAvailable || !caregiver.IsVerified)
            {
                TempData["ErrorMessage"] = $"Người chăm sóc {caregiver.Name} đang bận hoặc chưa được xác minh. Vui lòng liên hệ lại sau.";
                return RedirectToAction("ListCaregivers");
            }

            var userId = HttpContext.Session.GetString("UserId");
            var senior = _context.Seniors.FirstOrDefault(s => s.UserId == userId);
            if (senior == null) return RedirectWithError("Không tìm thấy thông tin khách hàng.");

            var existingJobs = _context.CareJobs
                .Count(j => j.SeniorId == senior.Id && (j.Status == "Đang chờ" || j.Status == "Đang thực hiện"));
            if (existingJobs > 0)
            {
                TempData["ErrorMessage"] = "Bạn đã có lịch đang chờ hoặc đang thực hiện. Không thể chốt thêm lịch.";
                return RedirectToAction("ListCaregivers");
            }

            var pricing = GetPricing(caregiver.Price);
            ViewBag.Pricing = pricing;
            ViewBag.SeniorPhone = _context.Users.FirstOrDefault(u => u.Id == userId)?.PhoneNumber ?? "Chưa có số điện thoại";
            return View(caregiver);
        }

        [HttpPost]
        public async Task<IActionResult> BookCaregiver(int caregiverId, string serviceType, DateTime startTime, double latitude, double longitude, string phoneNumber)
        {
            if (HttpContext.Session.GetString("UserRole") != "Senior") return AccessDenied();
            var caregiver = await _context.Caregivers.FindAsync(caregiverId);
            if (caregiver == null) return NotFound();

            if (!caregiver.IsAvailable || !caregiver.IsVerified)
            {
                TempData["ErrorMessage"] = $"Người chăm sóc {caregiver.Name} đang bận hoặc chưa được xác minh. Vui lòng liên hệ lại sau.";
                return RedirectToAction("ListCaregivers");
            }

            var seniorId = HttpContext.Session.GetString("UserId");
            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == seniorId);
            if (senior == null) return RedirectWithError("Không tìm thấy thông tin khách hàng.");

            var existingJobs = await _context.CareJobs
                .CountAsync(j => j.SeniorId == senior.Id && (j.Status == "Đang chờ" || j.Status == "Đang thực hiện"));
            if (existingJobs > 0)
            {
                TempData["ErrorMessage"] = "Bạn đã có lịch đang chờ hoặc đang thực hiện. Không thể chốt thêm lịch.";
                return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
            }

            if (latitude == 0 && longitude == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một vị trí hợp lệ từ gợi ý hoặc cho phép truy cập vị trí.";
                return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
            }

            var (totalBill, endTime) = CalculateBillAndEndTime(serviceType, caregiver.Price, startTime);
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
                CreatedByRole = "Senior",
                Latitude = latitude,
                Longitude = longitude
            };

            _context.CareJobs.Add(careJob);
            await _context.SaveChangesAsync();

            var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId && u.Role == "Caregiver");
            if (caregiverUser != null)
            {
                var notification = new Notification
                {
                    UserId = caregiverUser.Id,
                    JobId = careJob.Id,
                    Message = $"Bạn nhận được yêu cầu công việc từ khách hàng #{senior.Id}. Dịch vụ: {serviceType}, Thời gian: {startTime:dd/MM/yyyy HH:mm}, SĐT: {phoneNumber}. Vui lòng xác nhận.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", notification.Message);
            }

            var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == seniorId);
            if (seniorUser != null && seniorUser.PhoneNumber != phoneNumber)
            {
                seniorUser.PhoneNumber = phoneNumber;
                _context.Users.Update(seniorUser);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Đã gửi yêu cầu đến {caregiver.Name}. Vui lòng chờ xác nhận.";
            return RedirectToAction("Index", "CareJobs");
        }

        private (decimal totalBill, DateTime endTime) CalculateBillAndEndTime(string serviceType, decimal pricing, DateTime startTime)
        {
            switch (serviceType)
            {
                case "1Hour": return (pricing, startTime.AddHours(1));
                case "2Hours": return (pricing * 2, startTime.AddHours(2));
                case "5Sessions": return (pricing * 5, startTime.AddHours(5)); // Giả định 5 giờ liên tục
                default: return (0, startTime);
            }
        }

        private decimal GetPricing(decimal pricing)
        {
            return pricing;
        }
        [HttpPost]
        public async Task<IActionResult> ConfirmJob(int notificationId)
        {
            if (HttpContext.Session.GetString("UserRole") != "Caregiver") return AccessDenied();
            var userId = HttpContext.Session.GetString("UserId");

            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null || notification.UserId != userId) return RedirectWithError("Không tìm thấy thông báo.");

            var job = await _context.CareJobs.FindAsync(notification.JobId);
            if (job == null || job.Status != "Đang chờ") return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ.");
            if (_context.Caregivers.FirstOrDefault(c => c.UserId == userId)?.Id != job.CaregiverId) return RedirectWithError("Công việc không thuộc về bạn.");

            notification.IsRead = true;
            _context.Notifications.Update(notification);

            var senior = await _context.Seniors.FindAsync(job.SeniorId);
            if (senior != null)
            {
                var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId);
                if (seniorUser != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = seniorUser.Id,
                        JobId = job.Id,
                        Message = $"Người chăm sóc đã xác nhận công việc #{job.Id}. Chờ nạp cọc để bắt đầu.",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Bạn đã xác nhận công việc. Vui lòng nạp cọc để tiếp tục.";
            return RedirectToAction("Index", "CareJobs");
        }
        [HttpGet]
        public async Task<IActionResult> ConfirmDepositGet(int jobId)
        {

            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver")
                return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Đang chờ Người chăm sóc thanh toán cọc") // Updated to match Deposit
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ nạp cọc.");

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
                return AccessDenied();

            // Generate a unique confirmation code
            var confirmationCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            HttpContext.Session.SetString($"ConfirmationCode_{jobId}", confirmationCode);

            // Generate QR code
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(confirmationCode, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20);
            ViewBag.QRCodeImage = $"data:image/png;base64,{Convert.ToBase64String(qrCodeImage)}";

            return View("ConfirmDeposit", job); // Assuming "ConfirmDeposit" is the view name
        }
        [HttpGet]
        public async Task<IActionResult> Deposit(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver")
                return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Đang chờ Người chăm sóc thanh toán cọc")
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ nạp cọc.");

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
                return AccessDenied();

            ViewBag.JobId = jobId;
            ViewBag.TotalBill = job.TotalBill;
            ViewBag.DepositAmount = job.Deposit;
            return View(job);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmDeposit(int jobId, string confirmationCode)
        {

            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver")
                return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Đang chờ Người chăm sóc thanh toán cọc") // Updated to match Deposit
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ nạp cọc.");

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
                return AccessDenied();

            var storedCode = HttpContext.Session.GetString($"ConfirmationCode_{jobId}");
            if (string.IsNullOrEmpty(storedCode) || storedCode != confirmationCode)
            {
                TempData["ErrorMessage"] = "Mã xác nhận không đúng. Vui lòng quét mã để lấy mã mới.";
                return RedirectToAction("ConfirmDepositGet", new { jobId });
            }

            // Confirm deposit
            job.Deposit = job.TotalBill * 0.3m; // 30% deposit
            job.Status = "Đang thực hiện"; // Move to "Đang thực hiện" after deposit
            _context.CareJobs.Update(job);

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
            if (admin != null)
            {
                admin.Balance += job.Deposit;
                _context.Users.Update(admin);

                var adminNotification = new Notification
                {
                    UserId = admin.Id,
                    JobId = job.Id,
                    Message = $"Người chăm sóc #{caregiver.Id} đã nạp cọc {job.Deposit.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} VNĐ cho công việc #{job.Id}.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(adminNotification);
                await _notificationHub.Clients.User(admin.Id).SendAsync("ReceiveNotification", adminNotification.Message);
            }

            await _context.SaveChangesAsync();

            // Clear session code
            HttpContext.Session.Remove($"ConfirmationCode_{jobId}");

            TempData["SuccessMessage"] = "Thanh toán cọc thành công! Công việc đã sẵn sàng.";
            return RedirectToAction("DepositSuccess", new { jobId });
        }
        private Dictionary<string, decimal> GetPricing(string pricingJson)
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
            return pricing;
        }

        private (decimal, DateTime) CalculateBillAndEndTime(string serviceType, string pricingJson, DateTime startTime)
        {
            var pricing = GetPricing(pricingJson);
            switch (serviceType)
            {
                case "1Hour": return (pricing["1Hour"], startTime.AddHours(1));
                case "2Hours": return (pricing["2Hours"], startTime.AddHours(2));
                case "5Sessions": return (pricing["5Sessions"], startTime.AddHours(5));
                default: return (0, startTime);
            }
        }
        [HttpGet]
        public async Task<IActionResult> DepositSuccess(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver")
                return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Đang thực hiện")
                return RedirectWithError("Công việc không hợp lệ hoặc chưa ở trạng thái thực hiện.");

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
                return AccessDenied();

            Response.Headers.Add("Refresh", "5;URL=" + Url.Action("Index", "CareJobs"));
            return View(job);
        }

        private async Task SendNotificationToCaregiver(Caregiver caregiver, CareJob job, Senior senior)
        {
            var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId);
            if (caregiverUser != null)
            {
                var notification = new Notification
                {
                    UserId = caregiverUser.Id,
                    JobId = job.Id,
                    Message = $"Bạn nhận được yêu cầu công việc #{job.Id}. Dịch vụ: {job.ServiceType}, Thời gian: {job.StartTime}. Xác nhận trong 24h.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }
        }


        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("ListCaregivers");
        }

        private IActionResult AccessDenied() => View("~/Views/Shared/AccessDenied.cshtml");
    }
}