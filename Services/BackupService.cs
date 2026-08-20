using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using SnapAfghanistan.Native.Data;

namespace SnapAfghanistan.Native.Services
{
    public sealed class BackupService
    {
        private const long MaxBackupEntryBytes = 256L * 1024L * 1024L;
        private const long MaxBackupExtractedBytes = 1024L * 1024L * 1024L;

        public string CreateBackup(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new InvalidOperationException("مسیر بکاپ انتخاب نشده است.");
            if (!destinationPath.EndsWith(".snapbackup", StringComparison.OrdinalIgnoreCase)) destinationPath += ".snapbackup";

            Database.Checkpoint();
            var temporary = NewTemporaryDirectory();
            try
            {
                var databaseCopy = Path.Combine(temporary, "snap.db");
                File.Copy(Database.PathName, databaseCopy, true);
                ValidateDatabase(databaseCopy);

                var attachments = Path.Combine(temporary, "attachments");
                Directory.CreateDirectory(attachments);
                if (Directory.Exists(Database.AttachmentsDirectory)) CopyDirectory(Database.AttachmentsDirectory, attachments);

                File.WriteAllText(Path.Combine(temporary, "manifest.txt"),
                    "Snap Afghanistan Backup" + Environment.NewLine +
                    "version=2" + Environment.NewLine +
                    "schema=" + Database.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                    "created_at=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "database_sha256=" + Sha256(databaseCopy));

                var parent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                var partial = destinationPath + ".partial";
                if (File.Exists(partial)) File.Delete(partial);
                try
                {
                    ZipFile.CreateFromDirectory(temporary, partial, CompressionLevel.Optimal, false);
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);
                    File.Move(partial, destinationPath);
                }
                finally
                {
                    try { if (File.Exists(partial)) File.Delete(partial); } catch { }
                }
                return destinationPath;
            }
            finally
            {
                SafeDeleteDirectory(temporary);
            }
        }

        public string CreateAutomaticBackup()
        {
            Directory.CreateDirectory(Database.BackupsDirectory);
            var path = Path.Combine(Database.BackupsDirectory, "Snap-Auto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".snapbackup");
            var result = CreateBackup(path);
            PruneAutomaticBackups(10);
            return result;
        }

        public string RestoreBackup(string sourcePath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("فایل بکاپ پیدا نشد.", sourcePath);
            var temporary = NewTemporaryDirectory();
            try
            {
                ExtractSafely(sourcePath, temporary);
                var restoredDb = Path.Combine(temporary, "snap.db");
                if (!File.Exists(restoredDb)) throw new InvalidOperationException("این فایل، بکاپ معتبر اسنپ افغانستان نیست.");
                ValidateManifest(temporary, restoredDb);
                ValidateDatabase(restoredDb);

                var emergency = Path.Combine(Database.BackupsDirectory, "Before-Restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".snapbackup");
                CreateBackup(emergency);

                var rollbackDb = Path.Combine(temporary, "rollback.db");
                var rollbackAttachments = Path.Combine(temporary, "rollback-attachments");
                Database.Checkpoint();
                SQLiteConnection.ClearAllPools();
                File.Copy(Database.PathName, rollbackDb, true);
                if (Directory.Exists(Database.AttachmentsDirectory)) CopyDirectory(Database.AttachmentsDirectory, rollbackAttachments);

                try
                {
                    File.Copy(restoredDb, Database.PathName, true);
                    SafeDeleteDirectory(Database.AttachmentsDirectory);
                    Directory.CreateDirectory(Database.AttachmentsDirectory);
                    var restoredAttachments = Path.Combine(temporary, "attachments");
                    if (Directory.Exists(restoredAttachments)) CopyDirectory(restoredAttachments, Database.AttachmentsDirectory);

                    Database.Initialize();
                    if (!string.Equals(Database.IntegrityCheck(), "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("دیتابیس بازیابی‌شده آزمون سلامت را با موفقیت نگذرانید.");
                }
                catch
                {
                    SQLiteConnection.ClearAllPools();
                    File.Copy(rollbackDb, Database.PathName, true);
                    SafeDeleteDirectory(Database.AttachmentsDirectory);
                    Directory.CreateDirectory(Database.AttachmentsDirectory);
                    if (Directory.Exists(rollbackAttachments)) CopyDirectory(rollbackAttachments, Database.AttachmentsDirectory);
                    Database.Initialize();
                    throw;
                }

                return emergency;
            }
            finally
            {
                SafeDeleteDirectory(temporary);
            }
        }

        private static void ValidateDatabase(string path)
        {
            using (var connection = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA integrity_check;";
                    var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
                    if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("دیتابیس داخل بکاپ سالم نیست: " + result);
                }
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('app_settings','members','partners','subscription_payments');";
                    var count = Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
                    if (count < 4) throw new InvalidOperationException("ساختار دیتابیس بکاپ کامل نیست.");
                }
            }
        }

        private static void ValidateManifest(string directory, string databasePath)
        {
            var manifest = Path.Combine(directory, "manifest.txt");
            if (!File.Exists(manifest)) return; // Backward compatibility with the earliest backup format.
            var lines = File.ReadAllLines(manifest);
            if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "Snap Afghanistan Backup", StringComparison.Ordinal))
                throw new InvalidOperationException("شناسه فایل بکاپ معتبر نیست.");
            foreach (var line in lines)
            {
                const string prefix = "database_sha256=";
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var expected = line.Substring(prefix.Length).Trim();
                var actual = Sha256(databasePath);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("فایل بکاپ ناقص یا تغییر داده شده است.");
            }
        }

        private static void ExtractSafely(string sourcePath, string destination)
        {
            using (var archive = ZipFile.OpenRead(sourcePath))
            {
                var root = Path.GetFullPath(destination + Path.DirectorySeparatorChar);
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    if (entry.Length > MaxBackupEntryBytes) throw new InvalidOperationException("یکی از فایل‌های بکاپ بیش از حد بزرگ است.");
                    checked { total += entry.Length; }
                    if (total > MaxBackupExtractedBytes) throw new InvalidOperationException("حجم بازشده بکاپ بیش از حد مجاز است.");

                    var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("ساختار فایل بکاپ ناامن است.");
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    var parent = Path.GetDirectoryName(target);
                    if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static string NewTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void SafeDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }

        private static void PruneAutomaticBackups(int keep)
        {
            try
            {
                var files = new DirectoryInfo(Database.BackupsDirectory).GetFiles("Snap-Auto-*.snapbackup");
                Array.Sort(files, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
                for (var i = keep; i < files.Length; i++) files[i].Delete();
            }
            catch { }
        }
    }
}
