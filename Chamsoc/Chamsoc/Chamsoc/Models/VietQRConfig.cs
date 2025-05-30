namespace Chamsoc.Models
{
    public class VietQRConfig
    {
        public string AccountNo { get; set; }
        public string AccountName { get; set; }
        public string BankName { get; set; }
        public string BankCode { get; set; }
        public string Template { get; set; }

        public string GenerateQRUrl(decimal amount, string content)
        {
            return $"https://img.vietqr.io/image/{BankCode}-{AccountNo}-{Template}.png?amount={amount}&addInfo={content}";
        }
    }
}