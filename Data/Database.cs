using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace SnapAfghanistan.Native.Data
{
    public static class Database
    {
        public const int CurrentSchemaVersion = 5;

        public static readonly string RootDirectory = ResolveRootDirectory();
        public static readonly string DataDirectory = System.IO.Path.Combine(RootDirectory, "Data");
        public static readonly string AttachmentsDirectory = System.IO.Path.Combine(DataDirectory, "attachments");
        public static readonly string BackupsDirectory = System.IO.Path.Combine(DataDirectory, "backups");
        public static readonly string LogDirectory = System.IO.Path.Combine(RootDirectory, "Logs");
        public static readonly string PathName = System.IO.Path.Combine(DataDirectory, "snap.db");

        private static string ResolveRootDirectory()
        {
            var overridePath = Environment.GetEnvironmentVariable("SNAP_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(overridePath)) return System.IO.Path.GetFullPath(overridePath.Trim());
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapAfghanistan");
        }

        public static SQLiteConnection Open()
        {
            var connection = new SQLiteConnection(
                "Data Source=" + PathName + ";Version=3;Foreign Keys=True;Pooling=True;Max Pool Size=20;Journal Mode=WAL;");
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000; PRAGMA synchronous=NORMAL;";
                command.ExecuteNonQuery();
            }
            return connection;
        }

        public static void Initialize()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(AttachmentsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
            Directory.CreateDirectory(LogDirectory);

            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY;";
                command.ExecuteNonQuery();
                command.CommandText = Schema;
                command.ExecuteNonQuery();
            }

            ApplyMigrations();
            SeedDefaults();
            IdentitySchema.Ensure();
            SetSetting("schema_version", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
        }

        public static string GetSetting(string key, string fallback = "")
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT value FROM app_settings WHERE key=@key LIMIT 1";
                command.Parameters.AddWithValue("@key", key);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
            }
        }

        public static void SetSetting(string key, string value)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT OR REPLACE INTO app_settings(key,value,updated_at) VALUES(@key,@value,CURRENT_TIMESTAMP)";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? "");
                command.ExecuteNonQuery();
            }
        }

        public static string IntegrityCheck()
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA quick_check;";
                return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "unknown";
            }
        }

        public static void Checkpoint()
        {
            SQLiteConnection.ClearAllPools();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }
            SQLiteConnection.ClearAllPools();
        }

        public static void LogError(Exception exception)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var path = System.IO.Path.Combine(LogDirectory, "snap-error.log");
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Environment.NewLine +
                    exception + Environment.NewLine + new string('-', 60) + Environment.NewLine);
            }
            catch
            {
                // Logging must never prevent the application from opening.
            }
        }

        private static void ApplyMigrations()
        {
            EnsureColumn("sectors", "deleted_at", "TEXT");
            EnsureColumn("members", "institution", "TEXT");
            EnsureColumn("members", "archived_at", "TEXT");
            EnsureColumn("members", "deleted_at", "TEXT");
            EnsureColumn("partners", "fee_basis", "TEXT DEFAULT 'بدون حق‌الخدمت'");
            EnsureColumn("partners", "fee_amount", "NUMERIC DEFAULT 0");
            EnsureColumn("partners", "monthly_subscription", "NUMERIC DEFAULT 0");
            EnsureColumn("partners", "subscription_start", "TEXT");
            EnsureColumn("partners", "next_due_date", "TEXT");
            EnsureColumn("partners", "subscription_suspended", "INTEGER DEFAULT 0");
            EnsureColumn("partners", "archived_at", "TEXT");
            EnsureColumn("partners", "deleted_at", "TEXT");
            EnsureColumn("notes", "deleted_at", "TEXT");
            EnsureColumn("audit_log", "actor", "TEXT");
            EnsureColumn("audit_log", "machine_name", "TEXT");

            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version=" + CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture) + ";";
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureColumn(string table, string column, string definition)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + table + ")";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) existing.Add(reader.GetString(1));
                }

                if (!existing.Contains(column))
                {
                    command.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition;
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void SeedDefaults()
        {
            var settings = new Dictionary<string, string>
            {
                { "company_name", "اسنپ افغانستان" },
                { "province", "هرات" },
                { "member_code_prefix", "SNP-HRT" },
                { "due_reminder_days", "7" },
                { "auto_backup", "1" },
                { "last_auto_backup_date", "" }
            };

            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var pair in settings)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "INSERT OR IGNORE INTO app_settings(key,value) VALUES(@key,@value)";
                        command.Parameters.AddWithValue("@key", pair.Key);
                        command.Parameters.AddWithValue("@value", pair.Value);
                        command.ExecuteNonQuery();
                    }
                }

                foreach (var name in DefaultSectors)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "INSERT OR IGNORE INTO sectors(id,name,description,status) VALUES(@id,@name,'','فعال')";
                        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
                        command.Parameters.AddWithValue("@name", name);
                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        private static readonly string[] DefaultSectors =
        {
            "صحت", "دوا و تجهیزات صحی", "مواد غذایی", "آموزش", "کتاب و قرطاسیه",
            "پوشاک", "رستورانت و غذا", "ترانسپورت", "تعمیرات وسایط", "تکنالوژی و کمپیوتر",
            "انترنت و مخابرات", "لوازم خانه", "تعمیرات ساختمان", "چاپ و تبلیغات", "ورزش",
            "خدمات حقوقی", "عینک و بینایی", "لابراتوار", "فرهنگی و هنری", "سایر خدمات"
        };

        private const string Schema = @"
