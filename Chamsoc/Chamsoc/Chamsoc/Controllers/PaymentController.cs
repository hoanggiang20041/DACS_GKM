using Chamsoc.Data;
using Chamsoc.Models;
using Microsoft.AspNetCore.Mvc;

namespace Chamsoc.Controllers
{
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;

        public PaymentController(AppDbContext context)
        {
            _context = context;
        }

        // Các action xử lý thanh toán sẽ được thêm ở đây

        [HttpGet("VietQR/GenerateQR")]
        public IActionResult GenerateQRCode(int jobId)
        {
            // Lấy thông tin công việc từ database
            var job = _context.CareJobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                return NotFound();
            }

            // Tạo nội dung QR code theo chuẩn VietQR
            string qrContent = $"""
                bankId=TCB&accountNumber=4773777777&amount={job.Deposit}&content=NAPCOC_{job.Id}&accountName=VO NHAT KHANH
                """;

            // Tạo URL hình ảnh QR code từ VietQRConfig
            var qrConfig = new VietQRConfig();
            string qrUrl = qrConfig.GenerateQRUrl(job.Deposit, $"NAPCOC_{job.Id}");
            
            // Set các giá trị vào ViewBag
            ViewBag.QRUrl = qrUrl;
            ViewBag.Amount = job.Deposit;
            ViewBag.AccountNo = qrConfig.AccountNo;
            ViewBag.AccountName = qrConfig.AccountName;
            ViewBag.Description = $"NAPCOC_{job.Id}";
            
            // Trả về View
        return View("~/Views/VietQR/GenerateQR.cshtml", job);
    }

    [HttpPost("MarkAsTransferred")]
    public IActionResult MarkAsTransferred(int jobId)
    {
        var job = _context.CareJobs.FirstOrDefault(j => j.Id == jobId);
        if (job == null)
        {
            return NotFound();
        }

        job.Status = "Đang đợi duyệt";
        _context.SaveChanges();

        // Ghi log thông báo cho admin
        _context.Logs.Add(new Log {
            Message = $"Công việc {jobId} đã được đánh dấu đã thanh toán, chờ xác nhận",
            CreatedAt = DateTime.Now,
            Type = "PaymentNotification"
        });
        _context.SaveChanges();
        
        // Chuyển hướng về trang Index của CareJobs
        return RedirectToAction("Index", "CareJobs");
    }
}
}