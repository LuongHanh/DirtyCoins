using System;
using System.Security.Cryptography;

namespace DirtyCoins.Security
{
    public static class PasswordHasherWithFixedSalt
    {
        /// <summary>
        /// Tạo hash password theo Identity v3 với salt cứng
        /// </summary>
        /// <param name="password">Password input</param>
        /// <returns>Hash Base64</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // ASP.NET Identity v3 format: [version(1)][PRF(4)][iter(4)][salt(16)][subkey(32)]
            byte[] salt = new byte[16]
            {
                0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,
                0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x10
            }; // Salt cứng

            int iterCount = 10000;
            int subkeyLength = 32;
            byte version = 0x01;
            uint prf = 1; // HMACSHA256

            byte[] subkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterCount, HashAlgorithmName.SHA256))
            {
                subkey = deriveBytes.GetBytes(subkeyLength);
            }

            byte[] outputBytes = new byte[1 + 4 + 4 + salt.Length + subkey.Length];

            int offset = 0;
            outputBytes[offset++] = version;

            // PRF = UInt32, big-endian
            outputBytes[offset++] = (byte)((prf >> 24) & 0xFF);
            outputBytes[offset++] = (byte)((prf >> 16) & 0xFF);
            outputBytes[offset++] = (byte)((prf >> 8) & 0xFF);
            outputBytes[offset++] = (byte)(prf & 0xFF);

            // Iteration count = UInt32, big-endian
            outputBytes[offset++] = (byte)((iterCount >> 24) & 0xFF);
            outputBytes[offset++] = (byte)((iterCount >> 16) & 0xFF);
            outputBytes[offset++] = (byte)((iterCount >> 8) & 0xFF);
            outputBytes[offset++] = (byte)(iterCount & 0xFF);

            // Copy salt
            Array.Copy(salt, 0, outputBytes, offset, salt.Length);
            offset += salt.Length;

            // Copy subkey
            Array.Copy(subkey, 0, outputBytes, offset, subkey.Length);

            return Convert.ToBase64String(outputBytes);
        }
    }
}
