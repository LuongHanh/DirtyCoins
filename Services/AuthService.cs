// Services/AuthService.cs
using DirtyCoins.Data;
using DirtyCoins.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DirtyCoins.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _db;
        public AuthService(ApplicationDbContext db) { _db = db; }

        public async Task<User> ValidateLocalLoginAsync(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return null;
            if (VerifyHash(password, user.Password)) return user;
            return null;
        }

        // Simple hash (for demo). In production use BCrypt/Argon2.
        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        private static bool VerifyHash(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}
