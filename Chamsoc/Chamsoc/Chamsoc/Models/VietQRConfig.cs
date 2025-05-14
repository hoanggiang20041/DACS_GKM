namespace Chamsoc.Models
{
    public class VietQRConfig
    {
        public string BankId { get; set; } = "970407"; // Mã ngân hàng Techcombank
        public string AccountNo { get; set; } = "4773777777"; // Số tài khoản
        public string AccountName { get; set; } = "VO NHAT KHANH"; // Tên chủ tài khoản
        public string ApiUrl { get; set; } = "https://img.vietqr.io/image"; // URL API VietQR

        public string GenerateQRUrl(decimal amount, string description)
        {
            var encodedDescription = Uri.EscapeDataString(description);
            return $"{ApiUrl}/{BankId}-{AccountNo}-compact.png?amount={amount}&addInfo={encodedDescription}";
        }
    }
}