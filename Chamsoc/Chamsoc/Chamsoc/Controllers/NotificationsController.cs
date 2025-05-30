using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chamsoc.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Linq;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Caregiver,Senior,Admin")]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public NotificationsController(AppDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationHub = notificationHub;
        }

        // Hiển thị danh sách thông báo của Caregiver, Senior hoặc Admin

        
        public async Task<IActionResult> Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            // Lọc thông báo theo vai trò
            IQueryable<Notification> notificationsQuery = _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead);

            var notifications = await notificationsQuery
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // Lấy danh sách công việc liên quan
            if (userRole == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
                if (caregiver == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người chăm sóc.";
                    return RedirectToAction("Index", "Home");
                }

                var jobs = await _context.CareJobs
                    .Include(j => j.Senior)
                    .Include(j => j.Notifications)
                    .Where(j => j.CaregiverId == caregiver.Id)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();
                ViewBag.Jobs = jobs;
            }
            else if (userRole == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == userId);
                if (senior == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                    return RedirectToAction("Index", "Home");
                }

                var jobs = await _context.CareJobs
                    .Include(j => j.Caregiver)
                    .Include(j => j.Notifications)
                    .Where(j => j.SeniorId == senior.Id)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();
                ViewBag.Jobs = jobs;
            }
            else if (userRole == "Admin")
            {
                var jobs = await _context.CareJobs
                    .Include(j => j.Senior)
                    .Include(j => j.Caregiver)
                    .Include(j => j.Notifications)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();
                ViewBag.Jobs = jobs;
            }

            return View(notifications);
        }

        // Hiển thị chi tiết thông báo với thông tin Senior
        [HttpGet]
        public async Task<IActionResult> ViewNotification(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
            {
                return NotFound("Không tìm thấy thông báo");
            }

            // Đánh dấu thông báo đã đọc
            notification.IsRead = true;
            await _context.SaveChangesAsync();

            // Xử lý chuyển hướng dựa trên loại thông báo
            if (notification.Type == "ContactRequest")
            {
                // Lấy seniorUserId từ Link
                var seniorUserId = notification.Link.Split('/').Last();
                // Chuyển hướng đến ViewContactRequest với seniorUserId
                return RedirectToAction("ViewContactRequest", "Caregivers", new { seniorUserId = seniorUserId });
            }
            else if (notification.JobId.HasValue)
            {
                return RedirectToAction("Details", "CareJobs", new { id = notification.JobId });
            }

            return RedirectToAction("Index");
        }

        // Xác nhận công việc từ thông báo (Tiếp nhận)
        [HttpPost]
        public async Task<IActionResult> ConfirmJob(int notificationId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver" || string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null || notification.UserId != userId)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông báo.";
                return RedirectToAction("Index");
            }

            // Tìm công việc liên quan đến thông báo bằng JobId
            var job = await _context.CareJobs.FindAsync(notification.JobId);
            if (job == null)
            {
                System.Diagnostics.Debug.WriteLine($"Không tìm thấy CareJob với JobId: {notification.JobId}");
                TempData["ErrorMessage"] = "Không tìm thấy công việc liên quan đến thông báo.";
                return RedirectToAction("Index");
            }

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
            {
                System.Diagnostics.Debug.WriteLine($"CaregiverId không khớp: Job CaregiverId = {job.CaregiverId}, Caregiver Id = {caregiver?.Id}");
                TempData["ErrorMessage"] = "Công việc không thuộc về bạn.";
                return RedirectToAction("Index");
            }

            if (job.Status != "Đang chờ")
            {
                System.Diagnostics.Debug.WriteLine($"Trạng thái công việc không phải Đang chờ: Status = {job.Status}");
                TempData["ErrorMessage"] = "Công việc đã được xử lý (không còn ở trạng thái Đang chờ).";
                return RedirectToAction("Index");
            }

            // Đánh dấu thông báo là đã đọc
            notification.IsRead = true;
            _context.Notifications.Update(notification);

            // Tạo thông báo cho Senior
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
                        Message = $"Người chăm sóc đã xác nhận công việc của bạn.\n" +
                                  $"- Dịch vụ: {job.ServiceType}\n" +
                                  $"- Thời gian: {job.StartTime?.ToString("dd/MM/yyyy HH:mm")}\n" +
                                  $"Công việc sẽ bắt đầu vào thời gian đã chọn.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "CareJob" // Thêm Type để phân biệt thông báo
                    };
                    _context.Notifications.Add(seniorNotification);

                    // Gửi thông báo qua SignalR cho Senior
                    await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", seniorNotification.Message);
                }
            }

            // Tạo thông báo cho Caregiver
            var caregiverNotification = new Notification
            {
                UserId = userId,
                JobId = job.Id,
                Message = $"Bạn đã xác nhận công việc.\n" +
                          $"- Dịch vụ: {job.ServiceType}\n" +
                          $"- Thời gian: {job.StartTime?.ToString("dd/MM/yyyy HH:mm")}\n" +
                          $"Vui lòng nạp cọc để tiếp tục.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "CareJob" // Thêm Type để phân biệt thông báo
            };
            _context.Notifications.Add(caregiverNotification);

            // Gửi thông báo qua SignalR cho Caregiver
            await _notificationHub.Clients.User(userId).SendAsync("ReceiveNotification", caregiverNotification.Message);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã xác nhận công việc. Vui lòng nạp cọc để tiếp tục.";
            return RedirectToAction("Deposit", "Caregivers", new { jobId = job.Id });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã đánh dấu tất cả thông báo là đã đọc.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ListSeniors", "Seniors");
        }
        
        // Từ chối công việc từ thông báo
        [HttpPost]
        public async Task<IActionResult> RejectJob(int notificationId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userRole) || userRole != "Caregiver" || string.IsNullOrEmpty(userId))
            {
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null || notification.UserId != userId)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông báo.";
                return RedirectToAction("Index");
            }

            // Tìm công việc liên quan đến thông báo
            var job = await _context.CareJobs.FindAsync(notification.JobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy công việc liên quan đến thông báo.";
                return RedirectToAction("Index");
            }

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (caregiver == null || job.CaregiverId != caregiver.Id)
            {
                TempData["ErrorMessage"] = "Công việc không thuộc về bạn.";
                return RedirectToAction("Index");
            }

            if (job.Status != "Đang chờ")
            {
                TempData["ErrorMessage"] = "Công việc đã được xử lý (không còn ở trạng thái Đang chờ).";
                return RedirectToAction("Index");
            }

            // Cập nhật trạng thái công việc thành "Hủy"
            job.Status = "Hủy";
            _context.CareJobs.Update(job);

            // Tạo thông báo cho Senior
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
                        Message = $"Người chăm sóc đã từ chối công việc của bạn.\n" +
                                  $"- Dịch vụ: {job.ServiceType}\n" +
                                  $"- Thời gian: {job.StartTime?.ToString("dd/MM/yyyy HH:mm")}\n" +
                                  $"Vui lòng chọn người chăm sóc khác.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "CareJob" // Thêm Type để phân biệt thông báo
                    };
                    _context.Notifications.Add(seniorNotification);

                    // Gửi thông báo qua SignalR cho Senior
                    await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", seniorNotification.Message);
                }
            }

            // Tạo thông báo cho Caregiver
            var caregiverNotification = new Notification
            {
                UserId = userId,
                JobId = job.Id,
                Message = $"Bạn đã từ chối công việc.\n" +
                          $"- Dịch vụ: {job.ServiceType}\n" +
                          $"- Thời gian: {job.StartTime?.ToString("dd/MM/yyyy HH:mm")}\n" +
                          $"Công việc đã được hủy.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "CareJob" // Thêm Type để phân biệt thông báo
            };
            _context.Notifications.Add(caregiverNotification);

            // Gửi thông báo qua SignalR cho Caregiver
            await _notificationHub.Clients.User(userId).SendAsync("ReceiveNotification", caregiverNotification.Message);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã từ chối công việc.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> SendContactRequest(int caregiverId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng.";
                return RedirectToAction("ListCaregivers", "Caregivers");
            }

            var caregiver = await _context.Caregivers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == caregiverId);
            if (caregiver == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người chăm sóc.";
                return RedirectToAction("ListCaregivers", "Caregivers");
            }

            ViewBag.CaregiverId = caregiverId;
            ViewBag.CaregiverName = caregiver.User.FullName;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendContactRequest(int caregiverId, string message)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var senior = await _context.Seniors
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (senior == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng.";
                return RedirectToAction("ListCaregivers", "Caregivers");
            }

            var caregiver = await _context.Caregivers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == caregiverId);
            if (caregiver == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người chăm sóc.";
                return RedirectToAction("ListCaregivers", "Caregivers");
            }

            var notification = new Notification
            {
                UserId = caregiver.UserId,
                Title = "Yêu cầu liên hệ mới",
                Message = $"Bạn có yêu cầu liên hệ mới từ {senior.User.FullName}.\n" +
                         $"Thông tin người gửi:\n" +
                         $"- Tên: {senior.User.FullName}\n" +
                         $"- Số điện thoại: {senior.User.PhoneNumber}\n" +
                         $"- Địa chỉ: {senior.User.Address}\n\n" +
                         $"Lời nhắn:\n{message?.Trim() ?? "Không có nội dung"}",
                Type = "ContactRequest",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Link = $"/Caregivers/ViewContactRequest/{senior.UserId}"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Gửi thông báo realtime qua SignalR
            await _notificationHub.Clients.User(caregiver.UserId)
                .SendAsync("ReceiveNotification", new
                {
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type,
                    link = notification.Link
                });

            TempData["SuccessMessage"] = "Yêu cầu liên hệ đã được gửi thành công.";
            return RedirectToAction("ListCaregivers", "Caregivers");
        }
    }
}