using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chamsoc.Data;
using Chamsoc.Models;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Chamsoc.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class BalanceHistoryController : Controller
    {
        private readonly AppDbContext _context;

        public BalanceHistoryController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy tổng số dư hệ thống từ tiền cọc của các công việc đã hoàn thành
            var totalBalance = await _context.CareJobs
                .Where(j => j.Status == "Hoàn thành" && j.DepositMade)
                .SumAsync(j => j.Deposit);

            // Lấy lịch sử giao dịch gần đây với thông tin người dùng
            var recentTransactions = await _context.CareJobs
                .Include(j => j.Senior)
                .Include(j => j.Caregiver)
                .Where(j => j.Status == "Hoàn thành" && j.DepositMade)
                .OrderByDescending(j => j.CreatedAt)
                .Take(50)
                .Select(j => new TransactionViewModel
                {
                    Id = j.Id,
                    SeniorName = j.Senior.FullName,
                    CaregiverName = j.Caregiver.FullName,
                    ServiceType = j.ServiceType,
                    TotalAmount = j.Deposit,
                    Status = j.Status,
                    PaymentStatus = j.PaymentStatus,
                    CreatedAt = j.CreatedAt,
                    StartTime = j.StartTime,
                    EndTime = j.EndTime,
                    Location = j.Location,
                    Description = j.Description
                })
                .ToListAsync();

            // Tính toán thống kê
            var stats = new
            {
                TotalBalance = totalBalance,
                TotalTransactions = await _context.CareJobs
                    .CountAsync(j => j.Status == "Hoàn thành" && j.DepositMade),
                TodayTransactions = await _context.CareJobs
                    .CountAsync(j => j.Status == "Hoàn thành" && 
                        j.DepositMade && 
                        j.CreatedAt.Date == DateTime.Today),
                MonthlyTransactions = await _context.CareJobs
                    .CountAsync(j => j.Status == "Hoàn thành" && 
                        j.DepositMade && 
                        j.CreatedAt.Month == DateTime.Now.Month && 
                        j.CreatedAt.Year == DateTime.Now.Year)
            };

            ViewBag.Stats = stats;
            return View(recentTransactions);
        }
    }
} 