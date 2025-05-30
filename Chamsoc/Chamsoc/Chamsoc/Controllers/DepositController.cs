using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SkiaSharp;

namespace Chamsoc.Controllers
{
    public class DepositController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DepositController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> MakeDeposit(int jobId)
        {
            var job = await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return NotFound();
            }

            // Calculate default deposit amount (30% of total bill)
            decimal depositAmount = job.TotalBill * 0.3m;

            // Generate payment description with more details
            string description = $"NAPCOC_DV{job.Id}_{job.ServiceType}";

            // Create VietQR configuration
            var qrConfig = new VietQRConfig();
            string qrUrl = qrConfig.GenerateQRUrl(depositAmount, description);

            // Pass bank information to view
            ViewBag.QRUrl = qrUrl;
            ViewBag.BankName = "Techcombank";
            ViewBag.AccountNo = qrConfig.AccountNo;
            ViewBag.AccountName = qrConfig.AccountName;
            ViewBag.Description = description;
            ViewBag.Amount = depositAmount;

            return View(job);
        }

        [HttpPost]
        public async Task<IActionResult> MakeDeposit(int jobId, decimal amount)
        {
            var job = await _context.CareJobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            // Update deposit information
            job.Deposit = amount;
            job.DepositMade = true;
            job.PaymentStatus = "Chờ thanh toán";
            job.Status = "Đang đợi duyệt";
            job.PaymentTime = DateTime.Now;
            job.DepositNote = $"NAPCOC_DV{job.Id}_{job.ServiceType}";

            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã nạp cọc thành công và chờ admin duyệt!";
                return RedirectToAction("Index", "CareJobs");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi nạp cọc: " + ex.Message;
                return RedirectToAction("MakeDeposit", new { jobId });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ApproveDeposit(int jobId)
        {
            var job = await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null || !job.DepositMade)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin nạp cọc";
                return RedirectToAction("ApprovePayments", "Admin");
            }

            job.Status = "Đang thực hiện";
            job.PaymentStatus = "Completed";
            job.PaymentTime = DateTime.Now;

            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã duyệt nạp cọc thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi duyệt nạp cọc: " + ex.Message;
            }

            return RedirectToAction("ApprovePayments", "Admin");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RejectDeposit(int jobId, string reason)
        {
            var job = await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return NotFound();
            }

            job.Status = "Rejected";
            job.PaymentStatus = "Failed";
            job.PaymentTime = DateTime.Now;
            job.DepositNote += $"\nTừ chối bởi Admin - Lý do: {reason}";

            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã từ chối nạp cọc thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi từ chối nạp cọc: " + ex.Message;
            }

            return RedirectToAction("ApprovePayments", "Admin");
        }
    }
}