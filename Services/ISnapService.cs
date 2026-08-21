using System;
using System.Collections.Generic;
using System.Data;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public interface ISnapService
    {
        bool IsRemote { get; }
        bool IsConfigured();
        string SuggestedUsername { get; }
        UserSession Authenticate(string username, string password);
        void CreateFirstAdministrator(string username, string password);
        void ChangeOwnPassword(string currentPassword, string newPassword, string username);

        DashboardStats GetDashboard();
        IReadOnlyList<RevenueTrendPoint> GetRevenueTrend(int months = 6);

        PagedResult<MemberListItem> SearchMembers(string search, string memberType, string status, int page, int pageSize);
        MemberRecord? GetMember(string id);
        string SaveMember(MemberRecord member, string newAttachmentPath);
        void ArchiveMember(string id);
        void DeleteMember(string id);

        IReadOnlyList<SectorItem> GetSectors(bool includeInactive = true);
        string SaveSector(SectorItem sector);
        void ArchiveSector(string id);
        void DeleteSector(string id);

        PagedResult<CenterListItem> SearchCenters(string search, string sectorId, string subscriptionStatus, int page, int pageSize);
        CenterRecord? GetCenter(string id);
        string SaveCenter(CenterRecord center);
        void ArchiveCenter(string id);
        void DeleteCenter(string id);
        void ConfigureSubscription(string centerId, decimal amount, DateTime start, DateTime due, bool suspended);

        IReadOnlyList<PaymentItem> GetPayments(string centerId = "", int limit = 500);
        string RegisterPayment(string centerId, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes);
        void UpdatePayment(PaymentItem payment, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes);
        void DeletePayment(PaymentItem payment);

        IReadOnlyList<ArchivedItem> GetArchived(string entityType = "همه");
        long CountArchived(string entityType);
        void RestoreArchived(ArchivedItem item);
        void MoveArchivedToTrash(ArchivedItem item);

        IReadOnlyList<NoteItem> GetNotes(string status = "همه");
        string SaveNote(NoteItem note);
        void DeleteNote(string id);

        IReadOnlyList<TrashItem> GetTrash();
        void RestoreTrash(TrashItem item);
        void PermanentlyDeleteTrash(TrashItem item);

        AppSettingsRecord GetSettings();
        void SaveSettings(AppSettingsRecord settings);
        DataTable BuildReport(string reportKey, string memberType = "همه");

        IReadOnlyList<UserAccount> GetUsers();
        UserAccount CreateUser(string username, string displayName, string role, string temporaryPassword);
        void UpdateUser(string userId, string displayName, string role, bool isActive);
        void ResetPassword(string userId, string temporaryPassword);
        IReadOnlyDictionary<string, bool> GetEffectivePermissions(string userId);
        void SetPermissionOverride(string userId, string permission, bool allowed);
        void ClearPermissionOverrides(string userId);
    }

    public static class SnapServices
    {
        public static ISnapService Current { get; private set; } = null!;
        public static void Configure(ISnapService service) => Current = service ?? throw new ArgumentNullException(nameof(service));
    }
}
