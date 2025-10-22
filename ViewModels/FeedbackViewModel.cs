// File: ViewModels/FeedbackViewModel.cs
using System;
using System.Collections.Generic;

namespace DirtyCoins.ViewModels
{
    public class FeedbackViewModel
    {
        public int IdFeedback { get; set; }
        public string UserName { get; set; } = "Người dùng";
        public int Rating { get; set; }
        public string Content { get; set; } = "";
        public int LikeCount { get; set; } = 0;
        public List<ReplyViewModel> Replies { get; set; } = new();
    }
}
