using System.Text.RegularExpressions;
using Chamsoc.Data;
using Chamsoc.Models;
using Chamsoc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chamsoc.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(AppDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public IActionResult Index()
        {
            return RedirectToAction("GioiThieuSoQua", "Home");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public async Task<IActionResult> Profile()
        {
            System.Diagnostics.Debug.WriteLine("Profile action in AccountController called");
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return RedirectToAction("ViewUserProfile", "Account", new { id = user.Id });
        }

        [HttpGet]
        public IActionResult UpdateProfile()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // Redirect to the appropriate update action in AdminController based on role
            if (user.Role == "Senior")
            {
                return RedirectToAction("UpdateProfileSenior", "Admin", new { id = user.Id });
            }
            else if (user.Role == "Caregiver")
            {
                return RedirectToAction("UpdateProfileCaregiver", "Admin", new { id = user.Id });
            }
            else if (user.Role == "Admin")
            {
                return RedirectToAction("UpdateProfileAdmin", "Admin", new { id = user.Id });
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public async Task<IActionResult> UpdateProfileSenior(string id, string name, int age, string careNeeds, bool status, string email, string contact, string currentPassword, string newPassword, IFormFile avatar, decimal price, IFormFileCollection identityAndHealthDocs)
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
                return View("~/Views/Shared/AccessDenied.cshtml");
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
                        var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                        return View(senior ?? new Senior());
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        TempData["ErrorMessage"] = "Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu.";
                        ViewBag.User = user;
                        var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                        return View(senior ?? new Senior());
                    }

                    var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!passwordCheck)
                    {
                        TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                        ViewBag.User = user;
                        var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                        return View(senior ?? new Senior());
                    }

                    var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                        return View(senior ?? new Senior());
                    }
                }
            }

            if (user.Email != email)
            {
                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại. Vui lòng chọn email khác.";
                    ViewBag.User = user;
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                    return View(senior ?? new Senior());
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
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                    return View(senior ?? new Senior());
                }
                user.PhoneNumber = contact;
                await _userManager.UpdateAsync(user);
            }

            var seniorToUpdate = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (seniorToUpdate == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin Senior.";
                return RedirectToAction("Index");
            }

            // Xử lý upload giấy tờ tùy thân và giấy khám sức khỏe
            if (identityAndHealthDocs != null && identityAndHealthDocs.Count > 0)
            {
                var filePaths = new List<string>();
                foreach (var file in identityAndHealthDocs)
                {
                    if (file != null && file.Length > 0)
                    {
                        // Kiểm tra định dạng file
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            TempData["ErrorMessage"] = "Chỉ chấp nhận file có định dạng: .pdf, .jpg, .jpeg, .png";
                            ViewBag.User = user;
                            return View(seniorToUpdate);
                        }

                        // Kiểm tra kích thước file (giới hạn 10MB)
                        if (file.Length > 10 * 1024 * 1024)
                        {
                            TempData["ErrorMessage"] = "Kích thước file không được vượt quá 10MB";
                            ViewBag.User = user;
                            return View(seniorToUpdate);
                        }

                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "identityAndHealthDocs");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        filePaths.Add($"/uploads/identityAndHealthDocs/{uniqueFileName}");
                    }
                }

                if (filePaths.Any())
                {
                    // Xóa các file cũ nếu có
                    if (!string.IsNullOrEmpty(seniorToUpdate.IdentityAndHealthDocs))
                    {
                        var oldFiles = seniorToUpdate.IdentityAndHealthDocs.Split(',');
                        foreach (var oldFile in oldFiles)
                        {
                            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldFile.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                try
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }
                                catch
                                {
                                    // Bỏ qua lỗi khi xóa file cũ
                                }
                            }
                        }
                    }

                    seniorToUpdate.IdentityAndHealthDocs = string.Join(",", filePaths);
                }
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

                seniorToUpdate.AvatarUrl = $"/uploads/avatars/{fileName}";
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Giá mong muốn phải lớn hơn 0.";
                ViewBag.User = user;
                return View(seniorToUpdate);
            }

            seniorToUpdate.Name = name;
            seniorToUpdate.Age = age;
            seniorToUpdate.CareNeeds = careNeeds;
            seniorToUpdate.Status = status;
            if (price > 0)
            {
                seniorToUpdate.Price = price;
            }

            try
            {
                _context.Seniors.Update(seniorToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                ViewBag.User = user;
                return View(seniorToUpdate);
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
            return RedirectToAction("Profile");
        }

        // Action cập nhật hồ sơ cho Senior
        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
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
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (senior == null)
            {
                return NotFound();
            }

            ViewBag.User = user;
            return View("UpdateProfileSenior", senior);
        }

        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public async Task<IActionResult> ViewUserProfile(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "Không tìm thấy ID người dùng.";
                    return RedirectToAction("Index", "Home");
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                    return RedirectToAction("Index", "Home");
                }

                // Truyền vai trò của người dùng được xem
                ViewBag.UserRole = user.Role;

                // Truyền vai trò của người dùng hiện tại (người đăng nhập)
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem hồ sơ.";
                    return RedirectToAction("Login");
                }
                ViewBag.CurrentUserRole = currentUser.Role;

                // Truyền thông tin người dùng vào ViewBag
                ViewBag.User = user;

                // Kiểm tra vai trò và trả về view tương ứng
                if (user.Role == "Senior")
                {
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                    if (senior == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy thông tin Người cần chăm sóc.";
                        return RedirectToAction("Index", "Home");
                    }
                    return View("ProfileSenior", senior);
                }
                else if (user.Role == "Caregiver")
                {
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    if (caregiver == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy thông tin người chăm sóc.";
                        return RedirectToAction("Index", "Home");
                    }
                    var viewModel = await CreateCaregiverViewModel(caregiver);
                    return View("ProfileCaregiver", viewModel);
                }
                else if (user.Role == "Admin")
                {
                    return View("ProfileAdmin", user);
                }

                TempData["ErrorMessage"] = "Vai trò người dùng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View();
            }

            if (user.IsLocked)
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    TempData["ErrorMessage"] = "Tài khoản của bạn đã bị khóa tạm thời do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Mật khẩu không chính xác.";
                }
                return View();
            }

            // Lưu thông tin đăng nhập vào Session
            HttpContext.Session.SetString("LoggedInUser", user.UserName ?? string.Empty);
            HttpContext.Session.SetString("UserRole", user.Role ?? string.Empty);
            HttpContext.Session.SetString("UserId", user.Id);

            if (user.Role == "Admin")
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
            else if (user.Role == "Caregiver")
            {
                return RedirectToAction("ListSeniors", "Seniors");
            }
            else if (user.Role == "Senior")
            {
                return RedirectToAction("ListCaregivers", "Caregivers");
            }

            return RedirectToAction("About");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string phoneNumber, string password, string role, string name, int age, string careNeeds, bool status, string skills, bool isAvailable, IFormFile certificate, IFormFileCollection identityAndHealthDocs, decimal price)
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
                if (string.IsNullOrEmpty(name))
                {
                    TempData["ErrorMessage"] = $"Vui lòng nhập họ và tên. Giá trị hiện tại: \"{name}\".";
                    return View();
                }

                if (role == "Senior")
                {
                    if (age <= 0)
                    {
                        TempData["ErrorMessage"] = $"Vui lòng nhập tuổi hợp lệ. Giá trị hiện tại: \"{age}\".";
                        return View();
                    }
                    if (string.IsNullOrEmpty(careNeeds))
                    {
                        TempData["ErrorMessage"] = $"Vui lòng nhập tình trạng bệnh. Giá trị hiện tại: \"{careNeeds}\".";
                        return View();
                    }
                }
                else if (role == "Caregiver")
                {
                    if (string.IsNullOrEmpty(skills))
                    {
                        TempData["ErrorMessage"] = $"Vui lòng chọn chuyên môn. Giá trị hiện tại: \"{skills}\".";
                        return View();
                    }

                    // Kiểm tra chuyên môn hợp lệ
                    var validSkills = new[] { "Bác sĩ", "Y tá", "Điều dưỡng", "Vật lý trị liệu", "Dinh dưỡng" };
                    if (!validSkills.Contains(skills))
                    {
                        TempData["ErrorMessage"] = $"Chuyên môn không hợp lệ. Giá trị hiện tại: \"{skills}\". Chuyên môn phải là một trong các giá trị sau: {string.Join(", ", validSkills)}.";
                        return View();
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = $"Vai trò không hợp lệ. Giá trị hiện tại: \"{role}\".";
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
                    TempData["ErrorMessage"] = "Đăng ký thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return View();
                }

                // Gán vai trò cho người dùng
                await _userManager.AddToRoleAsync(user, role);

                if (role == "Senior")
                {
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
                        Age = age,
                        CareNeeds = careNeeds,
                        RegistrationDate = DateTime.Now,
                        Status = status,
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
                    string certificateFilePath = null;
                    if (certificate != null && certificate.Length > 0)
                    {
                        certificateFilePath = await SaveFile(certificate, "certificates");
                    }

                    // Khởi tạo bảng giá mặc định dựa trên chuyên môn
                    string pricing = null;
                    if (skills == "Bác sĩ")
                    {
                        pricing = "{\"1Hour\": 2000000, \"2Hours\": 3500000, \"5Sessions\": 8000000}";
                    }
                    else if (skills == "Y tá")
                    {
                        pricing = "{\"1Hour\": 1500000, \"2Hours\": 2500000, \"5Sessions\": 6000000}";
                    }
                    else if (skills == "Điều dưỡng")
                    {
                        pricing = "{\"1Hour\": 1200000, \"2Hours\": 2000000, \"5Sessions\": 5000000}";
                    }
                    else if (skills == "Vật lý trị liệu")
                    {
                        pricing = "{\"1Hour\": 1800000, \"2Hours\": 3000000, \"5Sessions\": 7000000}";
                    }
                    else if (skills == "Dinh dưỡng")
                    {
                        pricing = "{\"1Hour\": 1000000, \"2Hours\": 1800000, \"5Sessions\": 4000000}";
                    }

                    var caregiver = new Caregiver
                    {
                        UserId = user.Id,
                        Name = name,
                        Skills = skills,
                        Contact = phoneNumber,
                        IsAvailable = isAvailable,
                        CertificateFilePath = certificateFilePath,
                        IsVerified = false,
                        AvatarUrl = "https://via.placeholder.com/150",
                        Price = price,
                        Pricing = pricing,
                        Experience = "Chưa có kinh nghiệm",
                        Degree = skills // Sử dụng chuyên môn làm bằng cấp mặc định
                    };
                    _context.Caregivers.Add(caregiver);
                    await _context.SaveChangesAsync();
                    user.RoleId = caregiver.Id.ToString();
                }

                // Cập nhật RoleId của user
                await _userManager.UpdateAsync(user);

                // Lưu thông tin người dùng vào session
                HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role ?? string.Empty);

                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
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

        [HttpPost]
        [Authorize(Roles = "Admin,Caregiver")]
        public async Task<IActionResult> UpdateProfileCaregiver(string id, string name, string skills, string email, string contact, bool isAvailable, string currentPassword, string newPassword, IFormFile avatar, decimal price, IFormFile certificate, IFormFile degree)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Role != "Caregiver")
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng hoặc vai trò không hợp lệ.";
                return RedirectToAction("Index");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser?.Id != id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa hồ sơ của người dùng này.";
                return RedirectToAction("Index");
            }

            var caregiverToUpdate = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (caregiverToUpdate == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin người chăm sóc.";
                return RedirectToAction("Index");
            }

            // Kiểm tra email
            if (user.Email != email)
            {
                if (!string.IsNullOrEmpty(email) && _context.Users.Any(u => u.Email == email && u.Id != user.Id))
                {
                    TempData["ErrorMessage"] = "Email đã tồn tại. Vui lòng chọn email khác.";
                    var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                    ViewBag.User = user;
                    return View(viewModel);
                }
                user.Email = email;
            }

            // Kiểm tra số điện thoại
            if (user.PhoneNumber != contact)
            {
                if (!string.IsNullOrEmpty(contact) && _context.Users.Any(u => u.PhoneNumber == contact && u.Id != user.Id))
                {
                    TempData["ErrorMessage"] = "Số điện thoại đã tồn tại. Vui lòng chọn số khác.";
                    var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                    ViewBag.User = user;
                    return View(viewModel);
                }
                user.PhoneNumber = contact;
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Giá mong muốn phải lớn hơn 0.";
                var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                ViewBag.User = user;
                return View(viewModel);
            }

            // Xử lý tải lên chứng chỉ hành nghề
            if (certificate != null && certificate.Length > 0)
            {
                var certificatePath = await SaveFile(certificate, "certificates");
                if (certificatePath != null)
                {
                    caregiverToUpdate.CertificateFilePath = certificatePath;
                }
            }

            // Xử lý tải lên bằng cấp chuyên môn
            if (degree != null && degree.Length > 0)
            {
                var degreePath = await SaveFile(degree, "degrees");
                if (degreePath != null)
                {
                    caregiverToUpdate.Degree = degreePath;
                }
            }

            // Cập nhật thông tin Caregiver
            caregiverToUpdate.Name = name;
            if (!string.IsNullOrEmpty(skills))
            {
                caregiverToUpdate.Skills = skills;
            }
            caregiverToUpdate.Contact = contact;
            caregiverToUpdate.IsAvailable = isAvailable;
            if (price > 0)
            {
                caregiverToUpdate.Price = price;
            }

            // Xử lý avatar nếu có
            if (avatar != null && avatar.Length > 0)
            {
                try
                {
                    // Kiểm tra định dạng file
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Chỉ chấp nhận file ảnh có định dạng: .jpg, .jpeg, .png, .gif";
                        var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                        ViewBag.User = user;
                        return View(viewModel);
                    }

                    // Kiểm tra kích thước file (giới hạn 5MB)
                    if (avatar.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "Kích thước file không được vượt quá 5MB";
                        var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                        ViewBag.User = user;
                        return View(viewModel);
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Xóa ảnh cũ nếu có
                    if (!string.IsNullOrEmpty(caregiverToUpdate.AvatarUrl) && caregiverToUpdate.AvatarUrl != "/images/default-avatar.png")
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", caregiverToUpdate.AvatarUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                            catch
                            {
                                // Bỏ qua lỗi khi xóa file cũ
                            }
                        }
                    }

                    // Tạo tên file mới
                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Lưu file mới
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await avatar.CopyToAsync(fileStream);
                    }

                    caregiverToUpdate.AvatarUrl = "/uploads/avatars/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Lỗi khi upload ảnh: " + ex.Message;
                    var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                    ViewBag.User = user;
                    return View(viewModel);
                }
            }

            // Cập nhật mật khẩu nếu có
            if (!string.IsNullOrEmpty(currentPassword) && !string.IsNullOrEmpty(newPassword))
            {
                var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                if (!passwordCheck)
                {
                    TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                    var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                    ViewBag.User = user;
                    return View(viewModel);
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = "Lỗi khi đổi mật khẩu: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                    ViewBag.User = user;
                    return View(viewModel);
                }
            }

            try
            {
                await _userManager.UpdateAsync(user);
                _context.Caregivers.Update(caregiverToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                var viewModel = await CreateCaregiverViewModel(caregiverToUpdate);
                ViewBag.User = user;
                return View(viewModel);
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction("ViewUserProfile", new { id = user.Id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Caregiver")]
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
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (caregiver == null)
            {
                return NotFound();
            }

            var viewModel = await CreateCaregiverViewModel(caregiver);
            ViewBag.User = user;
            return View("UpdateProfileCaregiver", viewModel);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove("LoggedInUser");
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index");
        }

        // Thêm action AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View("~/Views/Shared/AccessDenied.cshtml");
        }

        private async Task<CaregiverViewModel> CreateCaregiverViewModel(Caregiver caregiver)
        {
            var ratings = await _context.Ratings.Where(r => r.CaregiverId == caregiver.Id).ToListAsync();
            return new CaregiverViewModel
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
                RecentRatings = ratings.OrderByDescending(r => r.CreatedAt).Take(3).ToList(),
                Caregiver = caregiver
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDetails(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });
                }

                object userData;
                if (user.Role == "Senior")
                {
                    var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                    if (senior != null)
                    {
                        userData = new
                        {
                            name = user.FullName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            isLocked = user.IsLocked,
                            avatarUrl = senior.AvatarUrl,
                            role = user.Role,
                            careNeeds = senior.CareNeeds,
                            price = senior.Price,
                            isVerified = senior.IsVerified,
                            age = senior.Age,
                            status = senior.Status,
                            identityDocs = senior.IdentityAndHealthDocs
                        };
                    }
                    else
                    {
                        userData = new
                        {
                            name = user.FullName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            isLocked = user.IsLocked,
                            avatarUrl = "/images/default-avatar.png",
                            role = user.Role
                        };
                    }
                }
                else if (user.Role == "Caregiver")
                {
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    if (caregiver != null)
                    {
                        userData = new
                        {
                            name = user.FullName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            isLocked = user.IsLocked,
                            avatarUrl = caregiver.AvatarUrl,
                            role = user.Role,
                            skills = caregiver.Skills,
                            price = caregiver.Price,
                            isVerified = caregiver.IsVerified,
                            experience = caregiver.Experience,
                            isAvailable = caregiver.IsAvailable,
                            degree = caregiver.Degree,
                            certificate = caregiver.CertificateFilePath,
                            identityDocs = caregiver.IdentityAndHealthDocs
                        };
                    }
                    else
                    {
                        userData = new
                        {
                            name = user.FullName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            isLocked = user.IsLocked,
                            avatarUrl = "/images/default-avatar.png",
                            role = user.Role
                        };
                    }
                }
                else
                {
                    userData = new
                    {
                        name = user.FullName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        isLocked = user.IsLocked,
                        avatarUrl = "/images/default-avatar.png",
                        role = user.Role
                    };
                }

                return Json(new { success = true, data = userData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Senior")]
        public async Task<IActionResult> ResetComplaintNotifications()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            // Lấy tất cả thông báo chưa đọc của user
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && n.Type == "Complaint")
                .ToListAsync();

            // Đánh dấu tất cả thông báo là đã đọc
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                System.Diagnostics.Debug.WriteLine($"Error resetting notifications: {ex.Message}");
            }

            // Chuyển hướng đến trang khiếu nại
            return RedirectToAction("FileComplaint", "CareJobs");
        }
    }
}