CREATE TABLE IF NOT EXISTS app_settings(
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS counters(
    name TEXT PRIMARY KEY,
    value INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS sectors(
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    status TEXT NOT NULL DEFAULT 'فعال',
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
    version INTEGER DEFAULT 1,
    deleted_at TEXT
);
CREATE TABLE IF NOT EXISTS members(
    id TEXT PRIMARY KEY,
    member_code TEXT NOT NULL UNIQUE,
    member_type TEXT NOT NULL,
    first_name TEXT NOT NULL,
    father_name TEXT NOT NULL,
    tazkira_no TEXT NOT NULL UNIQUE,
    phone TEXT,
    original_address TEXT NOT NULL,
    current_address TEXT NOT NULL,
    institution TEXT,
    status TEXT NOT NULL DEFAULT 'فعال',
    notes TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
    version INTEGER DEFAULT 1,
    archived_at TEXT,
    deleted_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_members_search ON members(first_name,father_name,tazkira_no,phone,member_code);
CREATE INDEX IF NOT EXISTS idx_members_type_status ON members(member_type,status,deleted_at);
CREATE TABLE IF NOT EXISTS partners(
    id TEXT PRIMARY KEY,
    partner_code TEXT NOT NULL UNIQUE,
    sector_id TEXT NOT NULL,
    legal_name TEXT NOT NULL,
    trade_name TEXT,
    license_no TEXT,
    representative TEXT,
    phone TEXT,
    address TEXT,
    contract_start TEXT,
    contract_end TEXT,
    discount_rate TEXT,
    fee_type TEXT DEFAULT 'مبلغ ثابت',
    fee_rate TEXT,
    opening_balance NUMERIC DEFAULT 0,
    status TEXT DEFAULT 'فعال',
    notes TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
    version INTEGER DEFAULT 1,
    fee_basis TEXT DEFAULT 'بدون حق‌الخدمت',
    fee_amount NUMERIC DEFAULT 0,
    fee_start_date TEXT,
    monthly_subscription NUMERIC DEFAULT 0,
    subscription_start TEXT,
    next_due_date TEXT,
    subscription_suspended INTEGER DEFAULT 0,
    archived_at TEXT,
    deleted_at TEXT,
    FOREIGN KEY(sector_id) REFERENCES sectors(id)
);
CREATE INDEX IF NOT EXISTS idx_partners_sector_status ON partners(sector_id,status,deleted_at);
CREATE INDEX IF NOT EXISTS idx_partners_due ON partners(next_due_date,subscription_suspended,deleted_at);
CREATE TABLE IF NOT EXISTS subscription_payments(
    id TEXT PRIMARY KEY,
    partner_id TEXT NOT NULL,
    payment_date TEXT NOT NULL,
    amount NUMERIC NOT NULL,
    receipt_no TEXT NOT NULL UNIQUE,
    covered_months INTEGER DEFAULT 1,
    previous_due_date TEXT,
    new_due_date TEXT NOT NULL,
    notes TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(partner_id) REFERENCES partners(id)
);
CREATE INDEX IF NOT EXISTS idx_payments_date ON subscription_payments(payment_date,partner_id);
CREATE TABLE IF NOT EXISTS notes(
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    note_type TEXT DEFAULT 'عمومی',
    related_name TEXT,
    priority TEXT DEFAULT 'عادی',
    due_date TEXT,
    status TEXT DEFAULT 'باز',
    body TEXT NOT NULL,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
    deleted_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_notes_status_due ON notes(status,due_date,deleted_at);
CREATE TABLE IF NOT EXISTS member_attachments(
    id TEXT PRIMARY KEY,
    member_id TEXT NOT NULL,
    document_type TEXT DEFAULT 'تذکره',
    original_name TEXT NOT NULL,
    stored_name TEXT NOT NULL,
    mime_type TEXT,
    size_bytes INTEGER NOT NULL,
    sha256 TEXT NOT NULL,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(member_id) REFERENCES members(id)
);
CREATE INDEX IF NOT EXISTS idx_attachments_member ON member_attachments(member_id);
CREATE TABLE IF NOT EXISTS audit_log(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_type TEXT NOT NULL,
    entity_id TEXT NOT NULL,
    action TEXT NOT NULL,
    summary TEXT,
    actor TEXT,
    machine_name TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_audit_entity ON audit_log(entity_type,entity_id,created_at);
";
    }
}
