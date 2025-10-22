using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Contact
    {
        [Key]
        public int IdContact { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; }

        [StringLength(200)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? IdUser { get; set; }
        public bool IsHandled { get; set; }

        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store? Store { get; set; }
    }
}
