﻿using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Chamsoc.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Chamsoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public AdminController(AppDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _notificationHub = notificationHub;
        }

        public IActionResult Index()
        {
            // Trang dashboard admin, có thể thêm thống kê ở đây
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("ManageUsers");
            }

            if (user.Role == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (senior != null)
                {
                    _context.Seniors.Remove(senior);
                }
            }
            else if (user.Role == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                if (caregiver != null)
                {
                    _context.Caregivers.Remove(caregiver);
                }
            }

            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);

            TempData["SuccessMessage"] = "Xóa người dùng thành công!";
            return RedirectToAction("ManageUsers");
        }

        [HttpGet]
        public IActionResult ManageUsers(string search = null)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(u => (u.UserName != null && u.UserName.Contains(search)) ||
                                         (u.Email != null && u.Email.Contains(search)) ||
                                         (u.Role != null && u.Role.Contains(search)));
            }

            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            ViewBag.Search = search;
            return View(users.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("ManageUsers");
            }

            user.IsLocked = true;
            await _userManager.UpdateAsync(user);

            if (user.Role == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (senior != null)
                {
                    senior.IsVerified = false;
                    _context.Seniors.Update(senior);
                }
            }
            else if (user.Role == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                if (caregiver != null)
                {
                    caregiver.IsVerified = false;
                    _context.Caregivers.Update(caregiver);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Khóa tài khoản thành công!";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("ManageUsers");
            }

            user.IsLocked = false;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Mở khóa tài khoản thành công!";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("ManageUsers");
            }

            if (user.Role == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (senior != null)
                {
                    senior.IsVerified = true;
                    _context.Seniors.Update(senior);
                }
            }
            else if (user.Role == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                if (caregiver != null)
                {
                    caregiver.IsVerified = true;
                    _context.Caregivers.Update(caregiver);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xác minh người dùng thành công!";
            return RedirectToAction("ManageUsers");
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
                Caregiver = caregiver
            };

            ViewBag.User = user;
            return View("UpdateProfileCaregiver", viewModel);
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

        [HttpGet]
        public async Task<IActionResult> UpdateProfileSenior(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Senior")
            {
                return NotFound();
            }

            // Kiểm tra quyền: Chỉ Admin hoặc chính người dùng đó mới được phép chỉnh sửa
            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser?.Id != id)
            {
                return Forbid();
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (senior == null)
            {
                return NotFound();
            }

            ViewBag.User = user;
            return View("UpdateProfileSenior", senior);
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
        public async Task<IActionResult> ManageComplaints()
        {
            var complaints = await _context.Complaints
                .Include(c => c.Job)
                .ToListAsync();

            return View(complaints);
        }

        // GET: Admin/HandleComplaint
        [HttpGet]
        public async Task<IActionResult> HandleComplaint(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Job)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
                return RedirectToAction("ManageComplaints");
            }

            var caregiver = await _context.Caregivers.FindAsync(complaint.CaregiverId);
            var senior = await _context.Seniors.FindAsync(complaint.SeniorId);

            var viewModel = new HandleComplaintViewModel
            {
                ComplaintId = complaint.Id,
                JobId = complaint.JobId,
                CaregiverId = complaint.CaregiverId,
                CaregiverName = caregiver?.Name,
                SeniorId = complaint.SeniorId,
                SeniorName = senior?.Name,
                Description = complaint.Description,
                Status = complaint.Status,
                Resolution = complaint.Resolution,
                ImagePath = complaint.ImagePath,
                ThumbnailPath = complaint.ThumbnailPath,
                CreatedAt = complaint.CreatedAt,
                Complaint = complaint
            };

            return View(viewModel);
        }

        // POST: Admin/HandleComplaint
        [HttpPost]
        public async Task<IActionResult> HandleComplaint(HandleComplaintViewModel model)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Model NewStatus: {model.NewStatus}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Model NewResolution: {model.NewResolution}");

                // Lấy thông tin khiếu nại hiện tại
                var existingComplaint = await _context.Complaints
                    .Include(c => c.Job)
                    .FirstOrDefaultAsync(c => c.Id == model.ComplaintId);

                if (existingComplaint == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
                    return RedirectToAction("ManageComplaints");
                }

                // Cập nhật các trường bắt buộc cho model
                model.Complaint = existingComplaint;
                model.SeniorName = (await _context.Seniors.FindAsync(existingComplaint.SeniorId))?.Name;
                model.CaregiverName = (await _context.Caregivers.FindAsync(existingComplaint.CaregiverId))?.Name;
                model.Status = existingComplaint.Status;
                model.Resolution = existingComplaint.Resolution;
                model.ImagePath = existingComplaint.ImagePath;
                model.ThumbnailPath = existingComplaint.ThumbnailPath;

                // Chỉ validate NewStatus và NewResolution
                if (string.IsNullOrEmpty(model.NewStatus) || string.IsNullOrEmpty(model.NewResolution))
                {
                    ModelState.AddModelError("", "Vui lòng điền đầy đủ thông tin xử lý khiếu nại.");
                    return View(model);
                }

                // Cập nhật trạng thái và nội dung xử lý
                existingComplaint.Status = model.NewStatus;
                existingComplaint.Resolution = model.NewResolution;
                _context.Complaints.Update(existingComplaint);

                // Gửi thông báo cho cả Senior và Caregiver
                var senior = await _context.Seniors.FindAsync(existingComplaint.SeniorId);
                var caregiver = await _context.Caregivers.FindAsync(existingComplaint.CaregiverId);

                if (senior != null)
                {
                    var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
                    if (seniorUser != null)
                    {
                        var seniorNotification = new Notification
                        {
                            UserId = seniorUser.Id,
                            JobId = existingComplaint.JobId,
                            Title = "Khiếu nại đã được xử lý",
                            Message = $"Khiếu nại của bạn về công việc #{existingComplaint.JobId} đã được xử lý.\n" +
                                     $"- Trạng thái: {model.NewStatus}\n" +
                                     $"- Giải quyết: {model.NewResolution}",
                            CreatedAt = DateTime.Now,
                            IsRead = false,
                            Type = "Complaint"
                        };
                        _context.Notifications.Add(seniorNotification);
                        await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", seniorNotification.Message);
                    }
                }

                if (caregiver != null)
                {
                    var caregiverUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == caregiver.UserId && u.Role == "Caregiver");
                    if (caregiverUser != null)
                    {
                        var caregiverNotification = new Notification
                        {
                            UserId = caregiverUser.Id,
                            JobId = existingComplaint.JobId,
                            Title = "Khiếu nại đã được xử lý",
                            Message = $"Khiếu nại về công việc #{existingComplaint.JobId} đã được xử lý.\n" +
                                     $"- Trạng thái: {model.NewStatus}\n" +
                                     $"- Giải quyết: {model.NewResolution}",
                            CreatedAt = DateTime.Now,
                            IsRead = false,
                            Type = "Complaint"
                        };
                        _context.Notifications.Add(caregiverNotification);
                        await _notificationHub.Clients.User(caregiverUser.Id).SendAsync("ReceiveNotification", caregiverNotification.Message);
                    }
                }

                var result = await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveChanges result: {result} rows affected");

                if (result > 0)
                {
                    TempData["SuccessMessage"] = "Khiếu nại đã được xử lý thành công.";
                    return RedirectToAction("ManageComplaints");
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể cập nhật trạng thái khiếu nại.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Exception: {ex.Message}");
                TempData["ErrorMessage"] = "Lỗi khi xử lý khiếu nại: " + ex.Message;
                return View(model);
            }
        }

        // GET: Admin/ResolveComplaint
        [HttpGet]
        public async Task<IActionResult> ResolveComplaint(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Job)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
                return RedirectToAction("ManageComplaints");
            }

            var job = await _context.CareJobs.FindAsync(complaint.JobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy công việc liên quan đến khiếu nại.";
                return RedirectToAction("ManageComplaints");
            }

            var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
            var caregiver = await _context.Caregivers.FindAsync(complaint.CaregiverId);

            var viewModel = new ComplaintViewModel
            {
                Complaint = complaint,
                Job = job,
                Senior = senior,
                Caregiver = caregiver
            };

            return View(viewModel);
        }

        // POST: Admin/ResolveComplaint
        [HttpPost]
        public async Task<IActionResult> ResolveComplaint(int id, string adminResponse)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Job)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khiếu nại.";
                return RedirectToAction("ManageComplaints");
            }

            if (string.IsNullOrWhiteSpace(adminResponse))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập phản hồi.";
                return RedirectToAction("ResolveComplaint", new { id });
            }

            complaint.Status = "Đã xử lý";
            complaint.Resolution = adminResponse;
            _context.Complaints.Update(complaint);

            // Gửi thông báo cho Senior
            var senior = await _context.Seniors.FindAsync(complaint.SeniorId);
            if (senior != null)
            {
                var seniorUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == senior.UserId && u.Role == "Senior");
                if (seniorUser != null)
                {
                    var notification = new Notification
                    {
                        UserId = seniorUser.Id,
                        JobId = complaint.JobId,
                        Message = $"Khiếu nại của bạn về công việc #{complaint.JobId} đã được xử lý.\n" +
                                  $"- Trạng thái: Đã xử lý\n" +
                                  $"- Giải quyết: {adminResponse}",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);

                    // Gửi thông báo qua SignalR
                    await _notificationHub.Clients.User(seniorUser.Id).SendAsync("ReceiveNotification", notification.Message);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Khiếu nại đã được xử lý thành công.";
            return RedirectToAction("ManageComplaints");
        }

        [HttpGet]
        public IActionResult AddUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(string username, string email, string phoneNumber, string password, string role, string name, int? age, string careNeeds, bool? status, string skills, bool? isAvailable, IFormFile certificate, IFormFileCollection identityAndHealthDocs, decimal price)
        {
            try
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

                // Tạo user mới
                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    Role = role,
                    IsLocked = false,
                    Balance = 0,
                    FullName = name,
                    Address = "Chưa cập nhật",
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Gender = "Chưa cập nhật",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    NormalizedUserName = username.ToUpper(),
                    NormalizedEmail = email.ToUpper(),
                    RoleId = "0" // Giá trị mặc định
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = "Thêm người dùng thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return View();
                }

                // Gán vai trò cho người dùng
                await _userManager.AddToRoleAsync(user, role);

                if (role == "Senior")
                {
                    if (!age.HasValue || age <= 0)
                    {
                        TempData["ErrorMessage"] = $"Vui lòng nhập tuổi hợp lệ. Giá trị hiện tại: \"{age}\".";
                        await _userManager.DeleteAsync(user);
                        return View();
                    }

                    if (string.IsNullOrEmpty(careNeeds))
                    {
                        TempData["ErrorMessage"] = $"Vui lòng nhập nhu cầu chăm sóc. Giá trị hiện tại: \"{careNeeds}\".";
                        await _userManager.DeleteAsync(user);
                        return View();
                    }

                    // Xử lý tải lên nhiều file cho giấy tờ tùy thân và giấy khám sức khỏe
                    string identityAndHealthDocsPaths = null;
                    if (identityAndHealthDocs != null && identityAndHealthDocs.Count > 0)
                    {
                        var filePaths = new List<string>();
                        foreach (var file in identityAndHealthDocs)
                        {
                            var filePath = await SaveFile(file, "identityAndHealthDocs");
                            if (filePath != null)
                            {
                                filePaths.Add(filePath);
                            }
                        }
                        identityAndHealthDocsPaths = string.Join(",", filePaths);
                    }

                    var senior = new Senior
                    {
                        UserId = user.Id,
                        Name = name,
                        Age = age.Value,
                        CareNeeds = careNeeds,
                        RegistrationDate = DateTime.Now,
                        Status = status ?? false,
                        IsVerified = false,
                        AvatarUrl = "https://via.placeholder.com/150",
                        Price = price,
                        IdentityAndHealthDocs = identityAndHealthDocsPaths
                    };
                    _context.Seniors.Add(senior);
                    await _context.SaveChangesAsync();
                    user.RoleId = senior.Id.ToString();
                }
                else if (role == "Caregiver")
                {
                    if (string.IsNullOrEmpty(skills))
                    {
                        TempData["ErrorMessage"] = $"Vui lòng nhập kỹ năng chăm sóc. Giá trị hiện tại: \"{skills}\".";
                        await _userManager.DeleteAsync(user);
                        return View();
                    }

                    string certificateFilePath = null;
                    if (certificate != null && certificate.Length > 0)
                    {
                        certificateFilePath = await SaveFile(certificate, "certificates");
                    }

                    // Khởi tạo bảng giá mặc định dựa trên kỹ năng
                    string pricing = null;
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
                        UserId = user.Id,
                        Name = name,
                        Skills = skills,
                        Contact = phoneNumber,
                        IsAvailable = isAvailable ?? false,
                        CertificateFilePath = certificateFilePath,
                        IsVerified = false,
                        AvatarUrl = "https://via.placeholder.com/150",
                        Price = price,
                        Pricing = pricing,
                        Experience = "Chưa có kinh nghiệm" // Thêm giá trị mặc định cho Experience
                    };
                    _context.Caregivers.Add(caregiver);
                    await _context.SaveChangesAsync();
                    user.RoleId = caregiver.Id.ToString();
                }

                // Cập nhật RoleId của user
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Thêm người dùng thành công!";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi không xác định: {ex.Message}";
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" - {ex.InnerException.Message}";
                }
                return View();
            }
        }

        private async Task<string> SaveFile(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", folderName);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{folderName}/{fileName}";
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManagePayments(DateTime? startDate = null, DateTime? endDate = null, string status = null, int page = 1)
        {
            // Set default date range if not provided
            if (!startDate.HasValue) startDate = DateTime.Now.AddMonths(-1);
            if (!endDate.HasValue) endDate = DateTime.Now;

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;

            var pageSize = 10; // Số lượng item trên mỗi trang

            var query = _context.CareJobs
                .Include(j => j.Senior)
                    .ThenInclude(s => s.User)
                .Include(j => j.Caregiver)
                    .ThenInclude(c => c.User)
                .Where(j => j.CreatedAt >= (startDate ?? DateTime.Now.AddMonths(-1)) && 
                           j.CreatedAt <= (endDate ?? DateTime.Now));

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(j => j.PaymentStatus == status);
            }

            // Tính tổng số trang
            var totalItems = await query.CountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Lấy dữ liệu cho trang hiện tại
            var payments = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new PaymentViewModel
                {
                    Id = j.Id,
                    SeniorName = j.Senior.User.FullName,
                    CaregiverName = j.Caregiver.User.FullName,
                    TotalAmount = j.TotalBill,
                    Deposit = j.Deposit,
                    RemainingAmount = j.RemainingAmount,
                    PaymentStatus = j.PaymentStatus,
                    PaymentMethod = j.PaymentMethod,
                    CreatedAt = j.CreatedAt,
                    IsDepositPaid = j.IsDepositPaid,
                    Status = j.Status,
                    DepositNote = j.DepositNote
                })
                .ToListAsync();

            // Thống kê tổng quan
            var stats = new PaymentStatsViewModel
            {
                TotalPayments = totalItems,
                TotalAmount = await query.SumAsync(j => j.TotalBill),
                TotalDeposits = await query.SumAsync(j => j.Deposit),
                PaidPayments = await query.CountAsync(j => j.PaymentStatus == "Đã thanh toán"),
                PendingPayments = await query.CountAsync(j => j.PaymentStatus == "Chờ thanh toán")
            };

            ViewBag.Stats = stats;
            return View(payments);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            try
            {
                var job = await _context.CareJobs
                    .Include(j => j.Senior)
                        .ThenInclude(s => s.User)
                    .Include(j => j.Caregiver)
                        .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(j => j.Id == paymentId);

                if (job == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin thanh toán." });
                }

                if (job.PaymentStatus != "Chờ thanh toán")
                {
                    return Json(new { success = false, message = "Thanh toán này đã được xử lý trước đó." });
                }

                // Cập nhật trạng thái thanh toán và công việc
                job.PaymentStatus = "Đã thanh toán";
                job.Status = "Đang thực hiện";
                job.PaymentTime = DateTime.Now;
                
                // Cập nhật trạng thái của Senior và Caregiver
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

                // Lưu thay đổi
                await _context.SaveChangesAsync();

                // Gửi thông báo cho cả Senior và Caregiver
                if (job.Senior?.User != null)
                {
                    var seniorNotification = new Notification
                    {
                        UserId = job.Senior.User.Id,
                        JobId = job.Id,
                        Title = "Thanh toán đã được xác nhận",
                        Message = $"Thanh toán cho công việc #{job.Id} đã được xác nhận. Công việc sẽ bắt đầu sớm.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "Payment"
                    };
                    _context.Notifications.Add(seniorNotification);
                    await _notificationHub.Clients.User(job.Senior.User.Id).SendAsync("ReceiveNotification", seniorNotification.Message);
                }

                if (job.Caregiver?.User != null)
                {
                    var caregiverNotification = new Notification
                    {
                        UserId = job.Caregiver.User.Id,
                        JobId = job.Id,
                        Title = "Thanh toán đã được xác nhận",
                        Message = $"Thanh toán cho công việc #{job.Id} đã được xác nhận. Công việc sẽ bắt đầu sớm.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Type = "Payment"
                    };
                    _context.Notifications.Add(caregiverNotification);
                    await _notificationHub.Clients.User(job.Caregiver.User.Id).SendAsync("ReceiveNotification", caregiverNotification.Message);
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xác nhận thanh toán thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi xác nhận thanh toán: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectPayment(int paymentId, string reason)
        {
            try
            {
                var job = await _context.CareJobs
                    .Include(j => j.Senior)
                        .ThenInclude(s => s.User)
                    .Include(j => j.Caregiver)
                        .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(j => j.Id == paymentId);

                if (job == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin thanh toán." });
                }

                if (job.PaymentStatus != "Chờ thanh toán")
                {
                    return Json(new { success = false, message = "Thanh toán này đã được xử lý trước đó." });
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });
                }

                // Cập nhật trạng thái thanh toán
                job.PaymentStatus = "Từ chối";
                job.Status = "Đã hủy";
                job.Description = reason;

                await _context.SaveChangesAsync();

                // Tạo thông báo cho Caregiver
                var caregiverNotification = new Notification
                {
                    Title = "Thanh toán đã bị từ chối",
                    UserId = job.CaregiverId.ToString(),
                    JobId = job.Id,
                    Message = $"Thanh toán cho công việc #{job.Id} đã bị từ chối. Lý do: {reason}",
                    Type = "Payment",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(caregiverNotification);

                // Tạo thông báo cho Senior
                var seniorNotification = new Notification
                {
                    Title = "Thanh toán đã bị từ chối",
                    UserId = job.SeniorId.ToString(),
                    JobId = job.Id,
                    Message = $"Thanh toán cho công việc #{job.Id} đã bị từ chối. Lý do: {reason}",
                    Type = "Payment",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(seniorNotification);

                await _context.SaveChangesAsync();

                // Gửi thông báo qua SignalR
                await _notificationHub.Clients.User(job.CaregiverId.ToString()).SendAsync("ReceiveNotification", caregiverNotification.Message);
                await _notificationHub.Clients.User(job.SeniorId.ToString()).SendAsync("ReceiveNotification", seniorNotification.Message);

                return Json(new { success = true, message = "Từ chối thanh toán thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi từ chối thanh toán: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePendingPayment(int paymentId)
        {
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Job)
                    .FirstOrDefaultAsync(p => p.Id == paymentId && p.Status == "Chờ thanh toán");

                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thanh toán đang chờ duyệt.";
                    return RedirectToAction("ManagePayments");
                }

                // Cập nhật trạng thái công việc nếu cần
                if (payment.Job != null)
                {
                    payment.Job.Status = "Đang chờ Người chăm sóc thanh toán cọc";
                    payment.Job.DepositMade = false;
                    payment.Job.PaymentStatus = "Chờ thanh toán";
                    _context.CareJobs.Update(payment.Job);
                }

                // Xóa bản ghi thanh toán
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa thanh toán đang chờ duyệt thành công!";
                return RedirectToAction("ManagePayments");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa thanh toán: {ex.Message}";
                return RedirectToAction("ManagePayments");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SeedSampleUsers()
        {
            try
            {
                var userManager = _userManager;
                var password = "Password123!";

                // Tạo 5 Senior
                for (int i = 1; i <= 5; i++)
                {
                    var seniorUser = new ApplicationUser
                    {
                        UserName = $"senior{i}",
                        Email = $"senior{i}@example.com",
                        PhoneNumber = $"01234567{i}",
                        Role = "Senior",
                        IsLocked = false,
                        Balance = 0,
                        FullName = $"Nguyễn Văn {i}",
                        Address = "Hà Nội",
                        DateOfBirth = new DateTime(1950 + i, 1, 1),
                        Gender = i % 2 == 0 ? "Nữ" : "Nam",
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true,
                        AccessFailedCount = 0,
                        LockoutEnabled = false,
                        TwoFactorEnabled = false
                    };

                    var result = await userManager.CreateAsync(seniorUser, password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(seniorUser, "Senior");

                        var senior = new Senior
                        {
                            UserId = seniorUser.Id,
                            Name = seniorUser.FullName,
                            Age = DateTime.Now.Year - seniorUser.DateOfBirth.Year,
                            CareNeeds = "Chăm sóc cơ bản",
                            RegistrationDate = DateTime.Now,
                            Status = true,
                            IsVerified = true,
                            AvatarUrl = "https://via.placeholder.com/150",
                            Price = 2000000 + (i * 100000)
                        };

                        _context.Seniors.Add(senior);
                        seniorUser.RoleId = senior.Id.ToString();
                        await userManager.UpdateAsync(seniorUser);
                    }
                }

                // Tạo 5 Caregiver
                for (int i = 1; i <= 5; i++)
                {
                    var caregiverUser = new ApplicationUser
                    {
                        UserName = $"caregiver{i}",
                        Email = $"caregiver{i}@example.com",
                        PhoneNumber = $"09876543{i}",
                        Role = "Caregiver",
                        IsLocked = false,
                        Balance = 0,
                        FullName = $"Trần Thị {i}",
                        Address = "Hồ Chí Minh",
                        DateOfBirth = new DateTime(1990 + i, 1, 1),
                        Gender = i % 2 == 0 ? "Nam" : "Nữ",
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true,
                        AccessFailedCount = 0,
                        LockoutEnabled = false,
                        TwoFactorEnabled = false
                    };

                    var result = await userManager.CreateAsync(caregiverUser, password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(caregiverUser, "Caregiver");

                        var caregiver = new Caregiver
                        {
                            UserId = caregiverUser.Id,
                            Name = caregiverUser.FullName,
                            Skills = "Chăm sóc cơ bản",
                            Contact = caregiverUser.PhoneNumber,
                            IsAvailable = true,
                            IsVerified = true,
                            AvatarUrl = "https://via.placeholder.com/150",
                            Price = 2000000 + (i * 100000),
                            Experience = $"{i} năm kinh nghiệm"
                        };

                        _context.Caregivers.Add(caregiver);
                        caregiverUser.RoleId = caregiver.Id.ToString();
                        await userManager.UpdateAsync(caregiverUser);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã thêm dữ liệu mẫu thành công!";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Lỗi khi tạo dữ liệu mẫu: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" - {ex.InnerException.Message}";
                }
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("ManageUsers");
            }
        }

        [HttpGet]
        public async Task<IActionResult> TransactionStats(DateTime? startDate = null, DateTime? endDate = null, string status = null, int page = 1)
        {
            // Set default date range if not provided
            var effectiveStartDate = startDate ?? DateTime.Now.AddMonths(-1);
            var effectiveEndDate = endDate ?? DateTime.Now;

            ViewBag.StartDate = effectiveStartDate;
            ViewBag.EndDate = effectiveEndDate;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;

            var pageSize = 10; // Số lượng item trên mỗi trang

            var query = _context.CareJobs
                .Include(j => j.Senior)
                    .ThenInclude(s => s.User)
                .Include(j => j.Caregiver)
                    .ThenInclude(c => c.User)
                .Where(j => j.CreatedAt >= effectiveStartDate && 
                           j.CreatedAt <= effectiveEndDate);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(j => j.Status == status);
            }

            // Tính tổng số trang
            var totalItems = await query.CountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Lấy dữ liệu cho trang hiện tại
            var transactions = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new TransactionViewModel
                {
                    Id = j.Id,
                    SeniorName = j.Senior.User.FullName,
                    CaregiverName = j.Caregiver.User.FullName,
                    ServiceType = j.ServiceType,
                    TotalAmount = j.TotalBill,
                    Status = j.Status,
                    PaymentStatus = j.PaymentStatus,
                    CreatedAt = j.CreatedAt,
                    StartTime = j.StartTime,
                    EndTime = j.EndTime,
                    Location = j.Location,
                    Description = j.Description
                })
                .ToListAsync();

            // Thống kê tổng quan
            var stats = new TransactionStatsViewModel
            {
                TotalTransactions = totalItems,
                TotalAmount = await query.SumAsync(j => j.TotalBill),
                CompletedTransactions = await query.CountAsync(j => j.Status == "Hoàn thành"),
                PendingTransactions = await query.CountAsync(j => j.Status.StartsWith("Đang chờ")),
                CancelledTransactions = await query.CountAsync(j => j.Status == "Hủy")
            };

            ViewBag.Stats = stats;
            return View(transactions);
        }

        [HttpGet]
        public async Task<IActionResult> BalanceHistory()
        {
            var adminUser = await _userManager.GetUserAsync(User);
            if (adminUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy lịch sử từ bảng CareJobs
            var transactions = await _context.CareJobs
                .Where(j => j.PaymentStatus == "Đã thanh toán")
                .OrderBy(j => j.PaymentTime ?? j.CreatedAt)
                .Select(j => new BalanceHistoryViewModel
                {
                    CreatedAt = j.PaymentTime ?? j.CreatedAt,
                    PaymentId = j.Id,
                    Type = "Received",
                    Amount = j.TotalBill * 0.3m, // 30% commission
                    Description = $"Phí hoa hồng từ giao dịch #{j.Id}",
                    BalanceAfter = 0 // Sẽ được tính toán sau
                })
                .ToListAsync();

            // Tính toán số dư sau mỗi giao dịch
            decimal runningBalance = 0;
            foreach (var transaction in transactions)
            {
                runningBalance += transaction.Amount;
                transaction.BalanceAfter = runningBalance;
            }

            // Cập nhật số dư hiện tại của admin
            adminUser.Balance = runningBalance;
            await _userManager.UpdateAsync(adminUser);

            ViewBag.CurrentBalance = runningBalance;
            ViewBag.BalanceHistory = transactions.OrderByDescending(t => t.CreatedAt).ToList();
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentDetails(int paymentId)
        {
            try
            {
                var payment = await _context.CareJobs
                    .Include(j => j.Senior)
                        .ThenInclude(s => s.User)
                    .Include(j => j.Caregiver)
                        .ThenInclude(c => c.User)
                    .Where(j => j.Id == paymentId)
                    .Select(j => new PaymentViewModel
                    {
                        Id = j.Id,
                        JobId = j.Id,
                        SeniorName = j.Senior.User.FullName,
                        CaregiverName = j.Caregiver.User.FullName,
                        TotalAmount = j.TotalBill,
                        Deposit = j.Deposit,
                        RemainingAmount = j.RemainingAmount,
                        PaymentStatus = j.PaymentStatus,
                        PaymentMethod = j.PaymentMethod,
                        CreatedAt = j.CreatedAt,
                        IsDepositPaid = j.IsDepositPaid,
                        Status = j.Status,
                        DepositNote = j.DepositNote
                    })
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin giao dịch." });
                }

                return Json(new { success = true, data = payment });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi tải thông tin chi tiết: {ex.Message}" });
            }
        }
    }
}