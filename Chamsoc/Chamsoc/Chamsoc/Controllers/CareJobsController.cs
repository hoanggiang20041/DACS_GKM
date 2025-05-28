using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chamsoc.Hubs;

using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chamsoc.Models;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Senior,Caregiver,Admin")]
    public class CareJobsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public CareJobsController(AppDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub;
        }

        public class CareJobViewModel
        {
            public CareJob Job { get; set; }
            public bool HasRated { get; set; }
            public bool HasComplained { get; set; }
            public string CaregiverUserId { get; set; } // UserId của Caregiver từ AspNetUsers
            public string SeniorUserId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public async Task<IActionResult> Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            IQueryable<CareJob> careJobsQuery;

            if (userRole == "Admin")
            {
                careJobsQuery = _context.CareJobs;
            }
            else if (userRole == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
                if (senior == null) return RedirectWithError("Không tìm thấy thông tin khách hàng.");
                careJobsQuery = _context.CareJobs.Where(job => job.SeniorId == senior.Id);
            }
            else
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                if (caregiver == null) return RedirectWithError("Không tìm thấy thông tin người chăm sóc.");
                careJobsQuery = _context.CareJobs.Where(job => job.CaregiverId == caregiver.Id);
            }

            var careJobs = await careJobsQuery.ToListAsync();

            foreach (var job in careJobs)
            {
                if (job.Status == "Đang thực hiện" && job.EndTime.HasValue && DateTime.Now > job.EndTime.Value.AddSeconds(30))
                {
                    job.Status = "Hoàn tất";
                    job.EndTime = DateTime.Now;
                    job.RemainingAmount = job.TotalBill - job.Deposit;
                    UpdateAvailability(job);
                    _context.CareJobs.Update(job);
                }
            }
            await _context.SaveChangesAsync();

            var careJobViewModels = new List<CareJobViewModel>();
            foreach (var job in careJobs)
            {
                bool hasRated = false, hasComplained = false;
                string caregiverUserId = null;
                string seniorUserId = null;

                if (userRole == "Senior")
                {
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (senior != null)
                    {
                        hasRated = await _context.Ratings.AnyAsync(r => r.JobId == job.Id && r.SeniorId == senior.Id);
                        hasComplained = await _context.Complaints.AnyAsync(c => c.JobId == job.Id && c.SeniorId == senior.Id);
                    }
                }
                else if (userRole == "Admin")
                {
                    hasRated = await _context.Ratings.AnyAsync(r => r.JobId == job.Id && r.SeniorId == 0);
                }

                // Lấy UserId từ bảng Caregivers và Seniors
                caregiverUserId = await _context.Caregivers
                    .Where(c => c.Id == job.CaregiverId)
                    .Select(c => c.UserId)
                    .FirstOrDefaultAsync();
                seniorUserId = await _context.Seniors
                    .Where(s => s.Id == job.SeniorId)
                    .Select(s => s.UserId)
                    .FirstOrDefaultAsync();

                careJobViewModels.Add(new CareJobViewModel
                {
                    Job = job,
                    HasRated = hasRated,
                    HasComplained = hasComplained,
                    CaregiverUserId = caregiverUserId,
                    SeniorUserId = seniorUserId,
                    Latitude = job.Latitude,   // Thêm dòng này
                    Longitude = job.Longitude
                });
            }

            return View(careJobViewModels);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TransactionStats(int? month, int? year)
        {
            var filterMonth = month ?? DateTime.Now.Month;
            var filterYear = year ?? DateTime.Now.Year;

            var careJobs = await _context.CareJobs
                .Where(j => j.StartTime.Year == filterYear && j.StartTime.Month == filterMonth)
                .ToListAsync();

            var transactionStats = new List<TransactionStatsViewModel>();

            foreach (var job in careJobs)
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.Id == job.SeniorId);
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.Id == job.CaregiverId);

                var seniorUser = senior != null ? await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId) : null;
                var caregiverUser = caregiver != null ? await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId) : null;

                transactionStats.Add(new TransactionStatsViewModel
                {
                    SeniorName = senior?.Name ?? "Không xác định",
                    CaregiverName = caregiver?.Name ?? "Không xác định",
                    TransactionDateTime = job.EndTime.HasValue ? job.EndTime.Value : job.StartTime,
                    SeniorPhone = seniorUser?.PhoneNumber ?? "N/A",
                    SeniorEmail = seniorUser?.Email ?? "N/A",
                    CaregiverPhone = caregiverUser?.PhoneNumber ?? "N/A",
                    CaregiverEmail = caregiverUser?.Email ?? "N/A",
                    ServiceName = job.ServiceType ?? "Không xác định",
                    TotalBill = job.TotalBill,
                    Deposit = job.Deposit,
                    Status = job.Status
                });
            }

            // Dữ liệu cho biểu đồ tròn (phân bố trạng thái)
            var statusCounts = transactionStats
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            // Dữ liệu cho biểu đồ cột (doanh thu và số đơn của Caregiver)
            var caregiverStats = transactionStats
                .GroupBy(t => t.CaregiverName)
                .Select(g => new
                {
                    CaregiverName = g.Key,
                    TotalRevenue = g.Sum(t => t.TotalBill),
                    OrderCount = g.Count()
                })
                .OrderByDescending(g => g.TotalRevenue)
                .ToList();

            ViewBag.StatusLabels = statusCounts.Select(s => s.Status).ToList();
            ViewBag.StatusData = statusCounts.Select(s => s.Count).ToList();
            ViewBag.CaregiverNames = caregiverStats.Select(c => c.CaregiverName).ToList();
            ViewBag.CaregiverRevenue = caregiverStats.Select(c => c.TotalRevenue).ToList();
            ViewBag.CaregiverOrderCount = caregiverStats.Select(c => c.OrderCount).ToList();
            ViewBag.FilterMonth = filterMonth;
            ViewBag.FilterYear = filterYear;

            return View(transactionStats);
        }

        [HttpPost]
        public async Task<IActionResult> CompleteJob(int id)
        {
            var job = await _context.CareJobs.FindAsync(id);
            if (job == null) return NotFound();
            if (!IsAuthorizedUser(job)) return AccessDenied();

            if (job.Status != "Đang thực hiện") return RedirectWithError("Công việc không ở trạng thái Đang thực hiện.");

            job.Status = "Hoàn tất";
            job.EndTime = DateTime.Now;
            job.RemainingAmount = job.TotalBill - job.Deposit;
            UpdateAvailability(job);
            _context.CareJobs.Update(job);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Senior
            var senior = await _context.Seniors.FindAsync(job.SeniorId);
            if (senior != null)
            {
                var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
                if (seniorUser != null)
                {
                    var seniorNotification = new Notification
                    {
                        UserId = seniorUser.Id,
                        JobId = job.Id,
                        Message = $"Công việc #{job.Id} đã hoàn tất. Dịch vụ: {job.ServiceType}, Thời gian: {job.EndTime?.ToString("dd/MM/yyyy HH:mm")}.",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    _context.Notifications.Add(seniorNotification);
                    await _context.SaveChangesAsync();
                    await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", seniorNotification.Message);
                }
            }

            // Gửi thông báo cho Caregiver
            var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
            if (caregiver != null)
            {
                var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId && u.Role == "Caregiver");
                if (caregiverUser != null)
                {
                    var caregiverNotification = new Notification
                    {
                        UserId = caregiverUser.Id,
                        JobId = job.Id,
                        Message = $"Công việc #{job.Id} đã hoàn tất. Dịch vụ: {job.ServiceType}, Thời gian: {job.EndTime?.ToString("dd/MM/yyyy HH:mm")}.",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    _context.Notifications.Add(caregiverNotification);
                    await _context.SaveChangesAsync();
                    await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", caregiverNotification.Message);
                }
            }

            TempData["SuccessMessage"] = "Công việc đã được xác nhận hoàn tất.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CancelJob(int id)
        {
            var job = await _context.CareJobs.FindAsync(id);
            if (job == null) return NotFound();
            if (!IsAuthorizedUser(job)) return AccessDenied();

            if (job.Status != "Đang chờ" && job.Status != "Đang chờ Người chăm sóc thanh toán cọc" && job.Status != "Đang thực hiện")
                return RedirectWithError("Không thể hủy công việc đã hoàn tất hoặc đã bị hủy.");

            job.Status = "Hủy";
            UpdateAvailability(job);
            await SendCancellationNotifications(job);
            _context.CareJobs.Update(job);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Công việc đã được hủy.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Senior,Caregiver")]
        public async Task<IActionResult> AcceptJob(int id, double latitude = 0, double longitude = 0)
        {
            var job = await _context.CareJobs.FindAsync(id);
            if (job == null || job.Status != "Đang chờ")
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ.");

            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (userRole == "Senior")
            {
                if (!IsSenior(job)) return RedirectWithError("Công việc không thuộc về bạn.");
                if (job.SeniorAccepted) return RedirectWithError("Bạn đã chấp nhận công việc này rồi.");
                if (job.CreatedByRole != "Caregiver") return RedirectWithError("Bạn không cần chấp nhận công việc do mình tạo.");

                // Kiểm tra vị trí khi Senior chấp nhận yêu cầu từ Caregiver
                if (job.CreatedByRole == "Caregiver" && (latitude == 0 || longitude == 0))
                {
                    TempData["ErrorMessage"] = "Vui lòng cung cấp vị trí chăm sóc trước khi chấp nhận.";
                    return RedirectToAction("Index");
                }

                job.SeniorAccepted = true;
                job.Latitude = latitude;
                job.Longitude = longitude;
                job.Status = "Đang chờ Người chăm sóc thanh toán cọc";
                await SendDepositNotification(job);
            }
            else if (userRole == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                if (caregiver == null || job.CaregiverId != caregiver.Id) return RedirectWithError("Công việc không thuộc về bạn.");
                if (job.CaregiverAccepted) return RedirectWithError("Bạn đã chấp nhận công việc này rồi.");
                if (job.CreatedByRole != "Senior") return RedirectWithError("Bạn không cần chấp nhận công việc do mình tạo.");

                job.CaregiverAccepted = true;
                job.Status = "Đang chờ Người chăm sóc thanh toán cọc";
                await SendDepositNotification(job);
            }

            _context.CareJobs.Update(job);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã chấp nhận công việc.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Senior")]
        public async Task<IActionResult> RejectJob(int id)
        {
            var job = await _context.CareJobs.FindAsync(id);
            if (job == null || job.Status != "Đang chờ")
                return RedirectWithError("Công việc không hợp lệ hoặc không ở trạng thái chờ.");
            if (!IsSenior(job)) return RedirectWithError("Công việc không thuộc về bạn.");
            if (job.CreatedByRole != "Caregiver") return RedirectWithError("Bạn không thể từ chối công việc do mình tạo.");

            job.Status = "Hủy";
            UpdateAvailability(job);
            await SendCancellationNotifications(job);
            _context.CareJobs.Update(job);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã từ chối công việc.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> RateCaregiver(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId) || (userRole != "Senior" && userRole != "Admin"))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Hoàn tất") return RedirectWithError("Công việc không hợp lệ hoặc chưa hoàn tất.");

            int? raterId = userRole == "Senior" ? _context.Seniors.FirstOrDefault(s => s.UserId == userId)?.Id : 0;
            if (userRole == "Senior" && (raterId == null || job.SeniorId != raterId)) return RedirectWithError("Bạn không có quyền đánh giá công việc này.");

            var existingRating = await _context.Ratings.FirstOrDefaultAsync(r => r.JobId == jobId && (r.SeniorId == raterId || (userRole == "Admin" && r.SeniorId == 0)));
            if (existingRating != null) return RedirectWithError("Công việc này đã được đánh giá.");

            var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
            if (caregiver == null) return RedirectWithError("Không tìm thấy thông tin người chăm sóc.");

            var viewModel = new RateCaregiverViewModel
            {
                JobId = job.Id,
                CaregiverId = caregiver.Id,
                CaregiverName = caregiver.Name,
                SeniorId = raterId ?? 0
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RateCaregiver(RateCaregiverViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId) || (userRole != "Senior" && userRole != "Admin"))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var job = await _context.CareJobs.FindAsync(model.JobId);
            if (job == null || job.Status != "Hoàn tất") return RedirectWithError("Công việc không hợp lệ hoặc chưa hoàn tất.");

            int? raterId = userRole == "Senior" ? _context.Seniors.FirstOrDefault(s => s.UserId == userId)?.Id : 0;
            if (userRole == "Senior" && (raterId == null || job.SeniorId != raterId)) return RedirectWithError("Bạn không có quyền đánh giá công việc này.");

            var existingRating = await _context.Ratings.FirstOrDefaultAsync(r => r.JobId == model.JobId && (r.SeniorId == raterId || (userRole == "Admin" && r.SeniorId == 0)));
            if (existingRating != null) return RedirectWithError("Công việc này đã được đánh giá.");

            var rating = new Rating
            {
                JobId = model.JobId,
                CaregiverId = model.CaregiverId,
                SeniorId = raterId ?? 0,
                Stars = model.Stars,
                Review = model.Review,
                CreatedAt = DateTime.Now
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đánh giá của bạn đã được gửi thành công.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> FileComplaint(int jobId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || userRole != "Senior" || string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null || job.Status != "Hoàn tất") return RedirectWithError("Công việc không hợp lệ hoặc chưa hoàn tất.");

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id) return RedirectWithError("Bạn không có quyền khiếu nại công việc này.");

            var existingComplaint = await _context.Complaints.AnyAsync(c => c.JobId == jobId && c.SeniorId == senior.Id);
            if (existingComplaint) return RedirectWithError("Bạn đã gửi khiếu nại cho công việc này rồi.");

            var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
            if (caregiver == null) return RedirectWithError("Không tìm thấy thông tin người chăm sóc.");

            var viewModel = new FileComplaintViewModel
            {
                JobId = job.Id,
                CaregiverId = caregiver.Id,
                CaregiverName = caregiver.Name,
                SeniorId = senior.Id
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> FileComplaint(FileComplaintViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || userRole != "Senior" || string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var job = await _context.CareJobs.FindAsync(model.JobId);
            if (job == null || job.Status != "Hoàn tất") return RedirectWithError("Công việc không hợp lệ hoặc chưa hoàn tất.");

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null || job.SeniorId != senior.Id) return RedirectWithError("Bạn không có quyền khiếu nại công việc này.");

            var existingComplaint = await _context.Complaints.AnyAsync(c => c.JobId == model.JobId && c.SeniorId == senior.Id);
            if (existingComplaint) return RedirectWithError("Bạn đã gửi khiếu nại cho công việc này rồi.");

            var complaint = new Complaint
            {
                JobId = model.JobId,
                CaregiverId = model.CaregiverId,
                SeniorId = senior.Id,
                Description = model.Description,
                Status = "Pending",
                CreatedAt = DateTime.Now
                // Resolution để null, sẽ được Admin cập nhật sau
            };

            _context.Complaints.Add(complaint);

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
            if (admin != null)
            {
                var adminNotification = new Notification
                {
                    UserId = admin.Id,
                    JobId = complaint.JobId,
                    Message = $"Có khiếu nại mới từ khách hàng về công việc #{complaint.JobId}: {model.Description}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(adminNotification);
                await _notificationHub.Clients.User(admin.Id).SendAsync("ReceiveNotification", adminNotification.Message);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Khiếu nại của bạn đã được gửi thành công.";
            return RedirectToAction("Index");
        }

        private bool IsAuthorizedUser(CareJob job)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId)) return false;

            if (userRole == "Senior")
            {
                var senior = _context.Seniors.FirstOrDefault(s => s.UserId == userId);
                return senior != null && job.SeniorId == senior.Id;
            }
            else if (userRole == "Caregiver")
            {
                var caregiver = _context.Caregivers.FirstOrDefault(c => c.UserId == userId);
                return caregiver != null && job.CaregiverId == caregiver.Id;
            }
            return false;
        }

        private void UpdateAvailability(CareJob job)
        {
            var caregiver = _context.Caregivers.Find(job.CaregiverId);
            if (caregiver != null) { caregiver.IsAvailable = true; _context.Caregivers.Update(caregiver); }

            var senior = _context.Seniors.Find(job.SeniorId);
            if (senior != null) { senior.Status = true; _context.Seniors.Update(senior); }
        }

        private async Task SendCancellationNotifications(CareJob job)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Caregiver")
            {
                var senior = await _context.Seniors.FindAsync(job.SeniorId);
                if (senior != null)
                {
                    var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId);
                    if (seniorUser != null)
                    {
                        var notification = new Notification
                        {
                            UserId = seniorUser.Id,
                            JobId = job.Id,
                            Message = $"Người chăm sóc đã hủy công việc #{job.Id}.",
                            CreatedAt = DateTime.Now,
                            IsRead = false
                        };
                        _context.Notifications.Add(notification);
                        await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
                    }
                }
            }
            else if (userRole == "Senior")
            {
                var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
                if (caregiver != null)
                {
                    var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId);
                    if (caregiverUser != null)
                    {
                        var notification = new Notification
                        {
                            UserId = caregiverUser.Id,
                            JobId = job.Id,
                            Message = $"Khách hàng đã hủy công việc #{job.Id}.",
                            CreatedAt = DateTime.Now,
                            IsRead = false
                        };
                        _context.Notifications.Add(notification);
                        await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", notification.Message);
                    }
                }
            }
        }

        private async Task SendDepositNotification(CareJob job)
        {
            var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
            if (caregiver != null)
            {
                var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId);
                if (caregiverUser != null)
                {
                    var notification = new Notification
                    {
                        UserId = caregiverUser.Id,
                        JobId = job.Id,
                        Message = $"Công việc #{job.Id} đã được cả hai bên chấp nhận. Vui lòng nạp cọc để bắt đầu.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "CareJob"
                    };
                    _context.Notifications.Add(notification);
                    await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", notification.Message);
                }
            }
        }

        private async Task SendNotificationToCaregiver(CareJob job, string acceptedBy)
        {
            var caregiver = await _context.Caregivers.FindAsync(job.CaregiverId);
            if (caregiver != null)
            {
                var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId);
                if (caregiverUser != null)
                {
                    var notification = new Notification
                    {
                        UserId = caregiverUser.Id,
                        JobId = job.Id,
                        Message = $"{acceptedBy} đã chấp nhận công việc #{job.Id}. Vui lòng xác nhận để tiếp tục.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "CareJob"
                    };
                    _context.Notifications.Add(notification);
                    await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", notification.Message);
                }
            }
        }

        private async Task SendNotificationToSenior(CareJob job, string acceptedBy)
        {
            var senior = await _context.Seniors.FindAsync(job.SeniorId);
            if (senior != null)
            {
                var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId);
                if (seniorUser != null)
                {
                    var notification = new Notification
                    {
                        UserId = seniorUser.Id,
                        JobId = job.Id,
                        Message = $"{acceptedBy} đã chấp nhận công việc #{job.Id}. Vui lòng xác nhận để tiếp tục.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "CareJob"
                    };
                    _context.Notifications.Add(notification);
                    await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
                }
            }
        }

        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("Index");
        }

        private IActionResult AccessDenied() => View("~/Views/Shared/AccessDenied.cshtml");
        private bool IsSenior               (CareJob job) => _context.Seniors.FirstOrDefault(s => s.UserId == HttpContext.Session.GetString("UserId"))?.Id == job.SeniorId;
    }
}