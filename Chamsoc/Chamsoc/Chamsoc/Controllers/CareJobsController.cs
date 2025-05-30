using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Chamsoc.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Senior,Caregiver,Admin")]
    public class CareJobsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CareJobsController(AppDbContext context, IHubContext<NotificationHub> notificationHub, IWebHostEnvironment webHostEnvironment)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationHub = notificationHub ?? throw new ArgumentNullException(nameof(notificationHub));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
        }

        public async Task<IActionResult> Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var jobs = await GetJobsByUserRole(userRole, userId);
            var viewModels = jobs.Select(job => new CareJobViewModel
            {
                Id = job.Id,
                SeniorId = job.SeniorId,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorName = job.SeniorName,
                CaregiverName = job.CaregiverName,
                SeniorPhone = job.SeniorPhone,
                StartTime = job.StartTime,
                EndTime = job.EndTime,
                Status = job.Status,
                TotalBill = job.TotalBill,
                Deposit = job.Deposit,
                RemainingAmount = job.RemainingAmount,
                ServiceType = job.ServiceType,
                CreatedByRole = job.CreatedByRole,
                Latitude = job.Latitude,
                Longitude = job.Longitude,
                Location = job.Location,
                Description = job.Description,
                PaymentStatus = job.PaymentStatus,
                PaymentMethod = job.PaymentMethod,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                IsDepositPaid = job.IsDepositPaid,
                DepositMade = job.DepositMade,
                CaregiverAccepted = job.CaregiverAccepted,
                SeniorAccepted = job.SeniorAccepted,
                DepositNote = job.DepositNote,
                Notifications = job.Notifications?.ToList() ?? new List<Notification>(),
                HasRated = job.HasRated ?? false,
                HasComplained = job.HasComplained ?? false,
                Senior = job.Senior,
                Caregiver = job.Caregiver
            }).ToList();

            return View(viewModels);
        }

        private async Task<List<CareJob>> GetJobsByUserRole(string userRole, string userId)
        {
            var query = _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .Include(j => j.Notifications)
                .AsQueryable();

            switch (userRole)
            {
                case "Senior":
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (senior != null)
                    {
                        query = query.Where(j => j.SeniorId == senior.Id);
                        // Chỉ cập nhật trạng thái nếu có công việc đang thực hiện và senior đang sẵn sàng
                        var hasActiveJob = await query.AnyAsync(j => j.Status == "Đang thực hiện");
                        if (hasActiveJob && senior.Status)
                        {
                            senior.Status = false; // Đánh dấu Senior đang bận
                            _context.Seniors.Update(senior);
                            await _context.SaveChangesAsync();
                        }
                        // Nếu không có công việc đang thực hiện và senior đang bận, cập nhật lại thành sẵn sàng
                        else if (!hasActiveJob && !senior.Status)
                        {
                            senior.Status = true; // Đánh dấu Senior sẵn sàng
                            _context.Seniors.Update(senior);
                            await _context.SaveChangesAsync();
                        }
                    }
                    break;

                case "Caregiver":
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                    if (caregiver != null)
                    {
                        query = query.Where(j => j.CaregiverId == caregiver.Id);
                        // Chỉ cập nhật trạng thái nếu có công việc đang thực hiện và caregiver đang sẵn sàng
                        var hasActiveJob = await query.AnyAsync(j => j.Status == "Đang thực hiện");
                        if (hasActiveJob && caregiver.IsAvailable)
                        {
                            caregiver.IsAvailable = false; // Đánh dấu Caregiver đang bận
                            _context.Caregivers.Update(caregiver);
                            await _context.SaveChangesAsync();
                        }
                        // Nếu không có công việc đang thực hiện và caregiver đang bận, cập nhật lại thành sẵn sàng
                        else if (!hasActiveJob && !caregiver.IsAvailable)
                        {
                            caregiver.IsAvailable = true; // Đánh dấu Caregiver sẵn sàng
                            _context.Caregivers.Update(caregiver);
                            await _context.SaveChangesAsync();
                        }
                    }
                    break;

                case "Admin":
                    // Admin có thể xem tất cả
                    break;

                default:
                    return new List<CareJob>();
            }

            return await query
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await GetJobWithDetails(id);
            if (job == null) return NotFound();

            if (!await CanAccessJob(job, userRole, userId))
                return AccessDenied();

            var viewModel = new CareJobViewModel
            {
                Id = job.Id,
                SeniorId = job.SeniorId,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorName = job.SeniorName,
                CaregiverName = job.CaregiverName,
                SeniorPhone = job.SeniorPhone,
                StartTime = job.StartTime,
                EndTime = job.EndTime,
                Status = job.Status,
                TotalBill = job.TotalBill,
                Deposit = job.Deposit,
                RemainingAmount = job.RemainingAmount,
                ServiceType = job.ServiceType,
                CreatedByRole = job.CreatedByRole,
                Latitude = job.Latitude,
                Longitude = job.Longitude,
                Location = job.Location,
                Description = job.Description,
                PaymentStatus = job.PaymentStatus,
                PaymentMethod = job.PaymentMethod,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                IsDepositPaid = job.IsDepositPaid,
                DepositMade = job.DepositMade,
                CaregiverAccepted = job.CaregiverAccepted,
                SeniorAccepted = job.SeniorAccepted,
                DepositNote = job.DepositNote,
                Notifications = job.Notifications?.ToList() ?? new List<Notification>(),
                HasRated = job.HasRated ?? false,
                HasComplained = job.HasComplained ?? false,
                Senior = job.Senior,
                Caregiver = job.Caregiver
            };

            return View(viewModel);
        }

        private async Task<CareJob> GetJobWithDetails(int id)
        {
            return await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .Include(j => j.Notifications)
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        private async Task<bool> CanAccessJob(CareJob job, string userRole, string userId)
        {
            switch (userRole)
            {
                case "Senior":
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
                    return senior != null && job.SeniorId == senior.Id;

                case "Caregiver":
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                    return caregiver != null && job.CaregiverId == caregiver.Id;

                case "Admin":
                    return true;

                default:
                    return false;
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus, string location = null, double? latitude = null, double? longitude = null)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .Include(j => j.Senior)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            if (!await CanAccessJob(job, userRole, userId))
                return AccessDenied();

            if (!IsValidStatusTransition(job.Status, newStatus, userRole))
            {
                TempData["ErrorMessage"] = "Không thể chuyển sang trạng thái này.";
                return RedirectToAction("Details", new { id });
            }

            // Kiểm tra và cập nhật thông tin vị trí khi Senior chấp nhận yêu cầu
            if (userRole == "Senior" && 
                job.Status == "Đang chờ xác nhận từ Senior" && 
                newStatus == "Đang chờ Người chăm sóc thanh toán cọc")
            {
                if (string.IsNullOrEmpty(location) || !latitude.HasValue || !longitude.HasValue)
                {
                    TempData["ErrorMessage"] = "Vui lòng cung cấp đầy đủ thông tin vị trí.";
                    return RedirectToAction("Details", new { id });
                }

                job.Location = location;
                job.Latitude = latitude.Value;
                job.Longitude = longitude.Value;
            }

            var oldStatus = job.Status;
            job.Status = newStatus;
            job.UpdatedAt = DateTime.Now;

            // Cập nhật trạng thái của Senior và Caregiver khi công việc chuyển sang trạng thái mới
            if (newStatus == "Đang thực hiện")
            {
                if (job.Senior != null)
                {
                    job.Senior.Status = false; // Đánh dấu Senior đang bận
                    _context.Seniors.Update(job.Senior);
                }
                if (job.Caregiver != null)
                {
                    job.Caregiver.IsAvailable = false; // Đánh dấu Caregiver đang bận
                    _context.Caregivers.Update(job.Caregiver);
                }
            }
            else if (newStatus == "Hoàn thành" || newStatus == "Hủy")
            {
                if (job.Senior != null)
                {
                    job.Senior.Status = true; // Đánh dấu Senior sẵn sàng
                    _context.Seniors.Update(job.Senior);
                }
                if (job.Caregiver != null)
                {
                    job.Caregiver.IsAvailable = true; // Đánh dấu Caregiver sẵn sàng
                    _context.Caregivers.Update(job.Caregiver);
                }
            }

            // Xử lý khi một bên chấp nhận yêu cầu
            if (newStatus == "Đang chờ Người chăm sóc thanh toán cọc")
            {
                // Hủy các yêu cầu khác đang chờ
                var otherPendingJobs = await _context.CareJobs
                    .Where(j => j.Id != job.Id && 
                               ((j.SeniorId == job.SeniorId && j.Status.StartsWith("Đang chờ")) ||
                                (j.CaregiverId == job.CaregiverId && j.Status.StartsWith("Đang chờ"))))
                    .ToListAsync();

                foreach (var pendingJob in otherPendingJobs)
                {
                    pendingJob.Status = "Hủy";
                    pendingJob.UpdatedAt = DateTime.Now;
                    _context.CareJobs.Update(pendingJob);
                }
            }

            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                await SendStatusUpdateNotification(job, userRole);
                TempData["SuccessMessage"] = "Cập nhật trạng thái thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        private bool IsValidStatusTransition(string currentStatus, string newStatus, string userRole)
        {
            var validTransitions = new Dictionary<string, Dictionary<string, string[]>>
            {
                ["Senior"] = new Dictionary<string, string[]>
                {
                    ["Đang chờ xác nhận từ Senior"] = new[] { "Đang chờ Người chăm sóc thanh toán cọc", "Hủy" },
                    ["Đang chờ xác nhận hoàn thành"] = new[] { "Hoàn thành", "Đang thực hiện" }
                },
                ["Caregiver"] = new Dictionary<string, string[]>
                {
                    ["Đang chờ xác nhận từ Caregiver"] = new[] { "Đang chờ Người chăm sóc thanh toán cọc", "Hủy" },
                    ["Đang thực hiện"] = new[] { "Đang chờ xác nhận hoàn thành", "Hủy" }
                },
                ["Admin"] = new Dictionary<string, string[]>
                {
                    ["Đang chờ"] = new[] { "Đang chờ xác nhận từ Caregiver", "Đang chờ xác nhận từ Senior", "Hủy" },
                    ["Đang chờ xác nhận từ Caregiver"] = new[] { "Đang chờ Người chăm sóc thanh toán cọc", "Hủy" },
                    ["Đang chờ xác nhận từ Senior"] = new[] { "Đang chờ Người chăm sóc thanh toán cọc", "Hủy" },
                    ["Đang chờ Người chăm sóc thanh toán cọc"] = new[] { "Đang thực hiện", "Hủy" },
                    ["Đang thực hiện"] = new[] { "Đang chờ xác nhận hoàn thành", "Hủy" },
                    ["Đang chờ xác nhận hoàn thành"] = new[] { "Hoàn thành", "Đang thực hiện" }
                }
            };

            return validTransitions.ContainsKey(userRole) &&
                   validTransitions[userRole].ContainsKey(currentStatus) &&
                   validTransitions[userRole][currentStatus].Contains(newStatus);
        }

        private async Task SendStatusUpdateNotification(CareJob job, string updatedByRole)
        {
            // Load the job with navigation properties
            job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .Include(j => j.Senior)
                .FirstOrDefaultAsync(j => j.Id == job.Id);

            if (job == null) return;

            var message = GetStatusUpdateMessage(job, updatedByRole);
            string targetUserId;

            if (updatedByRole == "Senior")
            {
                if (job.Caregiver == null)
                {
                    // If caregiver is not found, try to get it from the database
                    var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
                    targetUserId = caregiver?.UserId;
                }
                else
                {
                    targetUserId = job.Caregiver.UserId;
                }
            }
            else
            {
                if (job.Senior == null)
                {
                    // If senior is not found, try to get it from the database
                    var senior = await _context.Seniors.FindAsync(job.SeniorId);
                    targetUserId = senior?.UserId;
                }
                else
                {
                    targetUserId = job.Senior.UserId;
                }
            }

            if (string.IsNullOrEmpty(targetUserId)) return;

            var notification = new Notification
            {
                UserId = targetUserId,
                JobId = job.Id,
                Title = "Cập nhật trạng thái công việc",
                Message = message,
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "StatusUpdate",
                Link = $"/CareJobs/Details/{job.Id}"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            await _notificationHub.Clients.User(targetUserId).SendAsync("ReceiveNotification", message);
        }

        private string GetStatusUpdateMessage(CareJob job, string updatedByRole)
        {
            return updatedByRole switch
            {
                "Senior" => $"Khách hàng đã cập nhật trạng thái công việc #{job.Id} thành {job.Status}",
                "Caregiver" => $"Người chăm sóc đã cập nhật trạng thái công việc #{job.Id} thành {job.Status}",
                "Admin" => $"Admin đã cập nhật trạng thái công việc #{job.Id} thành {job.Status}",
                _ => $"Trạng thái công việc #{job.Id} đã được cập nhật thành {job.Status}"
            };
        }

        [HttpPost]
        public async Task<IActionResult> CancelJob(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .Include(j => j.Senior)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            if (!await CanAccessJob(job, userRole, userId))
                return AccessDenied();

            if (job.Status == "Hoàn thành" || job.Status == "Hủy")
            {
                TempData["ErrorMessage"] = "Không thể hủy công việc đã hoàn thành hoặc đã hủy.";
                return RedirectToAction("Details", new { id });
            }

            job.Status = "Hủy";
            job.UpdatedAt = DateTime.Now;

            // Cập nhật trạng thái của caregiver và senior
            if (job.Caregiver != null)
            {
                job.Caregiver.IsAvailable = true;
                _context.Caregivers.Update(job.Caregiver);
            }
            if (job.Senior != null)
            {
                job.Senior.Status = true; // Đánh dấu Senior đã sẵn sàng
                _context.Seniors.Update(job.Senior);
            }

            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                await SendStatusUpdateNotification(job, userRole);
                TempData["SuccessMessage"] = "Hủy công việc thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteJob(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .Include(j => j.Senior)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            if (!await CanAccessJob(job, userRole, userId))
                return AccessDenied();

            if (job.Status != "Đang thực hiện")
            {
                TempData["ErrorMessage"] = "Chỉ có thể hoàn thành công việc đang thực hiện.";
                return RedirectToAction("Details", new { id });
            }

            // Log trạng thái trước khi cập nhật
            Console.WriteLine($"Trước khi cập nhật - Job ID: {job.Id}");
            Console.WriteLine($"Caregiver IsAvailable: {job.Caregiver?.IsAvailable}");
            Console.WriteLine($"Senior Status: {job.Senior?.Status}");

            job.Status = "Hoàn thành";
            job.UpdatedAt = DateTime.Now;

            // Cập nhật trạng thái của caregiver và senior
            if (job.Caregiver != null)
            {
                job.Caregiver.IsAvailable = true; // Đánh dấu Caregiver sẵn sàng
                _context.Caregivers.Update(job.Caregiver);
                Console.WriteLine($"Đã cập nhật Caregiver {job.Caregiver.Id} thành sẵn sàng");
            }
            if (job.Senior != null)
            {
                job.Senior.Status = true; // Đánh dấu Senior sẵn sàng
                _context.Seniors.Update(job.Senior);
                Console.WriteLine($"Đã cập nhật Senior {job.Senior.Id} thành sẵn sàng");
            }

            try
            {
                // Lưu thay đổi cho job
                _context.Update(job);
                await _context.SaveChangesAsync();

                // Kiểm tra lại trạng thái sau khi lưu
                var updatedJob = await _context.CareJobs
                    .Include(j => j.Caregiver)
                    .Include(j => j.Senior)
                    .FirstOrDefaultAsync(j => j.Id == id);

                Console.WriteLine($"Sau khi cập nhật - Job ID: {updatedJob.Id}");
                Console.WriteLine($"Caregiver IsAvailable: {updatedJob.Caregiver?.IsAvailable}");
                Console.WriteLine($"Senior Status: {updatedJob.Senior?.Status}");

                await SendStatusUpdateNotification(job, userRole);
                TempData["SuccessMessage"] = "Hoàn thành công việc thành công.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật trạng thái: {ex.Message}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RateJob(int id, int rating, string comment)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs.FindAsync(id);
            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh giá công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            var ratingModel = new Rating
            {
                JobId = id,
                Stars = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _context.Ratings.Add(ratingModel);
            job.HasRated = true;
            _context.Update(job);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá dịch vụ!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ComplainJob(int id, string complaintReason)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs.FindAsync(id);
            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể khiếu nại công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            var complaint = new Complaint
            {
                JobId = id,
                Description = complaintReason,
                Status = "Chờ xử lý",
                CreatedAt = DateTime.Now
            };

            _context.Complaints.Add(complaint);
            job.HasComplained = true;
            _context.Update(job);

            // Tạo thông báo cho Admin
            var adminUsers = await _context.Users.Where(u => u.Role == "Admin").ToListAsync();
            foreach (var admin in adminUsers)
            {
                var notification = new Notification
                {
                    UserId = admin.Id,
                    JobId = job.Id,
                    Title = "Có khiếu nại mới",
                    Message = $"Có khiếu nại mới từ Senior về công việc #{job.Id}.\n" +
                              $"Người chăm sóc: {job.Caregiver?.FullName ?? "Chưa có"}\n" +
                              $"Nội dung: {complaintReason}",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Type = "Complaint"
                };
                _context.Notifications.Add(notification);

                // Gửi thông báo qua SignalR
                await _notificationHub.Clients.User(admin.Id).SendAsync("ReceiveNotification", notification.Message);
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Khiếu nại của bạn đã được gửi thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> RateCaregiver(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh giá công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id = jobId });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            var viewModel = new RateCaregiverViewModel
            {
                JobId = job.Id,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorId = senior.Id,
                CaregiverName = job.Caregiver?.FullName ?? "Người chăm sóc"
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RateCaregiver(RateCaregiverViewModel model)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == model.JobId);

            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh giá công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id = model.JobId });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            var rating = new Rating
            {
                JobId = job.Id,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorId = senior.Id,
                Stars = model.Stars,
                Review = model.Review,
                CreatedAt = DateTime.Now
            };

            _context.Ratings.Add(rating);
            job.HasRated = true;
            _context.Update(job);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá dịch vụ!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = model.JobId });
        }

        [HttpGet]
        public async Task<IActionResult> FileComplaint(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể khiếu nại công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id = jobId });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            var viewModel = new FileComplaintViewModel
            {
                JobId = job.Id,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorId = senior.Id,
                CaregiverName = job.Caregiver?.FullName ?? "Người chăm sóc"
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> FileComplaint(FileComplaintViewModel model)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            if (userRole != "Senior")
                return AccessDenied();

            var job = await _context.CareJobs
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == model.JobId);

            if (job == null) return NotFound();

            if (job.Status != "Hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể khiếu nại công việc đã hoàn thành.";
                return RedirectToAction("Details", new { id = model.JobId });
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id)
                return AccessDenied();

            string imagePath = null;
            string thumbnailPath = null;

            if (model.ImageFile != null)
            {
                try
                {
                    // Tạo đường dẫn đầy đủ đến thư mục uploads
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "complaints");
                    
                    // Kiểm tra và tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Tạo tên file duy nhất
                    var uniqueFileName = DateTime.Now.Ticks + "_" + model.ImageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Lưu file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    // Lưu đường dẫn tương đối cho database
                    imagePath = "/uploads/complaints/" + uniqueFileName;
                    thumbnailPath = imagePath;

                    // Log thông tin để debug
                    Console.WriteLine($"File saved to: {filePath}");
                    Console.WriteLine($"Image path: {imagePath}");
                }
                catch (Exception ex)
                {
                    // Log lỗi
                    Console.WriteLine($"Error saving file: {ex.Message}");
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu ảnh. Vui lòng thử lại.";
                    return RedirectToAction("FileComplaint", new { jobId = model.JobId });
                }
            }

            var complaint = new Complaint
            {
                JobId = job.Id,
                CaregiverId = job.CaregiverId ?? 0,
                SeniorId = senior.Id,
                Description = model.Description,
                Status = "Chờ xử lý",
                CreatedAt = DateTime.Now,
                ImagePath = imagePath,
                ThumbnailPath = thumbnailPath
            };

            _context.Complaints.Add(complaint);
            job.HasComplained = true;
            _context.Update(job);

            // Tạo thông báo cho Admin
            var adminUsers = await _context.Users.Where(u => u.Role == "Admin").ToListAsync();
            foreach (var admin in adminUsers)
            {
                var notification = new Notification
                {
                    UserId = admin.Id,
                    JobId = job.Id,
                    Title = "Có khiếu nại mới",
                    Message = $"Có khiếu nại mới từ Senior về công việc #{job.Id}.\n" +
                              $"Người chăm sóc: {job.Caregiver?.FullName ?? "Chưa có"}\n" +
                              $"Nội dung: {model.Description}",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Type = "Complaint"
                };
                _context.Notifications.Add(notification);

                // Gửi thông báo qua SignalR
                await _notificationHub.Clients.User(admin.Id).SendAsync("ReceiveNotification", notification.Message);
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Khiếu nại của bạn đã được gửi thành công!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving complaint: {ex.Message}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = model.JobId });
        }

        [HttpGet("api/carejobs/active")]
        public IActionResult CheckActiveJobs()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var activeJobs = _context.CareJobs
                .Include(j => j.Caregiver)
                .Include(j => j.Senior)
                .Where(j => (j.Caregiver.UserId == userId || j.Senior.UserId == userId) && 
                            (j.Status == "Đang chờ" || j.Status == "Đang thực hiện"))
                .ToList();

            return Json(new { hasActiveJob = activeJobs.Any() });
        }

        [HttpGet]
        public IActionResult BookCaregiver(int id)
        {
            var caregiver = _context.Caregivers
                .Include(c => c.User)
                .FirstOrDefault(c => c.Id == id);

            if (caregiver == null)
            {
                return NotFound();
            }

            var ratings = _context.Ratings
                .Where(r => r.CaregiverId == caregiver.Id)
                .ToList();

            var viewModel = new BookCaregiverViewModel
            {
                CaregiverId = caregiver.Id,
                CaregiverName = caregiver.FullName,
                CaregiverAvatar = caregiver.AvatarUrl,
                CaregiverSkills = caregiver.Skills,
                ServicePrice = caregiver.Price,
                CaregiverRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0,
                CaregiverRatingCount = ratings.Count,
                StartTime = DateTime.Now,
                NumberOfHours = 1
            };

            var userId = HttpContext.Session.GetString("UserId");
            ViewBag.SeniorPhone = _context.Users.FirstOrDefault(u => u.Id == userId)?.PhoneNumber ?? "Chưa có số điện thoại";

            return View("~/Views/Caregivers/BookCaregiver.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> BookCaregiver(BookCaregiverViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var caregiver = await _context.Caregivers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == model.CaregiverId);

            if (caregiver == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetString("UserId");
            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (senior == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin Người cần chăm sóc.";
                return RedirectToAction("Index", "Home");
            }

            // Create new job
            var job = new CareJob
            {
                SeniorId = senior.Id,
                CaregiverId = caregiver.Id,
                SeniorName = senior.Name,
                CaregiverName = caregiver.FullName,
                SeniorPhone = senior.User.PhoneNumber,
                StartTime = model.StartTime,
                EndTime = model.StartTime.AddHours(model.NumberOfHours),
                Status = "Chờ thanh toán",
                ServiceType = model.ServiceType,
                TotalBill = model.ServicePrice * model.NumberOfHours,
                CreatedByRole = "Senior",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            job.Deposit = job.TotalBill * 0.3m; // 30% deposit
            job.RemainingAmount = job.TotalBill - job.Deposit;

            _context.CareJobs.Add(job);

            // Create notification for caregiver
            var notification = new Notification
            {
                UserId = caregiver.UserId,
                Title = "Yêu cầu chăm sóc mới",
                Message = $"Bạn có yêu cầu chăm sóc mới từ {senior.Name}",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "Booking"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Send real-time notification
            await _notificationHub.Clients.User(caregiver.UserId).SendAsync("ReceiveNotification", notification.Message);

            TempData["SuccessMessage"] = "Đã gửi yêu cầu chăm sóc thành công!";
            return RedirectToAction("Index", "Caregivers");
        }

        private IActionResult AccessDenied() => View("~/Views/Shared/AccessDenied.cshtml");
    }
}