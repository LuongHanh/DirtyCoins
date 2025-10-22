using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class FeedbackReply
    {
        [Key]
        public int IdReply { get; set; }

        [ForeignKey("Feedback")]
        public int IdFeedback { get; set; }
        public Feedback Feedback { get; set; }

        [Required, MaxLength(1000)]
        public string ReplyContent { get; set; }

        public DateTime ReplyDate { get; set; } = DateTime.UtcNow;

        // 🔹 Dùng chung cho cả nhân viên & khách hàng
        [Required, MaxLength(255)]
        public string UserName { get; set; }

        // 🔹 Cờ xác định phản hồi của nhân viên hay khách hàng
        public bool IsStaff { get; set; } = false;
    }
}
