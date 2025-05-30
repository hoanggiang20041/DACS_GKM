using System;
using System.Collections.Generic;

namespace Chamsoc.Models
{
    public class BalanceHistoryViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public decimal CurrentBalance { get; set; }
        public string AvatarUrl { get; set; }
        public List<TransactionViewModel> RecentTransactions { get; set; }
    }
} 