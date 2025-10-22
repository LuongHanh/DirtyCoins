using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class FeedbackLike
    {
        [Key]
        public int IdFeedbackLike { get; set; }

        [Required]
        public int IdFeedback { get; set; }

        [Required]
        public int IdUser { get; set; }

        public DateTime LikeDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("IdFeedback")]
        public virtual Feedback Feedback { get; set; }

        // Giả sử bạn có bảng User/Customer
        [ForeignKey("IdUser")]
        public virtual User User { get; set; }
    }
}
