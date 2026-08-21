using System;
using System.Data.SQLite;
using System.Globalization;

namespace SnapAfghanistan.Native.Data
{
    internal static class IdentitySchema
    {
        public static void Ensure()
        {
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                Execute(connection, transaction, @"
CREATE TABLE IF NOT EXISTS users(
    id TEXT PRIMARY KEY,
    username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    display_name TEXT NOT NULL,
    role TEXT NOT NULL,
    password_salt TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    password_iterations INTEGER NOT NULL DEFAULT 260000,
    is_active INTEGER NOT NULL DEFAULT 1,
    must_change_password INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_users_active_role ON users(is_active,role);
CREATE TABLE IF NOT EXISTS user_permissions(
    user_id TEXT NOT NULL,
    permission_key TEXT NOT NULL,
    allowed INTEGER NOT NULL,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(user_id,permission_key),
    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_user_permissions_user ON user_permissions(user_id);
");

                MigrateLegacyAdministrator(connection, transaction);
                transaction.Commit();
            }
        }

        private static void MigrateLegacyAdministrator(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (Scalar(connection, transaction, "SELECT COUNT(*) FROM users") > 0) return;

            var hash = Setting(connection, transaction, "auth_password_hash");
            var salt = Setting(connection, transaction, "auth_password_salt");
            if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt)) return;

            var username = Setting(connection, transaction, "auth_username");
            if (string.IsNullOrWhiteSpace(username)) username = "admin";

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO users(id,username,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password)
VALUES(@id,@username,@display,'admin',@salt,@hash,260000,1,0)";
                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("@username", username.Trim());
                command.Parameters.AddWithValue("@display", "مدیر سیستم");
                command.Parameters.AddWithValue("@salt", salt);
                command.Parameters.AddWithValue("@hash", hash);
                command.ExecuteNonQuery();
            }
        }

        private static string Setting(SQLiteConnection connection, SQLiteTransaction transaction, string key)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT value FROM app_settings WHERE key=@key LIMIT 1";
                command.Parameters.AddWithValue("@key", key);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            }
        }

        private static long Scalar(SQLiteConnection connection, SQLiteTransaction transaction, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                return Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
            }
        }

        private static void Execute(SQLiteConnection connection, SQLiteTransaction transaction, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
