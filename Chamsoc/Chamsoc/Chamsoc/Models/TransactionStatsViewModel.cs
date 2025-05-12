namespace Chamsoc.Models
{
    public class TransactionStatsViewModel
    {
        public string SeniorName { get; set; }        // Tên khách hàng tạo đơn
        public string CaregiverName { get; set; }     // Tên người chấp nhận
        public DateTime TransactionDateTime { get; set; } // Ngày giờ giao dịch
        public string SeniorPhone { get; set; }       // SĐT khách hàng
        public string SeniorEmail { get; set; }       // Email khách hàng
        public string CaregiverPhone { get; set; }    // SĐT người chăm sóc
        public string CaregiverEmail { get; set; }    // Email người chăm sóc
        public string ServiceName { get; set; }       // Tên sản phẩm/dịch vụ
        public decimal TotalBill { get; set; }        // Tổng bill
        public decimal Deposit { get; set; }          // Số tiền cọc
        public string Status { get; set; }            // Trạng thái
    }
}