using System;
using System.Collections.Generic;
using System.Globalization;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Models
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
    }

    public sealed class DashboardStats
    {
        public long ActiveMembers { get; set; }
        public long RegisteredCenters { get; set; }
        public long ActiveSectors { get; set; }
        public long NearDue { get; set; }
        public long Overdue { get; set; }
        public long Suspended { get; set; }
        public long ArchivedMembers { get; set; }
        public long ArchivedCenters { get; set; }
        public long ArchivedSectors { get; set; }
        public long MonthRevenue { get; set; }
        public Dictionary<string, long> MemberTypes { get; set; } = new Dictionary<string, long>();
        public long ArchivedTotal => ArchivedMembers + ArchivedCenters + ArchivedSectors;
    }

    public sealed class RevenueTrendPoint
    {
        public string MonthKey { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal Amount { get; set; }
        public string AmountText => Amount.ToString("N0", CultureInfo.InvariantCulture) + " افغانی";
    }

    public sealed class MemberListItem
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string FatherName { get; set; } = "";
        public string Tazkira { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string Institution { get; set; } = "";
        public string Status { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string CreatedAtSolar => DateService.SolarFromIso(CreatedAt.Length >= 10 ? CreatedAt.Substring(0, 10) : CreatedAt);
    }

    public sealed class MemberRecord
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string Type { get; set; } = "معلم";
        public string FirstName { get; set; } = "";
        public string FatherName { get; set; } = "";
        public string TazkiraNo { get; set; } = "";
        public string Phone { get; set; } = "";
        public string OriginalAddress { get; set; } = "";
        public string CurrentAddress { get; set; } = "";
        public string Institution { get; set; } = "";
        public string Status { get; set; } = "فعال";
        public string Notes { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string AttachmentPath { get; set; } = "";
        public string AttachmentName { get; set; } = "";
        public int Version { get; set; }
    }

    public sealed class SectorItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "فعال";
        public long CenterCount { get; set; }
        public int Version { get; set; }
        public override string ToString() => Name;
    }

    public sealed class CenterListItem
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Representative { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Discount { get; set; } = "";
        public long MonthlySubscription { get; set; }
        public string SubscriptionText => MonthlySubscription <= 0 ? "تنظیم نشده" : MonthlySubscription.ToString("N0", CultureInfo.InvariantCulture) + " افغانی";
        public string StartDate { get; set; } = "";
        public string DueDate { get; set; } = "";
        public string StartDateSolar => DateService.SolarFromIso(StartDate);
        public string DueDateSolar => DateService.SolarFromIso(DueDate);
        public string SubscriptionStatus { get; set; } = "تنظیم نشده";
        public string Status { get; set; } = "فعال";
    }

    public sealed class CenterRecord
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string SectorId { get; set; } = "";
        public string LegalName { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string LicenseNo { get; set; } = "";
        public string Representative { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string ContractStart { get; set; } = "";
        public string ContractEnd { get; set; } = "";
        public string DiscountRate { get; set; } = "";
        public string FeeBasis { get; set; } = "بدون حق‌الخدمت";
        public decimal FeeAmount { get; set; }
        public decimal MonthlySubscription { get; set; }
        public string SubscriptionStart { get; set; } = "";
        public string NextDueDate { get; set; } = "";
        public bool SubscriptionSuspended { get; set; }
        public string Status { get; set; } = "فعال";
        public string Notes { get; set; } = "";
        public int Version { get; set; }
    }

    public sealed class PaymentItem
    {
        public string Id { get; set; } = "";
        public string CenterId { get; set; } = "";
        public string CenterName { get; set; } = "";
        public string PaymentDate { get; set; } = "";
        public string PaymentDateSolar => DateService.SolarFromIso(PaymentDate);
        public decimal Amount { get; set; }
        public string AmountText => Amount.ToString("N0", CultureInfo.InvariantCulture) + " افغانی";
        public string ReceiptNo { get; set; } = "";
        public int CoveredMonths { get; set; }
        public string PreviousDueDate { get; set; } = "";
        public string NewDueDate { get; set; } = "";
        public string NewDueDateSolar => DateService.SolarFromIso(NewDueDate);
        public string Notes { get; set; } = "";
        public int Version { get; set; }
    }

    public sealed class NoteItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Type { get; set; } = "عمومی";
        public string RelatedName { get; set; } = "";
        public string Priority { get; set; } = "عادی";
        public string DueDate { get; set; } = "";
        public string DueDateSolar => DateService.SolarFromIso(DueDate);
        public string Status { get; set; } = "باز";
        public string Body { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public int Version { get; set; }
    }

    public sealed class ArchivedItem
    {
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string ArchivedAt { get; set; } = "";
        public string ArchivedAtSolar => DateService.SolarFromIso(ArchivedAt.Length >= 10 ? ArchivedAt.Substring(0, 10) : ArchivedAt);
    }

    public sealed class TrashItem
    {
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string DeletedAt { get; set; } = "";
    }

    public sealed class AppSettingsRecord
    {
        public string CompanyName { get; set; } = "اسنپ افغانستان";
        public string Province { get; set; } = "هرات";
        public string MemberPrefix { get; set; } = "SNP-HRT";
        public int DueReminderDays { get; set; } = 7;
        public bool AutoBackup { get; set; } = true;
    }
}
