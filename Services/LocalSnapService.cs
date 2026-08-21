using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class LocalSnapService : ISnapService
    {
        private static readonly object MutationLock = new object();
        private readonly SnapRepository _repository = new SnapRepository();
        private readonly OperationsService _operations = new OperationsService();
        private readonly DashboardAnalyticsService _analytics = new DashboardAnalyticsService();
        private readonly AuthService _auth = new AuthService();

        public LocalSnapService()
        {
            OperationalSchema.Ensure();
        }

        public bool IsRemote => false;
        public bool IsConfigured() => _auth.IsConfigured();
        public string SuggestedUsername => _auth.Username;
        public UserSession Authenticate(string username, string password) => _auth.Authenticate(username, password);
        public void CreateFirstAdministrator(string username, string password) => _auth.SetPassword(password, username);
        public void ChangeOwnPassword(string currentPassword, string newPassword, string username) => Mutate(() => _auth.ChangePassword(currentPassword, newPassword, username));

        public DashboardStats GetDashboard()
        {
            RequireAuthenticated();
            return _repository.GetDashboard();
        }

        public IReadOnlyList<RevenueTrendPoint> GetRevenueTrend(int months = 6)
        {
            RequireAuthenticated();
            return _analytics.GetRevenueTrend(months);
        }

        public PagedResult<MemberListItem> SearchMembers(string search, string memberType, string status, int page, int pageSize)
        {
            AuthService.Require(PermissionCatalog.MembersView);
            return _repository.SearchMembers(search, memberType, status, page, pageSize);
        }

        public MemberRecord? GetMember(string id)
        {
            AuthService.Require(PermissionCatalog.MembersView);
            var record = _repository.GetMember(id);
            if (record != null) record.Version = ReadVersion("members", id);
            return record;
        }

        public string SaveMember(MemberRecord member, string newAttachmentPath)
        {
            AuthService.Require(PermissionCatalog.MembersWrite);
            return Mutate(() =>
            {
                EnsureVersion("members", member.Id, member.Version);
                var id = _repository.SaveMember(member, newAttachmentPath);
                member.Version = ReadVersion("members", id);
                return id;
            });
        }

        public void ArchiveMember(string id) { AuthService.Require(PermissionCatalog.MembersDelete); Mutate(() => _repository.ArchiveMember(id)); }
        public void DeleteMember(string id) { AuthService.Require(PermissionCatalog.MembersDelete); Mutate(() => _repository.DeleteMember(id)); }

        public IReadOnlyList<SectorItem> GetSectors(bool includeInactive = true)
        {
            RequireAny(PermissionCatalog.CentersView, PermissionCatalog.SubscriptionsView);
            var items = _repository.GetSectors(includeInactive);
            foreach (var item in items) item.Version = ReadVersion("sectors", item.Id);
            return items;
        }

        public string SaveSector(SectorItem sector)
        {
            AuthService.Require(PermissionCatalog.CentersWrite);
            return Mutate(() =>
            {
                EnsureVersion("sectors", sector.Id, sector.Version);
                var id = _repository.SaveSector(sector);
                sector.Version = ReadVersion("sectors", id);
                return id;
            });
        }

        public void ArchiveSector(string id) { AuthService.Require(PermissionCatalog.CentersDelete); Mutate(() => _operations.ArchiveSector(id)); }
        public void DeleteSector(string id) { AuthService.Require(PermissionCatalog.CentersDelete); Mutate(() => _repository.DeleteSector(id)); }

        public PagedResult<CenterListItem> SearchCenters(string search, string sectorId, string subscriptionStatus, int page, int pageSize)
        {
            RequireAny(PermissionCatalog.CentersView, PermissionCatalog.SubscriptionsView);
            return _repository.SearchCenters(search, sectorId, subscriptionStatus, page, pageSize);
        }

        public CenterRecord? GetCenter(string id)
        {
            RequireAny(PermissionCatalog.CentersView, PermissionCatalog.SubscriptionsView);
            var record = _repository.GetCenter(id);
            if (record != null) record.Version = ReadVersion("partners", id);
            return record;
        }

        public string SaveCenter(CenterRecord center)
        {
            AuthService.Require(PermissionCatalog.CentersWrite);
            return Mutate(() =>
            {
                EnsureVersion("partners", center.Id, center.Version);
                var id = _repository.SaveCenter(center);
                center.Version = ReadVersion("partners", id);
                return id;
            });
        }

        public void ArchiveCenter(string id) { AuthService.Require(PermissionCatalog.CentersDelete); Mutate(() => _repository.ArchiveCenter(id)); }
        public void DeleteCenter(string id) { AuthService.Require(PermissionCatalog.CentersDelete); Mutate(() => _repository.DeleteCenter(id)); }

        public void ConfigureSubscription(string centerId, decimal amount, DateTime start, DateTime due, bool suspended)
        {
            AuthService.Require(PermissionCatalog.SubscriptionsWrite);
            Mutate(() => _repository.ConfigureSubscription(centerId, amount, start, due, suspended));
        }

        public IReadOnlyList<PaymentItem> GetPayments(string centerId = "", int limit = 500)
        {
            AuthService.Require(PermissionCatalog.SubscriptionsView);
            var items = _repository.GetPayments(centerId, limit);
            var versions = ReadVersions("subscription_payments", "id", centerId, "partner_id");
            foreach (var item in items)
            {
                int version;
                if (versions.TryGetValue(item.Id, out version)) item.Version = version;
            }
            return items;
        }

        public string RegisterPayment(string centerId, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes)
        {
            AuthService.Require(PermissionCatalog.SubscriptionsWrite);
            return Mutate(() => _operations.RegisterPayment(centerId, amount, paymentDate, coveredMonths, receiptNo, notes));
        }

        public void UpdatePayment(PaymentItem payment, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes)
        {
            AuthService.Require(PermissionCatalog.SubscriptionsWrite);
            Mutate(() =>
            {
                EnsureVersion("subscription_payments", payment.Id, payment.Version);
                _operations.UpdatePayment(payment, amount, paymentDate, coveredMonths, receiptNo, notes);
                IncrementVersion("subscription_payments", payment.Id);
                payment.Version = ReadVersion("subscription_payments", payment.Id);
            });
        }

        public void DeletePayment(PaymentItem payment)
        {
            AuthService.Require(PermissionCatalog.SubscriptionsDelete);
            Mutate(() =>
            {
                EnsureVersion("subscription_payments", payment.Id, payment.Version);
                _operations.DeletePayment(payment);
            });
        }

        public IReadOnlyList<ArchivedItem> GetArchived(string entityType = "همه")
        {
            RequireAuthenticated();
            return _operations.GetArchived(entityType);
        }

        public long CountArchived(string entityType)
        {
            RequireAuthenticated();
            return _operations.CountArchived(entityType);
        }

        public void RestoreArchived(ArchivedItem item) { RequireDeleteForEntity(item.EntityType); Mutate(() => _operations.RestoreArchived(item)); }
        public void MoveArchivedToTrash(ArchivedItem item) { RequireDeleteForEntity(item.EntityType); Mutate(() => _operations.MoveArchivedToTrash(item)); }

        public IReadOnlyList<NoteItem> GetNotes(string status = "همه")
        {
            AuthService.Require(PermissionCatalog.NotesView);
            var items = _repository.GetNotes(status);
            var versions = ReadVersions("notes", "id", "", "");
            foreach (var item in items)
            {
                int version;
                if (versions.TryGetValue(item.Id, out version)) item.Version = version;
            }
            return items;
        }

        public string SaveNote(NoteItem note)
        {
            AuthService.Require(PermissionCatalog.NotesWrite);
            return Mutate(() =>
            {
                var isNew = string.IsNullOrWhiteSpace(note.Id);
                EnsureVersion("notes", note.Id, note.Version);
                var id = _repository.SaveNote(note);
                if (!isNew) IncrementVersion("notes", id);
                note.Version = ReadVersion("notes", id);
                return id;
            });
        }

        public void DeleteNote(string id) { AuthService.Require(PermissionCatalog.NotesDelete); Mutate(() => _repository.DeleteNote(id)); }

        public IReadOnlyList<TrashItem> GetTrash()
        {
            AuthService.Require(PermissionCatalog.SettingsGeneral);
            return _repository.GetTrash();
        }

        public void RestoreTrash(TrashItem item) { AuthService.Require(PermissionCatalog.SettingsGeneral); Mutate(() => _repository.RestoreTrash(item)); }
        public void PermanentlyDeleteTrash(TrashItem item) { AuthService.Require(PermissionCatalog.TrashPurge); Mutate(() => _repository.PermanentlyDeleteTrash(item)); }

        public AppSettingsRecord GetSettings()
        {
            RequireAuthenticated();
            return _repository.GetSettings();
        }

        public void SaveSettings(AppSettingsRecord settings) { AuthService.Require(PermissionCatalog.SettingsGeneral); Mutate(() => _repository.SaveSettings(settings)); }

        public DataTable BuildReport(string reportKey, string memberType = "همه")
        {
            AuthService.Require(PermissionCatalog.ReportsView);
            return _repository.BuildReport(reportKey, memberType);
        }

        public IReadOnlyList<UserAccount> GetUsers() { AuthService.Require(PermissionCatalog.UsersManage); return _auth.GetUsers(); }
        public UserAccount CreateUser(string username, string displayName, string role, string temporaryPassword) => Mutate(() => _auth.CreateUser(username, displayName, role, temporaryPassword));
        public void UpdateUser(string userId, string displayName, string role, bool isActive) => Mutate(() => _auth.UpdateUser(userId, displayName, role, isActive));
        public void ResetPassword(string userId, string temporaryPassword) => Mutate(() => _auth.ResetPassword(userId, temporaryPassword));

        public IReadOnlyDictionary<string, bool> GetEffectivePermissions(string userId)
        {
            AuthService.Require(PermissionCatalog.UsersManage);
            using (var connection = Database.Open())
            {
                string role;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT role FROM users WHERE id=@id LIMIT 1";
                    command.Parameters.AddWithValue("@id", userId);
                    role = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "employee";
                }
                var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in PermissionCatalog.All) result[key] = Contains(PermissionCatalog.Defaults(role), key);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT permission_key,allowed FROM user_permissions WHERE user_id=@id";
                    command.Parameters.AddWithValue("@id", userId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) result[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? ""] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0;
                    }
                }
                return result;
            }
        }

        public void SetPermissionOverride(string userId, string permission, bool allowed) => Mutate(() => _auth.SetPermissionOverride(userId, permission, allowed));
        public void ClearPermissionOverrides(string userId) => Mutate(() => _auth.ClearPermissionOverrides(userId));

        private static T Mutate<T>(Func<T> action)
        {
            lock (MutationLock)
            {
                SetAuditContext();
                return action();
            }
        }

        private static void Mutate(Action action)
        {
            lock (MutationLock)
            {
                SetAuditContext();
                action();
            }
        }

        private static void SetAuditContext()
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE runtime_context SET actor=@actor,machine_name=@machine WHERE id=1";
                command.Parameters.AddWithValue("@actor", SessionContext.ActorLabel);
                command.Parameters.AddWithValue("@machine", SessionContext.MachineName);
                command.ExecuteNonQuery();
            }
        }

        private static int ReadVersion(string table, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COALESCE(version,1) FROM " + table + " WHERE id=@id LIMIT 1";
                command.Parameters.AddWithValue("@id", id);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        private static Dictionary<string, int> ReadVersions(string table, string idColumn, string filterValue, string filterColumn)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + idColumn + ",COALESCE(version,1) FROM " + table + (string.IsNullOrWhiteSpace(filterColumn) ? "" : " WHERE " + filterColumn + "=@filter");
                if (!string.IsNullOrWhiteSpace(filterColumn)) command.Parameters.AddWithValue("@filter", filterValue);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? ""] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static void EnsureVersion(string table, string id, int expected)
        {
            if (string.IsNullOrWhiteSpace(id) || expected <= 0) return;
            var current = ReadVersion(table, id);
            if (current == 0) throw new InvalidOperationException("رکورد موردنظر دیگر وجود ندارد.");
            if (current != expected) throw new InvalidOperationException("این پرونده توسط کاربر دیگری تغییر کرده است. اطلاعات را تازه‌سازی کنید و دوباره تلاش کنید.");
        }

        private static void IncrementVersion(string table, string id)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE " + table + " SET version=COALESCE(version,1)+1 WHERE id=@id";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private static bool Contains(IEnumerable<string> values, string target)
        {
            foreach (var value in values) if (string.Equals(value, target, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void RequireAuthenticated()
        {
            if (!SessionContext.IsAuthenticated) throw new UnauthorizedAccessException("جلسه کاربری معتبر نیست.");
        }

        private static void RequireAny(params string[] permissions)
        {
            RequireAuthenticated();
            foreach (var permission in permissions) if (SessionContext.Has(permission)) return;
            throw new UnauthorizedAccessException("شما اجازه مشاهده این بخش را ندارید.");
        }

        private static void RequireDeleteForEntity(string entityType)
        {
            if (entityType == "عضو") AuthService.Require(PermissionCatalog.MembersDelete);
            else if (entityType == "مرکز" || entityType == "سکتور") AuthService.Require(PermissionCatalog.CentersDelete);
            else throw new UnauthorizedAccessException("مجوز عملیات مشخص نیست.");
        }
    }

    internal static class OperationalSchema
    {
        private static readonly object Sync = new object();
        private static bool _ready;

        public static void Ensure()
        {
            if (_ready) return;
            lock (Sync)
            {
                if (_ready) return;
                EnsureColumn("subscription_payments", "version", "INTEGER DEFAULT 1");
                EnsureColumn("notes", "version", "INTEGER DEFAULT 1");
                using (var connection = Database.Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS runtime_context(
    id INTEGER PRIMARY KEY CHECK(id=1),
    actor TEXT NOT NULL DEFAULT 'system',
    machine_name TEXT NOT NULL DEFAULT ''
);
INSERT OR IGNORE INTO runtime_context(id,actor,machine_name) VALUES(1,'system','');
CREATE TRIGGER IF NOT EXISTS audit_runtime_context
AFTER INSERT ON audit_log
WHEN COALESCE(NEW.actor,'')=''
BEGIN
    UPDATE audit_log
       SET actor=(SELECT actor FROM runtime_context WHERE id=1),
           machine_name=(SELECT machine_name FROM runtime_context WHERE id=1)
     WHERE id=NEW.id;
END;";
                    command.ExecuteNonQuery();
                }
                _ready = true;
            }
        }

        private static void EnsureColumn(string table, string column, string definition)
        {
            var exists = false;
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + table + ")";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture), column, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                    }
                }
                if (!exists)
                {
                    command.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition;
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
