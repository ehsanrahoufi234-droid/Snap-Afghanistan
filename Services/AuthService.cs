using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using SnapAfghanistan.Native.Data;

namespace SnapAfghanistan.Native.Services
{
    public sealed class UserAccount
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "employee";
        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; }
        public string LastLoginAt { get; set; } = "";
        public string RoleName => PermissionCatalog.RoleName(Role);
    }

    public sealed class UserSession
    {
        private readonly HashSet<string> _permissions;

        public UserSession(UserAccount user, IEnumerable<string> permissions, string machineName = "")
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            _permissions = new HashSet<string>(permissions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            MachineName = string.IsNullOrWhiteSpace(machineName) ? Environment.MachineName : machineName.Trim();
        }

        public UserAccount User { get; }
        public string MachineName { get; }
        public IReadOnlyCollection<string> Permissions => _permissions;
        public bool Has(string permission) => !string.IsNullOrWhiteSpace(permission) && _permissions.Contains(permission);
        public string ActorLabel => User.DisplayName + " (" + User.Username + ")";
    }

    public static class SessionContext
    {
        private static readonly AsyncLocal<UserSession?> Holder = new AsyncLocal<UserSession?>();
        public static UserSession? Current => Holder.Value;
        public static bool IsAuthenticated => Current != null;
        public static string ActorLabel => Current?.ActorLabel ?? "system";
        public static string MachineName => Current?.MachineName ?? Environment.MachineName;
        public static bool Has(string permission) => Current != null && Current.Has(permission);
        public static void Start(UserSession session) => Holder.Value = session ?? throw new ArgumentNullException(nameof(session));
        public static void End() => Holder.Value = null;
    }

    public static class PermissionCatalog
    {
        public const string MembersView = "members.view";
        public const string MembersWrite = "members.write";
        public const string MembersDelete = "members.delete";
        public const string CentersView = "centers.view";
        public const string CentersWrite = "centers.write";
        public const string CentersDelete = "centers.delete";
        public const string SubscriptionsView = "subscriptions.view";
        public const string SubscriptionsWrite = "subscriptions.write";
        public const string SubscriptionsDelete = "subscriptions.delete";
        public const string ReportsView = "reports.view";
        public const string NotesView = "notes.view";
        public const string NotesWrite = "notes.write";
        public const string NotesDelete = "notes.delete";
        public const string SettingsGeneral = "settings.general";
        public const string BackupCreate = "backup.create";
        public const string BackupRestore = "backup.restore";
        public const string TrashPurge = "trash.purge";
        public const string UsersManage = "users.manage";

        public static readonly string[] All =
        {
            MembersView, MembersWrite, MembersDelete,
            CentersView, CentersWrite, CentersDelete,
            SubscriptionsView, SubscriptionsWrite, SubscriptionsDelete,
            ReportsView,
            NotesView, NotesWrite, NotesDelete,
            SettingsGeneral, BackupCreate, BackupRestore, TrashPurge, UsersManage
        };

        public static IReadOnlyCollection<string> Defaults(string role)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in All) result.Add(item);
                return result;
            }

            if (string.Equals(role, "accountant", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in new[]
                {
                    MembersView, CentersView,
                    SubscriptionsView, SubscriptionsWrite, SubscriptionsDelete,
                    ReportsView, NotesView, NotesWrite,
                    BackupCreate
                }) result.Add(item);
                return result;
            }

            foreach (var item in new[]
            {
                MembersView, MembersWrite,
                CentersView, CentersWrite,
                SubscriptionsView,
                ReportsView,
                NotesView, NotesWrite
            }) result.Add(item);
            return result;
        }

        public static string RoleName(string role)
        {
            if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)) return "مدیر";
            if (string.Equals(role, "accountant", StringComparison.OrdinalIgnoreCase)) return "حسابدار";
            return "کارمند";
        }

        public static string PermissionTitle(string key)
        {
            switch (key)
            {
                case MembersView: return "مشاهده اعضا";
                case MembersWrite: return "ثبت و ویرایش اعضا";
                case MembersDelete: return "بایگانی و حذف اعضا";
                case CentersView: return "مشاهده مراکز و سکتورها";
                case CentersWrite: return "ثبت و ویرایش مراکز";
                case CentersDelete: return "بایگانی و حذف مراکز";
                case SubscriptionsView: return "مشاهده اشتراک‌ها";
                case SubscriptionsWrite: return "ثبت و اصلاح پرداخت";
                case SubscriptionsDelete: return "حذف پرداخت";
                case ReportsView: return "گزارش‌ها و PDF";
                case NotesView: return "مشاهده یادداشت‌ها";
                case NotesWrite: return "ثبت و ویرایش یادداشت";
                case NotesDelete: return "حذف یادداشت";
                case SettingsGeneral: return "تنظیمات عمومی و سطل زباله";
                case BackupCreate: return "ایجاد بکاپ";
                case BackupRestore: return "بازیابی بکاپ";
                case TrashPurge: return "حذف دایمی";
                case UsersManage: return "مدیریت کاربران و صلاحیت‌ها";
                default: return key;
            }
        }

        public static bool IsValidRole(string role)
        {
            return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "accountant", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "employee", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class AuthService
    {
        private const int DefaultIterations = 260000;

        public bool IsConfigured()
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM users";
                return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0;
            }
        }

        public string Username
        {
            get
            {
                if (SessionContext.Current != null) return SessionContext.Current.User.Username;
                using (var connection = Database.Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT username FROM users WHERE is_active=1 ORDER BY CASE role WHEN 'admin' THEN 0 ELSE 1 END,created_at LIMIT 1";
                    return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "admin";
                }
            }
        }

        public void SetPassword(string password, string username = "admin")
        {
            if (IsConfigured()) throw new InvalidOperationException("حساب مدیر قبلاً ساخته شده است.");
            CreateFirstAdministrator(username, "مدیر سیستم", password);
        }

        public UserSession Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("نام کاربری را وارد کنید.");
            using (var connection = Database.Open())
            {
                UserAccount? user = null;
                string saltText = "";
                string hashText = "";
                var iterations = DefaultIterations;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT id,username,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password,COALESCE(last_login_at,'')
FROM users WHERE username=@username COLLATE NOCASE LIMIT 1";
                    command.Parameters.AddWithValue("@username", username.Trim());
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidOperationException("نام کاربری یا رمز عبور نادرست است.");
                        user = new UserAccount
                        {
                            Id = Text(reader, 0), Username = Text(reader, 1), DisplayName = Text(reader, 2), Role = Text(reader, 3),
                            IsActive = Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) != 0,
                            MustChangePassword = Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture) != 0,
                            LastLoginAt = Text(reader, 9)
                        };
                        saltText = Text(reader, 4);
                        hashText = Text(reader, 5);
                        iterations = Math.Max(100000, Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture));
                    }
                }

                if (user == null || !user.IsActive) throw new InvalidOperationException("این حساب کاربری غیرفعال است.");
                if (!VerifyHash(password ?? "", saltText, hashText, iterations)) throw new InvalidOperationException("نام کاربری یا رمز عبور نادرست است.");

                using (var update = connection.CreateCommand())
                {
                    update.CommandText = "UPDATE users SET last_login_at=CURRENT_TIMESTAMP WHERE id=@id";
                    update.Parameters.AddWithValue("@id", user.Id);
                    update.ExecuteNonQuery();
                }

                var session = new UserSession(user, ResolvePermissions(connection, user.Id, user.Role), Environment.MachineName);
                SessionContext.Start(session);
                return session;
            }
        }

        public bool Verify(string username, string password)
        {
            try { Authenticate(username, password); return true; }
            catch { SessionContext.End(); return false; }
        }

        public void ChangePassword(string currentPassword, string newPassword, string username)
        {
            var current = SessionContext.Current?.User ?? throw new InvalidOperationException("جلسه کاربری فعال نیست.");
            if (!VerifyPasswordForUser(current.Id, currentPassword)) throw new InvalidOperationException("رمز فعلی نادرست است.");
            ValidateUsername(username);
            ValidatePassword(newPassword);
            var credentials = HashPassword(newPassword, DefaultIterations);
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"UPDATE users SET username=@username,password_salt=@salt,password_hash=@hash,password_iterations=@iterations,must_change_password=0,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                command.Parameters.AddWithValue("@username", username.Trim());
                command.Parameters.AddWithValue("@salt", credentials.Item1);
                command.Parameters.AddWithValue("@hash", credentials.Item2);
                command.Parameters.AddWithValue("@iterations", DefaultIterations);
                command.Parameters.AddWithValue("@id", current.Id);
                try { command.ExecuteNonQuery(); }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint) { throw new InvalidOperationException("این نام کاربری قبلاً استفاده شده است."); }
                transaction.Commit();
            }
            current.Username = username.Trim();
            current.MustChangePassword = false;
        }

        public IReadOnlyList<UserAccount> GetUsers()
        {
            var result = new List<UserAccount>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id,username,display_name,role,is_active,must_change_password,COALESCE(last_login_at,'') FROM users ORDER BY CASE role WHEN 'admin' THEN 0 WHEN 'accountant' THEN 1 ELSE 2 END,display_name";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new UserAccount
                    {
                        Id = Text(reader, 0), Username = Text(reader, 1), DisplayName = Text(reader, 2), Role = Text(reader, 3),
                        IsActive = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                        MustChangePassword = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0,
                        LastLoginAt = Text(reader, 6)
                    });
                }
            }
            return result;
        }

        public UserAccount CreateUser(string username, string displayName, string role, string temporaryPassword)
        {
            Require(PermissionCatalog.UsersManage);
            ValidateUsername(username); ValidateDisplayName(displayName); ValidateRole(role); ValidatePassword(temporaryPassword);
            var credentials = HashPassword(temporaryPassword, DefaultIterations);
            var user = new UserAccount { Id = Guid.NewGuid().ToString("N"), Username = username.Trim(), DisplayName = displayName.Trim(), Role = role, IsActive = true, MustChangePassword = true };
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO users(id,username,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password)
VALUES(@id,@username,@display,@role,@salt,@hash,@iterations,1,1)";
                command.Parameters.AddWithValue("@id", user.Id); command.Parameters.AddWithValue("@username", user.Username); command.Parameters.AddWithValue("@display", user.DisplayName);
                command.Parameters.AddWithValue("@role", user.Role); command.Parameters.AddWithValue("@salt", credentials.Item1); command.Parameters.AddWithValue("@hash", credentials.Item2); command.Parameters.AddWithValue("@iterations", DefaultIterations);
                try { command.ExecuteNonQuery(); }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint) { throw new InvalidOperationException("این نام کاربری قبلاً استفاده شده است."); }
            }
            AuditUser(user.Id, "create-user", user.Username + " / " + user.Role);
            return user;
        }

        public void UpdateUser(string userId, string displayName, string role, bool isActive)
        {
            Require(PermissionCatalog.UsersManage); ValidateDisplayName(displayName); ValidateRole(role);
            var current = SessionContext.Current?.User;
            if (current != null && string.Equals(current.Id, userId, StringComparison.OrdinalIgnoreCase) && !isActive) throw new InvalidOperationException("حسابی که با آن وارد شده‌اید را نمی‌توانید غیرفعال کنید.");
            if (!isActive && IsLastActiveAdministrator(userId)) throw new InvalidOperationException("آخرین مدیر فعال سیستم را نمی‌توان غیرفعال کرد.");
            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) && IsLastActiveAdministrator(userId)) throw new InvalidOperationException("نقش آخرین مدیر فعال سیستم را نمی‌توان تغییر داد.");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE users SET display_name=@display,role=@role,is_active=@active,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                command.Parameters.AddWithValue("@display", displayName.Trim()); command.Parameters.AddWithValue("@role", role); command.Parameters.AddWithValue("@active", isActive ? 1 : 0); command.Parameters.AddWithValue("@id", userId);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("کاربر پیدا نشد.");
            }
            AuditUser(userId, "update-user", displayName.Trim() + " / " + role + " / " + (isActive ? "active" : "inactive"));
        }

        public void ResetPassword(string userId, string temporaryPassword)
        {
            Require(PermissionCatalog.UsersManage); ValidatePassword(temporaryPassword);
            var credentials = HashPassword(temporaryPassword, DefaultIterations);
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE users SET password_salt=@salt,password_hash=@hash,password_iterations=@iterations,must_change_password=1,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                command.Parameters.AddWithValue("@salt", credentials.Item1); command.Parameters.AddWithValue("@hash", credentials.Item2); command.Parameters.AddWithValue("@iterations", DefaultIterations); command.Parameters.AddWithValue("@id", userId);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("کاربر پیدا نشد.");
            }
            AuditUser(userId, "reset-password", "temporary password issued");
        }

        public void SetPermissionOverride(string userId, string permission, bool allowed)
        {
            Require(PermissionCatalog.UsersManage);
            if (string.IsNullOrWhiteSpace(permission)) throw new InvalidOperationException("مجوز نامعتبر است.");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT OR REPLACE INTO user_permissions(user_id,permission_key,allowed,updated_at) VALUES(@user,@permission,@allowed,CURRENT_TIMESTAMP)";
                command.Parameters.AddWithValue("@user", userId); command.Parameters.AddWithValue("@permission", permission.Trim()); command.Parameters.AddWithValue("@allowed", allowed ? 1 : 0); command.ExecuteNonQuery();
            }
            AuditUser(userId, "permission-override", permission + "=" + (allowed ? "allow" : "deny"));
        }

        public void ClearPermissionOverrides(string userId)
        {
            Require(PermissionCatalog.UsersManage);
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand()) { command.CommandText = "DELETE FROM user_permissions WHERE user_id=@id"; command.Parameters.AddWithValue("@id", userId); command.ExecuteNonQuery(); }
            AuditUser(userId, "permission-reset", "role defaults restored");
        }

        public static void Require(string permission)
        {
            if (!SessionContext.Has(permission)) throw new UnauthorizedAccessException("شما اجازه انجام این عملیات را ندارید.");
        }

        private void CreateFirstAdministrator(string username, string displayName, string password)
        {
            ValidateUsername(username); ValidateDisplayName(displayName); ValidatePassword(password);
            var credentials = HashPassword(password, DefaultIterations); var id = Guid.NewGuid().ToString("N");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO users(id,username,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password) VALUES(@id,@username,@display,'admin',@salt,@hash,@iterations,1,0)";
                command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@username", username.Trim()); command.Parameters.AddWithValue("@display", displayName.Trim()); command.Parameters.AddWithValue("@salt", credentials.Item1); command.Parameters.AddWithValue("@hash", credentials.Item2); command.Parameters.AddWithValue("@iterations", DefaultIterations); command.ExecuteNonQuery();
            }
        }

        private static IEnumerable<string> ResolvePermissions(SQLiteConnection connection, string userId, string role)
        {
            var permissions = new HashSet<string>(PermissionCatalog.Defaults(role), StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT permission_key,allowed FROM user_permissions WHERE user_id=@id"; command.Parameters.AddWithValue("@id", userId);
                using (var reader = command.ExecuteReader()) while (reader.Read()) { var key = Text(reader, 0); var allowed = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0; if (allowed) permissions.Add(key); else permissions.Remove(key); }
            }
            return permissions;
        }

        private static bool VerifyPasswordForUser(string userId, string password)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT password_salt,password_hash,password_iterations FROM users WHERE id=@id LIMIT 1"; command.Parameters.AddWithValue("@id", userId);
                using (var reader = command.ExecuteReader()) { if (!reader.Read()) return false; return VerifyHash(password ?? "", Text(reader, 0), Text(reader, 1), Math.Max(100000, Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture))); }
            }
        }

        private static Tuple<string, string> HashPassword(string password, int iterations)
        {
            var salt = new byte[16]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt); byte[] hash;
            using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)) hash = derive.GetBytes(32);
            return Tuple.Create(ToHex(salt), ToHex(hash));
        }

        private static bool VerifyHash(string password, string saltText, string hashText, int iterations)
        {
            try { var salt = FromHex(saltText); var expected = FromHex(hashText); byte[] actual; using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)) actual = derive.GetBytes(32); return FixedTimeEquals(actual, expected); }
            catch { return false; }
        }

        private static bool IsLastActiveAdministrator(string targetUserId)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand()) { command.CommandText = "SELECT COUNT(*) FROM users WHERE role='admin' AND is_active=1 AND id<>@id"; command.Parameters.AddWithValue("@id", targetUserId); return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0; }
        }

        private static void AuditUser(string userId, string action, string summary)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO audit_log(entity_type,entity_id,action,summary,actor,machine_name) VALUES('user',@id,@action,@summary,@actor,@machine)";
                command.Parameters.AddWithValue("@id", userId); command.Parameters.AddWithValue("@action", action); command.Parameters.AddWithValue("@summary", summary ?? ""); command.Parameters.AddWithValue("@actor", SessionContext.ActorLabel); command.Parameters.AddWithValue("@machine", SessionContext.MachineName); command.ExecuteNonQuery();
            }
        }

        private static void ValidateUsername(string username) { if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3) throw new InvalidOperationException("نام کاربری باید حداقل ۳ نویسه داشته باشد."); if (username.Trim().Length > 64) throw new InvalidOperationException("نام کاربری بیش از حد طولانی است."); }
        private static void ValidateDisplayName(string displayName) { if (string.IsNullOrWhiteSpace(displayName)) throw new InvalidOperationException("نام نمایشی کاربر ضروری است."); if (displayName.Trim().Length > 100) throw new InvalidOperationException("نام نمایشی بیش از حد طولانی است."); }
        private static void ValidateRole(string role) { if (!PermissionCatalog.IsValidRole(role)) throw new InvalidOperationException("نقش کاربر نامعتبر است."); }
        private static void ValidatePassword(string password) { if (password == null || password.Length < 8) throw new InvalidOperationException("رمز باید حداقل ۸ نویسه داشته باشد."); }
        private static bool FixedTimeEquals(byte[] left, byte[] right) { if (left.Length != right.Length) return false; var difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0; }
        private static string Text(SQLiteDataReader reader, int index) => reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? "";
        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static byte[] FromHex(string text) { if (string.IsNullOrWhiteSpace(text) || text.Length % 2 != 0) throw new FormatException(); var bytes = new byte[text.Length / 2]; for (var i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16); return bytes; }
    }
}
