using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class MaintenanceLog
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int? IdUser { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
        public bool IsImportant { get; set; } = false;

        public User? User { get; set; }
    }
}
