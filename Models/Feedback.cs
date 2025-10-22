// Models/Feedback.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Feedback
    {
        public int IdFeedback { get; set; }

        [ForeignKey("Customer")]
        public int IdCustomer { get; set; }
        public Customer Customer { get; set; }

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }

        public int Rating { get; set; }
        public string Content { get; set; }
        public int LikeCount { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public ICollection<FeedbackReply> Replies { get; set; }
    }
}
