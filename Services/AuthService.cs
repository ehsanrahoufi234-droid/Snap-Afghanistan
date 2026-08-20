using System;
using System.Security.Cryptography;
using SnapAfghanistan.Native.Data;

namespace SnapAfghanistan.Native.Services
{
    public sealed class AuthService
    {
        private const int Iterations = 260000;

        public bool IsConfigured() => Database.GetSetting("auth_password_hash").Length > 0;

        public string Username => Database.GetSetting("auth_username", "admin");

        public void SetPassword(string password, string username = "admin")
        {
            if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("نام کاربری ضروری است.");
            if (password == null || password.Length < 6) throw new InvalidOperationException("رمز باید حداقل ۶ نویسه داشته باشد.");

            var salt = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            byte[] hash;
            using (var derive = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                hash = derive.GetBytes(32);

            Database.SetSetting("auth_username", username.Trim());
            Database.SetSetting("auth_password_salt", ToHex(salt));
            Database.SetSetting("auth_password_hash", ToHex(hash));
        }

        public bool Verify(string username, string password)
        {
            if (!string.Equals(username?.Trim(), Username, StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                var salt = FromHex(Database.GetSetting("auth_password_salt"));
                var expected = FromHex(Database.GetSetting("auth_password_hash"));
                byte[] actual;
                using (var derive = new Rfc2898DeriveBytes(password ?? "", salt, Iterations, HashAlgorithmName.SHA256))
                    actual = derive.GetBytes(32);
                return FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        public void ChangePassword(string currentPassword, string newPassword, string username)
        {
            if (!Verify(Username, currentPassword)) throw new InvalidOperationException("رمز فعلی نادرست است.");
            SetPassword(newPassword, username);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static byte[] FromHex(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length % 2 != 0) throw new FormatException();
            var bytes = new byte[text.Length / 2];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
