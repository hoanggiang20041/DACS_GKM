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
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
  
        public async Task<IActionResult> ListCaregivers(string search = null, string skill = null, decimal? minPrice = null, decimal? maxPrice = null)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var query = _context.Caregivers
                .Include(c => c.User)
                .Where(c => c.IsVerified && c.IsAvailable);

            // Áp dụng bộ lọc tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) || c.Skills.Contains(search));
            }

            if (!string.IsNullOrEmpty(skill))
            {
                query = query.Where(c => c.Skills.Contains(skill));
            }

            if (minPrice.HasValue)
            {
                query = query.Where(c => c.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(c => c.Price <= maxPrice.Value);
            }

            var caregivers = await query.ToListAsync();

            // Chuyển đổi Caregiver thành CaregiverViewModel
            var viewModels = new List<CaregiverViewModel>();
            foreach (var caregiver in caregivers)
            {
                var ratings = await _context.Ratings
                    .Where(r => r.CaregiverId == caregiver.Id)
                    .ToListAsync();

                var viewModel = new CaregiverViewModel
                {
                    Id = caregiver.Id,
                    UserId = caregiver.UserId,
                    Name = caregiver.Name,
                    Skills = caregiver.Skills,
                    Contact = caregiver.Contact,
                    Price = caregiver.Price,
                    IsAvailable = caregiver.IsAvailable,
                    IsVerified = caregiver.IsVerified,
                    AvatarUrl = caregiver.AvatarUrl,
                    CertificateFilePath = caregiver.CertificateFilePath,
                    AverageRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0,
                    RatingCount = ratings.Count,
                    RecentRatings = ratings.OrderByDescending(r => r.CreatedAt).Take(3).ToList()
                };

                viewModels.Add(viewModel);
            }

            // Lấy danh sách kỹ năng duy nhất từ tất cả caregivers
            var skills = await _context.Caregivers
                .Select(c => c.Skills)
                .Distinct()
                .ToListAsync();

            ViewBag.Skills = skills;
            ViewBag.Search = search;
            ViewBag.SelectedSkill = skill;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            return View(viewModels);
        }
        [HttpGet]
        public IActionResult BookCaregiver(string caregiverId)
        {
            if (HttpContext.Session.GetString("UserRole") != "Senior") return AccessDenied();
            if (!int.TryParse(caregiverId, out int id)) return BadRequest("Invalid caregiver ID.");
            
            // Kiểm tra xem người dùng có đang trong quá trình book với caregiver này không
            var userId = HttpContext.Session.GetString("UserId");
            var existingBooking = _context.CareJobs
                .Any(j => j.CaregiverId == id && 
                         j.Senior.UserId == userId && 
                         (j.Status == "Đang chờ" || j.Status == "Đang thực hiện"));

            var caregiver = _context.Caregivers.Find(id);
            if (caregiver == null) return NotFound();

            // Chỉ kiểm tra trạng thái nếu không phải là booking hiện tại của user
            if (!existingBooking && (!caregiver.IsAvailable || !caregiver.IsVerified))
            {
                TempData["ErrorMessage"] = $"Người chăm sóc {caregiver.Name} đang bận hoặc chưa được xác minh. Vui lòng liên hệ lại sau.";
                return RedirectToAction("ListCaregivers");
            }

            var senior = _context.Seniors.FirstOrDefault(s => s.UserId == userId);
            if (senior == null) return RedirectWithError("Không tìm thấy thông tin khách hàng.");

            var ratings = _context.Ratings
                .Where(r => r.CaregiverId == caregiver.Id)
                .ToList();

            var viewModel = new BookCaregiverViewModel
            {
                CaregiverId = caregiver.Id,
                SeniorId = senior.Id,
                CaregiverName = caregiver.Name,
                CaregiverAvatar = caregiver.AvatarUrl,
                CaregiverLocation = caregiver.User?.Address,
                CaregiverSkills = caregiver.Skills,
                ServicePrice = caregiver.Price,
                CaregiverRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0,
                CaregiverRatingCount = ratings.Count
            };

            var pricing = GetPricing(caregiver.Price);
            ViewBag.Pricing = pricing;
            ViewBag.SeniorPhone = _context.Users.FirstOrDefault(u => u.Id == userId)?.PhoneNumber ?? "Chưa có số điện thoại";
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> BookCaregiver(int caregiverId, string serviceType, DateTime startTime, double latitude, double longitude, string phoneNumber, int numberOfHours)
        {
            if (HttpContext.Session.GetString("UserRole") != "Senior") return AccessDenied();
            
            try
            {
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
                    TempData["ErrorMessage"] = "Bạn đã có một yêu cầu đang chờ xử lý hoặc đang thực hiện. Không thể chọn thêm người cần chăm sóc cho đến khi yêu cầu hiện tại hoàn tất hoặc bị hủy.";
                    TempData["ErrorAction"] = "Index";
                    TempData["ErrorController"] = "CareJobs";
                    TempData["ErrorButtonText"] = "Xem yêu cầu hiện tại";
                    return RedirectToAction("ListCaregivers");
                }

                if (latitude == 0 && longitude == 0)
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn một vị trí hợp lệ từ gợi ý hoặc cho phép truy cập vị trí.";
                    return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
                }

                string normalizedServiceType = NormalizeServiceType(serviceType);
                if (string.IsNullOrEmpty(normalizedServiceType))
                {
                    TempData["ErrorMessage"] = "Loại dịch vụ không hợp lệ.";
                    return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
                }

                var (totalBill, endTime) = CalculateBillAndEndTime(normalizedServiceType, caregiver.Price, startTime);
                if (totalBill == 0) return RedirectWithError("Loại dịch vụ không hợp lệ.");

                var careJob = await CreateCareJob(senior, caregiver, normalizedServiceType, startTime, endTime, totalBill, latitude, longitude, phoneNumber);
                await SendNotifications(careJob, senior, caregiver, phoneNumber);

                // Xóa các TempData cũ để tránh hiển thị sai thông báo
                TempData.Clear();
                TempData["SuccessMessage"] = $"Đã gửi yêu cầu đến {caregiver.Name} thành công. Vui lòng chờ xác nhận.";
                
                return RedirectToAction("Index", "CareJobs");
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException;
                var errorMessage = "Có lỗi xảy ra khi lưu dữ liệu: ";
                
                if (innerException != null)
                {
                    errorMessage += innerException.Message;
                    if (innerException.InnerException != null)
                    {
                        errorMessage += " - " + innerException.InnerException.Message;
                    }
                }
                else
                {
                    errorMessage += ex.Message;
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" - {ex.InnerException.Message}";
                }
                return RedirectToAction("BookCaregiver", new { caregiverId = caregiverId });
            }
        }

        private string NormalizeServiceType(string serviceType)
        {
            return serviceType?.ToLower() switch
            {
                "comprehensive" => "Chăm sóc toàn diện",
                "physiotherapy" => "Vật lí trị liệu",
                "medical" => "Chăm sóc y tế",
                "rehabilitation" => "Phục hồi chức năng",
                _ => null
            };
        }

        private async Task<CareJob> CreateCareJob(Senior senior, Caregiver caregiver, string serviceType, DateTime startTime, DateTime endTime, decimal totalBill, double latitude, double longitude, string phoneNumber)
        {
            // Create a new Service record
            var service = new Service
            {
                Name = serviceType,
                Description = $"Dịch vụ {serviceType} từ {startTime:dd/MM/yyyy HH:mm} đến {endTime:dd/MM/yyyy HH:mm}",
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
                CaregiverName = caregiver.Name,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Đang chờ xác nhận từ Caregiver",
                TotalBill = totalBill,
                Deposit = totalBill * 0.3m,
                RemainingAmount = totalBill - (totalBill * 0.3m),
                ServiceType = serviceType,
                CreatedByRole = "Senior",
                Latitude = latitude,
                Longitude = longitude,
                DepositNote = "Chưa thanh toán cọc",
                Description = $"Dịch vụ {serviceType} từ {startTime:dd/MM/yyyy HH:mm} đến {endTime:dd/MM/yyyy HH:mm}",
                PaymentStatus = "Chưa thanh toán",
                PaymentMethod = "Chưa chọn",
                SeniorName = senior.Name,
                SeniorPhone = phoneNumber,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDepositPaid = false,
                DepositMade = false,
                CaregiverAccepted = false,
                SeniorAccepted = true,
                Location = "Chưa cập nhật",
                Notifications = new List<Notification>(),
                ServiceId = service.Id
            };

            _context.CareJobs.Add(careJob);

            // Chỉ cập nhật trạng thái của Senior
            senior.Status = false; // Đánh dấu Senior đang bận
            _context.Seniors.Update(senior);
            
            await _context.SaveChangesAsync();

            return careJob;
        }

        private async Task SendNotifications(CareJob careJob, Senior senior, Caregiver caregiver, string phoneNumber)
        {
            var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId && u.Role == "Caregiver");
            if (caregiverUser != null)
            {
                var notification = new Notification
                {
                    UserId = caregiverUser.Id,
                    JobId = careJob.Id,
                    Title = "Yêu cầu chăm sóc mới",
                    Type = "BookingRequest",
                    Message = $"Bạn nhận được yêu cầu công việc từ khách hàng {senior.Name} (SĐT: {phoneNumber}). Dịch vụ: {careJob.ServiceType}, Thời gian: {careJob.StartTime:dd/MM/yyyy HH:mm}. Vui lòng xác nhận.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Link = $"/CareJobs/Details/{careJob.Id}"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", notification.Message);
            }

            var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId);
            if (seniorUser != null && seniorUser.PhoneNumber != phoneNumber)
            {
                seniorUser.PhoneNumber = phoneNumber;
                _context.Users.Update(seniorUser);
                await _context.SaveChangesAsync();
            }

            var seniorNotification = new Notification
            {
                UserId = senior.UserId,
                JobId = careJob.Id,
                Title = "Đã gửi yêu cầu chăm sóc",
                Type = "BookingConfirmation",
                Message = $"Bạn đã gửi yêu cầu đến người chăm sóc {caregiver.Name}. Vui lòng chờ xác nhận.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Link = $"/CareJobs/Details/{careJob.Id}"
            };
            _context.Notifications.Add(seniorNotification);
            await _context.SaveChangesAsync();
            await _notificationHub.Clients.User(senior.UserId).SendAsync("ReceiveNotification", seniorNotification.Message);
        }

        private (decimal totalBill, DateTime endTime) CalculateBillAndEndTime(string serviceType, decimal pricing, DateTime startTime)
        {
            // Lấy số giờ từ request
            if (!int.TryParse(HttpContext.Request.Form["NumberOfHours"], out int numberOfHours))
            {
                numberOfHours = 1; // Giá trị mặc định nếu không lấy được
            }

            // Tính toán tổng tiền dựa trên giá theo giờ và số giờ được chọn
            decimal totalBill = pricing * numberOfHours;
            DateTime endTime = startTime.AddHours(numberOfHours);

            return (totalBill, endTime);
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

            // Generate VietQR code
            var qrConfig = new VietQRConfig
            {
                AccountNo = "19034567890123", // Replace with your actual account number
                AccountName = "CONG TY TNHH CHAM SOC",
                BankName = "Techcombank",
                BankCode = "TCB",
                Template = "compact2"
            };

            var description = $"NAPCOC_DV{job.Id}_{job.ServiceType}";
            ViewBag.QRUrl = qrConfig.GenerateQRUrl(job.Deposit, description);
            ViewBag.BankName = qrConfig.BankName;
            ViewBag.AccountNo = qrConfig.AccountNo;
            ViewBag.AccountName = qrConfig.AccountName;
            ViewBag.Description = description;
            ViewBag.Amount = job.Deposit;

            return View(job);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmDeposit(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver")
                return AccessDenied();

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null || job.Status != "Đang chờ Người chăm sóc thanh toán cọc")
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ nạp cọc.");

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
                return AccessDenied();

            // Tạo bản ghi thanh toán
            var payment = new Payment
            {
                JobId = job.Id,
                SeniorId = job.SeniorId,
                CaregiverId = job.CaregiverId ?? 0,
                Amount = job.Deposit,
                Status = "Chờ thanh toán",
                CreatedAt = DateTime.Now,
                PaymentMethod = "BankTransfer",
                TransactionId = $"DEP_{job.Id}_{DateTime.Now:yyyyMMddHHmmss}",
                Notes = $"Nạp cọc cho công việc #{job.Id} - {job.ServiceType}",
                ApprovedBy = "System",
                RejectedBy = "System",
                RejectionReason = "N/A"
            };
            _context.Payments.Add(payment);

            // Cập nhật trạng thái công việc
            job.Status = "Đang chờ xác nhận thanh toán";
            job.DepositMade = true;
            job.PaymentStatus = "Chờ thanh toán";
            job.DepositNote = $"NAPCOC_DV{job.Id}_{job.ServiceType}";
            job.UpdatedAt = DateTime.Now;
            _context.CareJobs.Update(job);

            // Gửi thông báo cho admin
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
            if (admin != null)
            {
                var adminNotification = new Notification
                {
                    UserId = admin.Id,
                    JobId = job.Id,
                    Title = "Yêu cầu xác nhận thanh toán",
                    Message = $"Người chăm sóc #{caregiver.Id} đã xác nhận nạp cọc {job.Deposit.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} VNĐ cho công việc #{job.Id}. Vui lòng kiểm tra và xác nhận.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Type = "PaymentConfirmation"
                };
                _context.Notifications.Add(adminNotification);
                await _notificationHub.Clients.User(admin.Id).SendAsync("ReceiveNotification", adminNotification.Message);
            }

            // Gửi thông báo cho người chăm sóc
            var caregiverNotification = new Notification
            {
                UserId = caregiver.UserId,
                JobId = job.Id,
                Title = "Đã gửi yêu cầu xác nhận thanh toán",
                Message = $"Bạn đã xác nhận thanh toán cọc cho công việc #{job.Id}. Vui lòng đợi admin xác nhận.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "PaymentPending",
                Link = $"/CareJobs/Details/{job.Id}"
            };
            _context.Notifications.Add(caregiverNotification);
            await _notificationHub.Clients.User(caregiver.UserId).SendAsync("ReceiveNotification", caregiverNotification.Message);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã thanh toán. Vui lòng đợi admin xác nhận giao dịch.";
            return RedirectToAction("Details", "CareJobs", new { id = jobId });
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

        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("ListCaregivers");
        }

        private IActionResult AccessDenied() => View("~/Views/Shared/AccessDenied.cshtml");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendContactRequest([FromBody] ContactRequestModel model)
        {
            try
            {
                Console.WriteLine($"Starting SendContactRequest with model: {JsonSerializer.Serialize(model)}");

                if (model == null)
                {
                    Console.WriteLine("Model is null");
                    return BadRequest(new { message = "Dữ liệu không hợp lệ" });
                }

                var userId = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");
                Console.WriteLine($"User info: Id={userId}, Role={userRole}");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
                {
                    Console.WriteLine("User not authenticated");
                    return Unauthorized(new { message = "Vui lòng đăng nhập để gửi yêu cầu" });
                }

                if (userRole != "Senior")
                {
                    Console.WriteLine($"Invalid role: {userRole}");
                    return StatusCode(403, new { message = "Chỉ người dùng Senior mới có thể gửi yêu cầu liên hệ" });
                }

                Console.WriteLine("Looking up senior...");
                var senior = await _context.Seniors
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (senior == null)
                {
                    Console.WriteLine($"Senior not found for userId: {userId}");
                    return NotFound(new { message = "Không tìm thấy thông tin người dùng" });
                }
                Console.WriteLine($"Found senior: {senior.User?.FullName}");

                Console.WriteLine($"Looking up caregiver with id: {model.CaregiverId}");
                var caregiver = await _context.Caregivers
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == model.CaregiverId);

                if (caregiver == null)
                {
                    Console.WriteLine($"Caregiver not found with id: {model.CaregiverId}");
                    return NotFound(new { message = "Không tìm thấy người chăm sóc" });
                }
                Console.WriteLine($"Found caregiver: {caregiver.User?.FullName}");

                if (string.IsNullOrEmpty(model.Message?.Trim()))
                {
                    Console.WriteLine("Message is empty");
                    return BadRequest(new { message = "Vui lòng nhập lời nhắn" });
                }

                // Kiểm tra dữ liệu trước khi tạo notification
                if (string.IsNullOrEmpty(caregiver.UserId))
                {
                    Console.WriteLine("Error: Caregiver UserId is null or empty");
                    return StatusCode(500, new { message = "Không tìm thấy thông tin người chăm sóc" });
                }

                if (string.IsNullOrEmpty(senior.User?.FullName))
                {
                    Console.WriteLine("Error: Senior FullName is null or empty");
                    return StatusCode(500, new { message = "Không tìm thấy thông tin người dùng" });
                }

                if (string.IsNullOrEmpty(senior.User?.PhoneNumber))
                {
                    Console.WriteLine("Error: Senior PhoneNumber is null or empty");
                    return StatusCode(500, new { message = "Thông tin số điện thoại không hợp lệ" });
                }

                if (string.IsNullOrEmpty(senior.User?.Address))
                {
                    Console.WriteLine("Error: Senior Address is null or empty");
                    return StatusCode(500, new { message = "Thông tin địa chỉ không hợp lệ" });
                }

                Console.WriteLine("Creating notification...");
                // Tạo thông báo cho người chăm sóc
                var notification = new Notification
                {
                    UserId = caregiver.UserId,
                    Title = "Yêu cầu liên hệ mới",
                    Message = $"Bạn có yêu cầu liên hệ mới từ {senior.User.FullName}.\n" +
                             $"Thông tin người gửi:\n" +
                             $"- Tên: {senior.User.FullName}\n" +
                             $"- Số điện thoại: {senior.User.PhoneNumber}\n" +
                             $"- Địa chỉ: {senior.User.Address}\n\n" +
                             $"Lời nhắn:\n{model.Message.Trim()}",
                    Type = "ContactRequest",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Link = $"/Caregivers/ViewContactRequest/{senior.UserId}"
                };

                try
                {
                    Console.WriteLine("Saving notification to database...");
                    Console.WriteLine($"Notification details: UserId={notification.UserId}, Title={notification.Title}, Message={notification.Message}, Type={notification.Type}, Link={notification.Link}");
                    
                    // Kiểm tra xem notification có hợp lệ không
                    var validationContext = new ValidationContext(notification, serviceProvider: null, items: null);
                    var validationResults = new List<ValidationResult>();
                    var isValid = Validator.TryValidateObject(notification, validationContext, validationResults, validateAllProperties: true);
                    
                    if (!isValid)
                    {
                        Console.WriteLine("Validation errors:");
                        foreach (var validationResult in validationResults)
                        {
                            Console.WriteLine($"- {validationResult.ErrorMessage}");
                        }
                        return StatusCode(500, new { message = "Dữ liệu thông báo không hợp lệ" });
                    }

                    _context.Notifications.Add(notification);
                    Console.WriteLine("Notification added to context");
                    
                    try 
                    {
                        await _context.SaveChangesAsync();
                        Console.WriteLine("Notification saved successfully");

                        Console.WriteLine("Sending SignalR notification...");
                        // Gửi thông báo realtime qua SignalR
                        await _notificationHub.Clients.User(caregiver.UserId)
                            .SendAsync("ReceiveNotification", new
                            {
                                title = notification.Title,
                                message = notification.Message,
                                type = notification.Type,
                                link = notification.Link
                            });
                        Console.WriteLine("SignalR notification sent successfully");

                        return Ok(new { message = "Yêu cầu liên hệ đã được gửi thành công" });
                    }
                    catch (DbUpdateException dbEx)
                    {
                        Console.WriteLine($"Database error during SaveChanges: {dbEx.Message}");
                        if (dbEx.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                            if (dbEx.InnerException.InnerException != null)
                            {
                                Console.WriteLine($"Inner inner exception: {dbEx.InnerException.InnerException.Message}");
                                Console.WriteLine($"Inner inner exception stack trace: {dbEx.InnerException.InnerException.StackTrace}");
                            }
                        }
                        return StatusCode(500, new { message = "Có lỗi xảy ra khi lưu thông báo" });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in notification process: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                    }
                    return StatusCode(500, new { message = "Có lỗi xảy ra khi gửi thông báo" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendContactRequest: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
                }
                return StatusCode(500, new { message = "Có lỗi xảy ra khi xử lý yêu cầu" });
            }
        }

        public class ContactRequestModel
        {
            public int CaregiverId { get; set; }
            public string Message { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> ViewContactRequest(string seniorUserId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
            {
                return RedirectToAction("Login", "Account");
            }

            if (userRole != "Caregiver")
            {
                return AccessDenied();
            }

            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == seniorUserId);

            if (senior == null)
            {
                return NotFound("Không tìm thấy thông tin người gửi yêu cầu");
            }

            // Lấy thông báo yêu cầu liên hệ gần nhất
            var notification = await _context.Notifications
                .Where(n => n.UserId == userId && 
                           n.Type == "ContactRequest" && 
                           n.Link == $"/Caregivers/ViewContactRequest/{seniorUserId}")
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            if (notification == null)
            {
                // Thử tìm thông báo theo cách khác
                notification = await _context.Notifications
                    .Where(n => n.UserId == userId && 
                               n.Type == "ContactRequest" && 
                               n.Message.Contains(senior.User.FullName))
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefaultAsync();

                if (notification == null)
                {
                    return NotFound("Không tìm thấy thông báo yêu cầu liên hệ");
                }
            }

            // Nếu có JobId, kiểm tra xem công việc có tồn tại không
            CareJob relatedJob = null;
            if (notification.JobId.HasValue)
            {
                relatedJob = await _context.CareJobs
                    .FirstOrDefaultAsync(j => j.Id == notification.JobId.Value);
            }

            var viewModel = new ContactRequestViewModel
            {
                SeniorId = senior.UserId,
                SeniorName = senior.User.FullName,
                SeniorPhone = senior.User.PhoneNumber,
                SeniorAddress = senior.User.Address,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt,
                HasRelatedJob = relatedJob != null,
                JobId = relatedJob?.Id,
                JobStatus = relatedJob?.Status
            };

            return View(viewModel);
        }

        public class ContactRequestViewModel
        {
            public string SeniorId { get; set; }
            public string SeniorName { get; set; }
            public string SeniorPhone { get; set; }
            public string SeniorAddress { get; set; }
            public string Message { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool HasRelatedJob { get; set; }
            public int? JobId { get; set; }
            public string JobStatus { get; set; }
        }
    }
}