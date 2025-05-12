using Microsoft.AspNetCore.Mvc;
using Chamsoc.Models;
using Chamsoc.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace Chamsoc.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HomeController(AppDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

       

        public IActionResult GioiThieuSoQua()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult ChamSocToanQuoc()
        {
            return View();
        }

        public IActionResult ChamSocNguoiOm()
        {
            return View();
        }

        public IActionResult ChamSocSanPhu()
        {
            return View();
        }

        public IActionResult ChamSocNguoiGia()
        {
            return View();
        }

        public IActionResult ChamSocSucKhoe()
        {
            return View();
        }

        public IActionResult ChamSocVatLyTriLieu()
        {
            return View();
        }



        public IActionResult ListSeniors(string searchCareNeeds, decimal? minPrice, decimal? maxPrice, decimal? customMinPrice, decimal? customMaxPrice)
        {
            // Chỉ lấy các Senior có IsVerified = true
            var seniors = _context.Seniors
                .Where(s => s.IsVerified == true) // Đảm bảo lọc đúng
                .AsQueryable();

            // Lưu giá trị tìm kiếm vào ViewBag để hiển thị lại trên form
            ViewBag.SearchCareNeeds = searchCareNeeds;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.CustomMinPrice = customMinPrice;
            ViewBag.CustomMaxPrice = customMaxPrice;

            // Tìm kiếm theo nhu cầu chăm sóc (khớp chính xác)
            if (!string.IsNullOrEmpty(searchCareNeeds))
            {
                searchCareNeeds = searchCareNeeds.Trim().ToLower();
                seniors = seniors.Where(s => s.CareNeeds != null && s.CareNeeds.ToLower() == searchCareNeeds);
            }

            // Tìm kiếm theo giá
            if (minPrice.HasValue && minPrice != 0)
            {
                if (minPrice == -1 && customMinPrice.HasValue) // Giá tùy chỉnh
                {
                    seniors = seniors.Where(s => s.Price >= customMinPrice.Value);
                }
                else
                {
                    seniors = seniors.Where(s => s.Price >= minPrice.Value);
                }
            }
            if (maxPrice.HasValue && maxPrice != 0)
            {
                if (maxPrice == -1 && customMaxPrice.HasValue) // Giá tùy chỉnh
                {
                    seniors = seniors.Where(s => s.Price <= customMaxPrice.Value);
                }
                else
                {
                    seniors = seniors.Where(s => s.Price <= maxPrice.Value);
                }
            }

            // Debug: Kiểm tra số lượng bản ghi sau khi lọc
            var filteredSeniors = seniors.ToList();
            System.Diagnostics.Debug.WriteLine($"Số lượng Senior sau khi lọc: {filteredSeniors.Count}");

            return View(filteredSeniors);
        }
        public IActionResult ConnectSenior(int id)
        {
            // Giả định: Lấy thông tin Senior và xử lý yêu cầu kết nối
            var senior = _context.Seniors.FirstOrDefault(s => s.Id == id);
            if (senior == null)
            {
                return NotFound();
            }

            // Logic xử lý yêu cầu kết nối (ví dụ: lưu vào bảng kết nối, gửi thông báo, v.v.)
            // Tạm thời, chỉ hiển thị thông báo thành công
            TempData["SuccessMessage"] = $"Yêu cầu kết nối với {senior.Name} đã được gửi!";
            return RedirectToAction("ListSeniors");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public IActionResult ListCaregivers()
        {
            var caregivers = _context.Caregivers.ToList();
            return View(caregivers);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Senior,Caregiver")]
        public async Task<IActionResult> EditUserProfile(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền: Chỉ Admin hoặc chính người dùng đó mới được phép chỉnh sửa
            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser.Id != id)
            {
                return Forbid();
            }

            if (user.Role == "Senior")
            {
                if (int.TryParse(user.RoleId, out int roleId))
                {
                    var senior = _context.Seniors.FirstOrDefault(s => s.Id == roleId);
                    if (senior == null)
                    {
                        return NotFound();
                    }
                    ViewBag.User = user;
                    return View("UpdateProfileSenior", senior);
                }
                else
                {
                    return BadRequest("Invalid RoleId for Senior.");
                }
            }
            else if (user.Role == "Caregiver")
            {
                if (int.TryParse(user.RoleId, out int roleId))
                {
                    var caregiver = _context.Caregivers.FirstOrDefault(c => c.Id == roleId);
                    if (caregiver == null)
                    {
                        return NotFound();
                    }
                    ViewBag.User = user;
                    return View("UpdateProfileCaregiver", caregiver);
                }
                else
                {
                    return BadRequest("Invalid RoleId for Caregiver.");
                }
            }
            else if (user.Role == "Admin")
            {
                ViewBag.User = user;
                return View("UpdateProfileAdmin", user);
            }

            return RedirectToAction("ManageUsers");
        }

       
    }
}