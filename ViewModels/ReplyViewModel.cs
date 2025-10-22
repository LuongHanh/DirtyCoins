// File: ViewModels/ReplyViewModel.cs
using System;

namespace DirtyCoins.ViewModels
{
    public class ReplyViewModel
    {
        public int IdReply { get; set; }
        public string UserName { get; set; } = "Người dùng";
        public string ReplyContent { get; set; } = "";
        public DateTime ReplyDate { get; set; }
        public bool IsStaff { get; set; } = false;
    }
}
