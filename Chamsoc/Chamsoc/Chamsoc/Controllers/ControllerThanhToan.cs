using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Chamsoc.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using Chamsoc.Models;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ControllerThanhToan : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public ControllerThanhToan(AppDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _notificationHub = notificationHub;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ApprovePayments()
        {
            var allPayments = _context.CareJobs
                .OrderByDescending(j => j.StartTime)
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .ToList();
            return View(allPayments);
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePayment(int jobId)
        {
            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            job.Status = "Đang thực hiện";
            job.PaymentStatus = "Completed";
            job.PaymentTime = DateTime.Now;
            _context.Update(job);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã duyệt thanh toán và chuyển sang trạng thái đang thực hiện thành công!";
            return RedirectToAction(nameof(ApprovePayments));
        }

        [HttpPost]
        public async Task<IActionResult> RejectPayment(int jobId)
        {
            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            job.Status = "Rejected";
            job.PaymentStatus = "Failed";
            job.PaymentTime = DateTime.Now;
            _context.Update(job);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã từ chối thanh toán thành công!";
            return RedirectToAction(nameof(ApprovePayments));
        }

        [HttpGet]
        public IActionResult ManageUsers(string search = null)
        {
            var users = _context.Users.AsQueryable();

            // Debug: Kiểm tra số lượng người dùng trước khi lọc
            System.Diagnostics.Debug.WriteLine($"Số lượng người dùng trước khi lọc: {users.Count()}");

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(u => (u.UserName != null && u.UserName.Contains(search)) ||
                                         (u.Email != null && u.Email.Contains(search)) ||
                                         (u.Role != null && u.Role.Contains(search)));
                System.Diagnostics.Debug.WriteLine($"Số lượng người dùng sau khi lọc tìm kiếm: {users.Count()}");
            }

            // Get the current user
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                System.Diagnostics.Debug.WriteLine("Current user is null. User not authenticated.");
                return RedirectToAction("Login", "Account");
            }

            // Pass the current user's ID and role to the view
            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            ViewBag.Search = search;
            return View(users.ToList());
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View("ProfileAdmin", user);
        }

        [HttpGet]
        public async Task<IActionResult> UpdateProfileAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Admin")
            {
                return NotFound();
            }

            ViewBag.User = user;
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileAdmin(string id, string email, string phoneNumber, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Admin")
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                if (!passwordCheck)
                {
                    TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                    ViewBag.User = user;
                    return View(user);
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!changePasswordResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                    ViewBag.User = user;
                    return View(user);
                }
            }

            if (user.Email != email)
            {
                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại. Vui lòng chọn email khác.";
                    ViewBag.User = user;
                    return View(user);
                }
                user.Email = email;
            }

            if (user.PhoneNumber != phoneNumber)
            {
                if (!string.IsNullOrEmpty(phoneNumber) && _context.Users.Any(u => u.PhoneNumber == phoneNumber && u.Id != user.Id))
                {
                    TempData["ErrorMessage"] = "Số điện thoại đã tồn tại. Vui lòng chọn số khác.";
                    ViewBag.User = user;
                    return View(user);
                }
                user.PhoneNumber = phoneNumber;
            }

            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> UpdateProfileCaregiver(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Caregiver")
            {
                return NotFound();
            }

            // Kiểm tra quyền: Chỉ Admin hoặc chính người dùng đó mới được phép chỉnh sửa
            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser?.Id != id)
            {
                return Forbid();
            }

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (caregiver == null)
            {
                return NotFound();
            }

            ViewBag.User = user;
            return View("UpdateProfileCaregiver", caregiver);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileCaregiver(string id, string name, string skills, string email, string contact, bool isAvailable, string currentPassword, string newPassword, IFormFile avatar, decimal price)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Caregiver")
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng hoặc vai trò không hợp lệ.";
                return RedirectToAction("Index");
            }

            // Kiểm tra quyền: Chỉ Admin hoặc chính người dùng đó mới được phép chỉnh sửa
            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser?.Id != id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa hồ sơ của người dùng này.";
                return RedirectToAction("Index");
            }

            // Xử lý đổi mật khẩu nếu có
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (User.IsInRole("Admin"))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var changePasswordResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        TempData["ErrorMessage"] = "Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu.";
                        ViewBag.User = user;
                        return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                    }

                    var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!passwordCheck)
                    {
                        TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                        ViewBag.User = user;
                        return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                    }

                    var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                    }
                }
            }

            // Cập nhật email nếu thay đổi
            if (user.Email != email)
            {
                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại. Vui lòng chọn email khác.";
                    ViewBag.User = user;
                    return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                }
                user.Email = email;
                await _userManager.UpdateAsync(user);
            }

            // Cập nhật số điện thoại nếu thay đổi
            if (contact != null && user.PhoneNumber != contact)
            {
                if (_context.Users.Any(u => u.PhoneNumber == contact && u.Id != user.Id))
                {
                    TempData["ErrorMessage"] = "Số điện thoại đã tồn tại. Vui lòng chọn số khác.";
                    ViewBag.User = user;
                    return View(await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id));
                }
                user.PhoneNumber = contact;
                await _userManager.UpdateAsync(user);
            }

            // Tìm và cập nhật thông tin Caregiver
            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (caregiver == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin Caregiver.";
                return RedirectToAction("Index");
            }

            // Xử lý upload avatar nếu có
            if (avatar != null && avatar.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(fileStream);
                }

                caregiver.AvatarUrl = $"/uploads/avatars/{fileName}";
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Giá mong muốn phải lớn hơn 0.";
                ViewBag.User = user;
                return View(caregiver);
            }

            // Cập nhật thông tin Caregiver
            caregiver.Name = name;
            caregiver.Skills = skills;
            caregiver.Contact = contact;
            caregiver.IsAvailable = isAvailable;
            caregiver.Price = price;

            try
            {
                _context.Caregivers.Update(caregiver);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                ViewBag.User = user;
                return View(caregiver);
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
            return RedirectToAction("Profile");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileSenior(string id, string name, int age, string careNeeds, bool status, string email, string contact, string currentPassword, string newPassword, IFormFile avatar, decimal price)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Senior")
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng hoặc vai trò không hợp lệ.";
                return RedirectToAction("Index");
            }

            // Kiểm tra quyền: Chỉ Admin hoặc chính người dùng đó mới được phép chỉnh sửa
            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser?.Id != id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa hồ sơ của người dùng này.";
                return RedirectToAction("Index");
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                if (User.IsInRole("Admin"))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var changePasswordResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        TempData["ErrorMessage"] = "Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu.";
                        ViewBag.User = user;
                        return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                    }

                    var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!passwordCheck)
                    {
                        TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                        ViewBag.User = user;
                        return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                    }

                    var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                    }
                }
            }

            if (user.Email != email)
            {
                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại. Vui lòng chọn email khác.";
                    ViewBag.User = user;
                    return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                }
                user.Email = email;
                await _userManager.UpdateAsync(user);
            }

            if (user.PhoneNumber != contact)
            {
                if (!string.IsNullOrEmpty(contact) && _context.Users.Any(u => u.PhoneNumber == contact && u.Id != user.Id))
                {
                    TempData["ErrorMessage"] = "Số điện thoại đã tồn tại. Vui lòng chọn số khác.";
                    ViewBag.User = user;
                    return View(await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id));
                }
                user.PhoneNumber = contact;
                await _userManager.UpdateAsync(user);
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (senior == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin Senior.";
                return RedirectToAction("Index");
            }

            if (avatar != null && avatar.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(fileStream);
                }

                senior.AvatarUrl = $"/uploads/avatars/{fileName}";
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Giá mong muốn phải lớn hơn 0.";
                ViewBag.User = user;
                return View(senior);
            }

            senior.Name = name;
            senior.Age = age;
            senior.CareNeeds = careNeeds;
            senior.Status = status;
            senior.Price = price; // Cập nhật giá mong muốn

            try
            {
                _context.Seniors.Update(senior);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                ViewBag.User = user;
                return View(senior);
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
            return RedirectToAction("Profile");
        }

        // Hiển thị danh sách khiếu nại
        // public async Task<IActionResult> ManageComplaints()
        // {
        //     var complaints = await _context.Complaints
        //         .Include(c => c.Job)
        //         .ToListAsync();
        //
        //     return View(complaints);
        // }

        // GET: Admin/HandleComplaint
        // [HttpGet]
        // public async Task<IActionResult> HandleComplaint(int id)
        // {
        //     var complaint = await _context.Complaints
        //         .Include(c => c.Job)
        //         .FirstOrDefaultAsync(c => c.Id == id);
        //
        //     if (complaint == null)
        //     {
        //         TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
        //         return RedirectToAction("ManageComplaints");
        //     }
        //
        //     var caregiver = await _context.Caregivers.FindAsync(complaint.CaregiverId);
        //     var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
        //
        //     var viewModel = new HandleComplaintViewModel
        //     {
        //         ComplaintId = complaint.Id,
        //         JobId = complaint.JobId,
        //         CaregiverId = complaint.CaregiverId,
        //         CaregiverName = caregiver?.Name,
        //         SeniorId = complaint.SeniorId,
        //         SeniorName = senior?.Name,
        //         Description = complaint.Description,
        //         Status = complaint.Status,
        //         Resolution = complaint.Resolution,
        //         ImagePath = complaint.ImagePath,
        //         ThumbnailPath = complaint.ThumbnailPath,
        //         CreatedAt = complaint.CreatedAt
        //     };
        //
        //     return View(viewModel);
        // }

        // POST: Admin/HandleComplaint
        // [HttpPost]
        // public async Task<IActionResult> HandleComplaint(HandleComplaintViewModel model)
        // {
        //     if (!ModelState.IsValid)
        //     {
        //         return View(model);
        //     }
        //
        //     var complaint = await _context.Complaints
        //         .Include(c => c.Job)
        //         .FirstOrDefaultAsync(c => c.Id == model.ComplaintId);
        //
        //     if (complaint == null)
        //     {
        //         TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
        //         return RedirectToAction("ManageComplaints");
        //     }
        //
        //     complaint.Status = model.Status;
        //     complaint.Resolution = model.Resolution;
        //     _context.Complaints.Update(complaint);
        //
        //     // Gửi thông báo cho Senior nếu khiếu nại đã được xử lý
        //     if (model.Status == "Resolved" || model.Status == "Dismissed")
        //     {
        //         var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
        //         if (senior != null)
        //         {
        //             var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
        //             if (seniorUser != null)
        //             {
        //                 var notification = new Notification
        //                 {
        //                     UserId = seniorUser.Id,
        //                     JobId = complaint.JobId,
        //                     Message = $"Khiếu nại của bạn về công việc #{complaint.JobId} đã được xử lý.\\n" +
        //                               $"- Trạng thái: {model.Status}\\n" +
        //                               $"- Giải quyết: {model.Resolution}",
        //                     CreatedAt = DateTime.Now,
        //                     IsRead = false
        //                 };
        //                 _context.Notifications.Add(notification);
        //
        //                 // Gửi thông báo qua SignalR
        //                 await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
        //             }
        //         }
        //     }
        //
        //     await _context.SaveChangesAsync();
        //
        //     TempData["SuccessMessage"] = "Khiếu nại đã được xử lý thành công.";
        //     return RedirectToAction("ManageComplaints");
        // }

        // GET: Admin/ResolveComplaint
        // [HttpGet]
        // public async Task<IActionResult> ResolveComplaint(int id)
        // {
        //     var complaint = await _context.Complaints
        //         .Include(c => c.Job)
        //         .FirstOrDefaultAsync(c => c.Id == id);
        //
        //     if (complaint == null)
        //     {
        //         TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
        //         return RedirectToAction("ManageComplaints");
        //     }
        //
        //     var job = await _context.CareJobs.FindAsync(complaint.JobId);
        //     if (job == null)
        //     {
        //         TempData["ErrorMessage"] = "Không tìm thấy công việc liên quan đến khiếu nại.";
        //         return RedirectToAction("ManageComplaints");
        //     }
        //
        //     var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
        //     var caregiver = await _context.Caregivers.FindAsync(complaint.CaregiverId);
        //
        //     var viewModel = new ComplaintViewModel
        //     {
        //         Complaint = complaint,
        //         Job = job,
        //         Senior = senior,
        //         Caregiver = caregiver
        //     };
        //
        //     return View(viewModel);
        // }

        // POST: Admin/ResolveComplaint
        // [HttpPost]
        // public async Task<IActionResult> ResolveComplaint(int id, string adminResponse)
        // {
        //     var complaint = await _context.Complaints
        //         .Include(c => c.Job)
        //         .FirstOrDefaultAsync(c => c.Id == id);
        //
        //     if (complaint == null)
        //     {
        //         TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
        //         return RedirectToAction("ManageComplaints");
        //     }
        //
        //     if (string.IsNullOrWhiteSpace(adminResponse))
        //     {
        //         TempData["ErrorMessage"] = "Vui lòng nhập phản hồi.";
        //         return RedirectToAction("ResolveComplaint", new { id });
        //     }
        //
        //     complaint.Status = "Đã xử lý";
        //     complaint.Resolution = adminResponse;
        //     _context.Complaints.Update(complaint);
        //
        //     // Gửi thông báo cho Senior
        //     var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
        //     if (senior != null)
        //     {
        //         var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
        //         if (seniorUser != null)
        //         {
        //             var notification = new Notification
        //             {
        //                 UserId = seniorUser.Id,
        //                 JobId = complaint.JobId,
        //                 Message = $"Khiếu nại của bạn về công việc #{complaint.JobId} đã được xử lý.\\n" +
        //                           $"- Trạng thái: Đã xử lý\\n" +
        //                           $"- Giải quyết: {adminResponse}",
        //                 CreatedAt = DateTime.Now,
        //                 IsRead = false
        //             };
        //             _context.Notifications.Add(notification);
        //
        //             // Gửi thông báo qua SignalR
        //             await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
        //         }
        //     }
        //
        //     await _context.SaveChangesAsync();
        //
        //     TempData["SuccessMessage"] = "Khiếu nại đã được xử lý thành công.";
        //     return RedirectToAction("ManageComplaints");
        // }

        [HttpGet]
        public IActionResult AddUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(string username, string email, string phoneNumber, string password, string role, string name, int? age, string careNeeds, bool? status, string skills, bool? isAvailable, IFormFile certificate, IFormFileCollection identityAndHealthDocs, decimal price)
        {
            // Kiểm tra định dạng email
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                TempData["ErrorMessage"] = $"Email không hợp lệ. Giá trị hiện tại: \"{email}\".";
                return View();
            }

            // Kiểm tra trùng lặp username
            if (await _userManager.FindByNameAsync(username) != null)
            {
                TempData["ErrorMessage"] = $"Tên đăng nhập đã tồn tại. Giá trị hiện tại: \"{username}\".";
                return View();
            }

            // Kiểm tra trùng lặp email
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                TempData["ErrorMessage"] = $"Email đã tồn tại. Giá trị hiện tại: \"{email}\".";
                return View();
            }

            // Kiểm tra trùng lặp số điện thoại
            if (!string.IsNullOrEmpty(phoneNumber) && _context.Users.Any(u => u.PhoneNumber == phoneNumber))
            {
                TempData["ErrorMessage"] = $"Số điện thoại đã tồn tại. Giá trị hiện tại: \"{phoneNumber}\".";
                return View();
            }

            // Kiểm tra các trường bắt buộc
            if (string.IsNullOrEmpty(name) || name.Trim() == "")
            {
                TempData["ErrorMessage"] = $"Vui lòng nhập họ và tên. Giá trị hiện tại: \"{name}\".";
                return View();
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = $"Giá mong muốn phải lớn hơn 0. Giá trị hiện tại: \"{price}\".";
                return View();
            }

            // Tạo đối tượng Senior hoặc Caregiver dựa trên vai trò
            string roleId;
            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                PhoneNumber = phoneNumber,
                Role = role,
                IsLocked = false,
                Balance = role == "Admin" ? 0 : 0 // Khởi tạo số dư cho Admin
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Thêm người dùng thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return View();
            }

            // Gán vai trò cho người dùng trong hệ thống Identity
            await _userManager.AddToRoleAsync(user, role);

            if (role == "Senior")
            {
                if (!age.HasValue || age <= 0)
                {
                    TempData["ErrorMessage"] = $"Vui lòng nhập tuổi hợp lệ. Giá trị hiện tại: \"{age}\".";
                    await _userManager.DeleteAsync(user); // Xóa user nếu có lỗi
                    return View();
                }
                if (string.IsNullOrEmpty(careNeeds))
                {
                    TempData["ErrorMessage"] = $"Vui lòng nhập nhu cầu chăm sóc. Giá trị hiện tại: \"{careNeeds}\".";
                    await _userManager.DeleteAsync(user); // Xóa user nếu có lỗi
                    return View();
                }

                // Xử lý tải lên nhiều file cho giấy tờ tùy thân và giấy khám sức khỏe
                string? identityAndHealthDocsPaths = null;
                if (identityAndHealthDocs != null && identityAndHealthDocs.Count > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/identityAndHealthDocs");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePaths = new List<string>();
                    foreach (var file in identityAndHealthDocs)
                    {
                        if (file.Length > 0)
                        {
                            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            filePaths.Add($"/uploads/identityAndHealthDocs/{fileName}");
                        }
                    }

                    // Lưu danh sách đường dẫn file dưới dạng chuỗi phân tách bằng dấu phẩy
                    identityAndHealthDocsPaths = string.Join(",", filePaths);
                }

                var senior = new Senior
                {
                    UserId = user.Id, // Lưu GUID từ AspNetUsers
                    Name = name,
                    Age = age.Value, // Use .Value since validation ensures age is not null
                    CareNeeds = careNeeds,
                    RegistrationDate = DateTime.Now,
                    Status = status ?? false, // Default to false if status is null
                    IsVerified = false,
                    AvatarUrl = "https://via.placeholder.com/150",
                    Price = price,
                    IdentityAndHealthDocs = identityAndHealthDocsPaths
                };
                _context.Seniors.Add(senior);
                await _context.SaveChangesAsync();
                roleId = senior.Id.ToString(); // Chuyển int thành string
            }
            else if (role == "Caregiver")
            {
                if (string.IsNullOrEmpty(skills))
                {
                    TempData["ErrorMessage"] = $"Vui lòng nhập kỹ năng chăm sóc. Giá trị hiện tại: \"{skills}\".";
                    await _userManager.DeleteAsync(user); // Xóa user nếu có lỗi
                    return View();
                }

                string? certificateFilePath = null;
                if (certificate != null && certificate.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/certificates");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(certificate.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await certificate.CopyToAsync(fileStream);
                    }

                    certificateFilePath = $"/uploads/certificates/{fileName}";
                }

                // Khởi tạo bảng giá mặc định dựa trên kỹ năng
                string? pricing = null;
                if (skills == "Khám toàn diện")
                {
                    pricing = "{\"1Hour\": 1500000, \"2Hours\": 2500000, \"5Sessions\": 6000000}";
                }
                else if (skills == "Trị liệu")
                {
                    pricing = "{\"1Hour\": 1200000, \"2Hours\": 2000000, \"5Sessions\": 5000000}";
                }

                var caregiver = new Caregiver
                {
                    UserId = user.Id, // Lưu GUID từ AspNetUsers
                    Name = name,
                    Skills = skills,
                    Contact = phoneNumber,
                    IsAvailable = isAvailable ?? false, // Default to false if isAvailable is null
                    CertificateFilePath = certificateFilePath,
                    IsVerified = false,
                    AvatarUrl = "https://via.placeholder.com/150",
                    Price = price,
                    Pricing = pricing
                };
                _context.Caregivers.Add(caregiver);
                await _context.SaveChangesAsync();
                roleId = caregiver.Id.ToString(); // Chuyển int thành string
            }
            else
            {
                TempData["ErrorMessage"] = $"Vai trò không hợp lệ. Giá trị hiện tại: \"{role}\".";
                await _userManager.DeleteAsync(user); // Xóa user nếu có lỗi
                return View();
            }

            // Cập nhật RoleId của user
            user.RoleId = roleId;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Thêm người dùng thành công!";
            return RedirectToAction("ManageUsers");
        }
    }
}