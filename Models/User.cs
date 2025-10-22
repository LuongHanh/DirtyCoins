// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace DirtyCoins.Models
{
    public class User
    {
        public int IdUser { get; set; }

        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; } // store hashed password

        public string? Email { get; set; }
        public string Role { get; set; } = "Customer"; // mặc định
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; } 
        //Cho phép nullable, vì nếu không C# sẽ mặc định là 0001-01-01 
        //Mà trong SQL Datetime nhỏ nhất là 1753-01-01
        //Sẽ gây lỗi SQL cố convert kiểu datetime2 to datetime
    }
}
