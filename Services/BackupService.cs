using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using SnapAfghanistan.Native.Data;

namespace SnapAfghanistan.Native.Services
{
    public sealed class BackupService
    {
        public string CreateBackup(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new InvalidOperationException("مسیر بکاپ انتخاب نشده است.");
            if (!destinationPath.EndsWith(".snapbackup", StringComparison.OrdinalIgnoreCase)) destinationPath += ".snapbackup";

            Database.Checkpoint();
            var temporary = NewTemporaryDirectory();
            try
            {
                File.Copy(Database.PathName, Path.Combine(temporary, "snap.db"), true);
                var attachments = Path.Combine(temporary, "attachments");
                if (Directory.Exists(Database.AttachmentsDirectory)) CopyDirectory(Database.AttachmentsDirectory, attachments);
                File.WriteAllText(Path.Combine(temporary, "manifest.txt"),
                    "Snap Afghanistan Backup" + Environment.NewLine +
                    "version=1" + Environment.NewLine +
                    "created_at=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

                var parent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                ZipFile.CreateFromDirectory(temporary, destinationPath, CompressionLevel.Optimal, false);
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
                ZipFile.ExtractToDirectory(sourcePath, temporary);
                var restoredDb = Path.Combine(temporary, "snap.db");
                if (!File.Exists(restoredDb)) throw new InvalidOperationException("این فایل، بکاپ معتبر اسنپ افغانستان نیست.");
                ValidateDatabase(restoredDb);

                var emergency = Path.Combine(Database.BackupsDirectory, "Before-Restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".snapbackup");
                CreateBackup(emergency);
                Database.Checkpoint();
                SQLiteConnection.ClearAllPools();
                File.Copy(restoredDb, Database.PathName, true);

                var restoredAttachments = Path.Combine(temporary, "attachments");
                if (Directory.Exists(restoredAttachments))
                {
                    if (Directory.Exists(Database.AttachmentsDirectory))
                    {
                        var old = Database.AttachmentsDirectory + ".before-restore";
                        SafeDeleteDirectory(old);
                        Directory.Move(Database.AttachmentsDirectory, old);
                    }
                    CopyDirectory(restoredAttachments, Database.AttachmentsDirectory);
                }
                Database.Initialize();
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
                    command.CommandText = "PRAGMA integrity_check; SELECT COUNT(*) FROM app_settings;";
                    command.ExecuteScalar();
                }
            }
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
