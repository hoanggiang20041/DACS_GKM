using System.Text.RegularExpressions;
using Chamsoc.Data;
using Chamsoc.Models;
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
            seniorToUpdate.Price = price; // Cập nhật giá mong muốn

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

        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public async Task<IActionResult> ViewUserProfile(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Truyền vai trò của người dùng được xem
            ViewBag.UserRole = user.Role;

            // Truyền vai trò của người dùng hiện tại (người đăng nhập)
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUserRole = currentUser?.Role;

            // Chỉ cần kiểm tra xem người dùng hiện tại có tồn tại hay không
            if (currentUser == null)
            {
                return Unauthorized(); // Nếu không tìm thấy người dùng hiện tại
            }

            // Truyền thông tin người dùng vào ViewBag
            ViewBag.User = user;

            // Kiểm tra vai trò và trả về view tương ứng
            if (user.Role == "Senior")
            {
                var senior = await _context.Seniors.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (senior == null)
                {
                    return NotFound();
                }
                return View("ProfileSenior", senior);
            }
            else if (user.Role == "Caregiver")
            {
                var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                if (caregiver == null)
                {
                    return NotFound();
                }
                return View("ProfileCaregiver", caregiver);
            }
            else if (user.Role == "Admin")
            {
                return View("ProfileAdmin", user);
            }

            return RedirectToAction("About", "Home");
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
                    TempData["ErrorMessage"] = $"Vui lòng chọn kỹ năng chăm sóc. Giá trị hiện tại: \"{skills}\".";
                    return View();
                }

                // Kiểm tra giá trị skills hợp lệ
                var validSkills = new List<string> { "Khám toàn diện", "Khám vật lý trị liệu" };
                if (!validSkills.Contains(skills))
                {
                    TempData["ErrorMessage"] = $"Kỹ năng không hợp lệ. Giá trị hiện tại: \"{skills}\". Kỹ năng phải là: {string.Join(", ", validSkills)}.";
                    return View();
                }
            }
            else
            {
                TempData["ErrorMessage"] = $"Vui lòng chọn vai trò. Giá trị hiện tại: \"{role}\".";
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
                TempData["ErrorMessage"] = "Đăng ký thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return View();
            }

            // Gán vai trò cho người dùng trong hệ thống Identity
            await _userManager.AddToRoleAsync(user, role);

            if (role == "Senior")
            {
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
                roleId = senior.Id.ToString(); // Chuyển int thành string
            }
            else if (role == "Caregiver")
            {
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
                    IsAvailable = isAvailable,
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
            await _context.SaveChangesAsync();

            // Lưu thông tin người dùng vào session
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserRole", user.Role ?? string.Empty);

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
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
                return View("~/Views/Shared/AccessDenied.cshtml");
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
                        var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        return View(caregiver ?? new Caregiver());
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        TempData["ErrorMessage"] = "Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu.";
                        ViewBag.User = user;
                        var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        return View(caregiver ?? new Caregiver());
                    }

                    var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!passwordCheck)
                    {
                        TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                        ViewBag.User = user;
                        var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        return View(caregiver ?? new Caregiver());
                    }

                    var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Đổi mật khẩu thất bại: " + string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                        ViewBag.User = user;
                        var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                        return View(caregiver ?? new Caregiver());
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
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    return View(caregiver ?? new Caregiver());
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
                    var caregiver = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    return View(caregiver ?? new Caregiver());
                }
                user.PhoneNumber = contact;
                await _userManager.UpdateAsync(user);
            }

            // Tìm và cập nhật thông tin Caregiver
            var caregiverToUpdate = await _context.Caregivers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (caregiverToUpdate == null)
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

                caregiverToUpdate.AvatarUrl = $"/uploads/avatars/{fileName}";
            }

            // Kiểm tra giá (Price)
            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Giá mong muốn phải lớn hơn 0.";
                ViewBag.User = user;
                return View(caregiverToUpdate);
            }

            // Cập nhật thông tin Caregiver
            caregiverToUpdate.Name = name;
            caregiverToUpdate.Skills = skills;
            caregiverToUpdate.Contact = contact;
            caregiverToUpdate.IsAvailable = isAvailable;
            caregiverToUpdate.Price = price;

            try
            {
                _context.Caregivers.Update(caregiverToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                ViewBag.User = user;
                return View(caregiverToUpdate);
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
            return RedirectToAction("Profile");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
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

            ViewBag.User = user;
            return View("UpdateProfileCaregiver", caregiver);
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
    }
}