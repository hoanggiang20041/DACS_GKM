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

        public async Task<IActionResult> ListSeniors(string searchCareNeeds, decimal? minPrice, decimal? maxPrice, string requesterId = null)
        {
            if (HttpContext.Session.GetString("UserRole") != "Caregiver") return AccessDenied();

            var query = _context.Seniors
                .Include(s => s.User)
                .Where(s => s.IsVerified);

            // Nếu có requesterId, chỉ hiển thị người đã gửi yêu cầu
            if (!string.IsNullOrEmpty(requesterId))
            {
                query = query.Where(s => s.UserId == requesterId);
                ViewBag.ShowingContactRequests = true;
                ViewBag.Title = "Thông tin người cần chăm sóc";
            }
            else
            {
                ViewBag.ShowingContactRequests = false;
                ViewBag.Title = "Danh sách người cần chăm sóc";
            }

            // Lọc theo nhu cầu chăm sóc
            if (!string.IsNullOrEmpty(searchCareNeeds))
            {
                query = query.Where(s => s.CareNeeds.Contains(searchCareNeeds));
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                query = query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(s => s.Price <= maxPrice.Value);
            }

            var seniors = await query.ToListAsync();

            // Lấy danh sách nhu cầu chăm sóc duy nhất
            var careNeeds = await _context.Seniors
                .Where(s => s.IsVerified && !string.IsNullOrEmpty(s.CareNeeds))
                .Select(s => s.CareNeeds)
                .Distinct()
                .ToListAsync();

            ViewBag.CareNeeds = careNeeds;
            ViewBag.SearchCareNeeds = searchCareNeeds;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.RequesterId = requesterId;

            return View(seniors);
        }
        private IActionResult AccessDenied()
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> BookSenior(int seniorId)
        {
            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == seniorId);

            if (senior == null)
            {
                return NotFound();
            }

            var viewModel = new BookSeniorViewModel
            {
                SeniorId = senior.Id,
                SeniorName = senior.FullName,
                SeniorAvatar = senior.AvatarUrl,
                SeniorLocation = senior.User?.Address,
                SeniorPhone = senior.User?.PhoneNumber,
                HealthInfo = senior.CareNeeds,
                Price = senior.Price
            };

            return View(viewModel);
        }
        [HttpPost]
        public async Task<IActionResult> BookSenior(int seniorId, string serviceType, DateTime startTime, int duration)
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

            string normalizedServiceType = NormalizeServiceType(serviceType);
            if (string.IsNullOrEmpty(normalizedServiceType))
            {
                TempData["ErrorMessage"] = "Loại dịch vụ không hợp lệ.";
                return RedirectToAction("BookSenior", new { seniorId = seniorId });
            }

            // Tính toán giá và thời gian kết thúc dựa trên thời lượng
            decimal totalBill = CalculateTotalBill(senior.Price, duration);
            DateTime endTime = startTime.AddHours(duration);

            // Tạo một Service mới
            var service = new Service
            {
                Name = normalizedServiceType,
                Description = $"Đề xuất dịch vụ chăm sóc cho {senior.Name}",
                BasePrice = totalBill,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            var careJob = new CareJob
            {
                SeniorId = senior.Id,
                CaregiverId = caregiver.Id,
                CaregiverName = caregiver.Name ?? "Không có tên",
                SeniorName = senior.Name ?? "Không có tên",
                SeniorPhone = senior.User?.PhoneNumber ?? "Không có số điện thoại",
                StartTime = startTime,
                EndTime = endTime,
                Status = "Đang chờ xác nhận từ Senior",
                TotalBill = totalBill,
                Deposit = totalBill * 0.3m,
                DepositAmount = totalBill * 0.3m,
                RemainingAmount = totalBill * 0.7m,
                ServiceType = normalizedServiceType,
                Description = $"Dịch vụ {normalizedServiceType} từ {startTime:dd/MM/yyyy HH:mm} đến {endTime:dd/MM/yyyy HH:mm}",
                CreatedByRole = "Caregiver",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDepositPaid = false,
                DepositMade = false,
                CaregiverAccepted = true,
                SeniorAccepted = false,
                DepositNote = $"NAPCOC_DV_{normalizedServiceType}_{DateTime.Now:yyyyMMddHHmmss}",
                PaymentStatus = "Chờ thanh toán",
                PaymentMethod = "Chưa thanh toán",
                ServiceId = service.Id,
                Location = senior.User?.Address ?? "Chưa cập nhật địa chỉ",
                Latitude = 0,
                Longitude = 0,
                HasRated = false,
                HasComplained = false
            };

            _context.CareJobs.Add(careJob);
            await _context.SaveChangesAsync();

            await SendNotificationToSenior(senior, careJob);

            TempData["SuccessMessage"] = "Đã gửi đề xuất dịch vụ đến khách hàng. Vui lòng chờ xác nhận.";
            return RedirectToAction("Index", "CareJobs");
        }

        private decimal CalculateTotalBill(decimal pricePerHour, int duration)
        {
            return pricePerHour * duration;
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
                    Title = "Đề xuất dịch vụ mới",
                    Message = $"Bạn nhận được đề xuất dịch vụ từ người chăm sóc #{job.Id}. Dịch vụ: {job.ServiceType}, Thời gian: {job.StartTime}. Vui lòng xem xét và xác nhận.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Type = "JobRequest",
                    Link = $"/CareJobs/Details/{job.Id}"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
            }
        }

        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("ListSeniors");
        }

        private string NormalizeServiceType(string serviceType)
        {
            return serviceType?.ToLower() switch
            {
                "comprehensive" => "comprehensive",
                "physiotherapy" => "physiotherapy",
                "medical" => "medical",
                "rehabilitation" => "rehabilitation",
                _ => null
            };
        }

        public async Task<IActionResult> ListSenior(string requesterId)
        {
            if (string.IsNullOrEmpty(requesterId))
            {
                return RedirectToAction("ListSeniors");
            }

            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == requesterId);

            if (senior == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng.";
                return RedirectToAction("ListSeniors");
            }

            var viewModel = new SeniorProfileViewModel
            {
                Id = senior.Id,
                UserId = senior.UserId,
                Name = senior.FullName,
                AvatarUrl = senior.AvatarUrl,
                Address = senior.User?.Address,
                PhoneNumber = senior.User?.PhoneNumber,
                Email = senior.User?.Email,
                CareNeeds = senior.CareNeeds,
                Price = senior.Price,
                IsVerified = senior.IsVerified,
                Status = senior.Status,
                CreatedAt = senior.RegistrationDate
            };

            return View(viewModel);
        }

        public class SeniorProfileViewModel
        {
            public int Id { get; set; }
            public string UserId { get; set; }
            public string Name { get; set; }
            public string AvatarUrl { get; set; }
            public string Address { get; set; }
            public string PhoneNumber { get; set; }
            public string Email { get; set; }
            public string CareNeeds { get; set; }
            public decimal Price { get; set; }
            public bool IsVerified { get; set; }
            public bool Status { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}