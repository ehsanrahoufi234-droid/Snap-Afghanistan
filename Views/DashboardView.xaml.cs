using System.Globalization;
using System.Windows.Controls;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class DashboardView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository;
        public DashboardView(SnapRepository repository) { InitializeComponent(); _repository = repository; }

        public void RefreshData()
        {
            var stats = _repository.GetDashboard();
            ActiveMembers.Text = Format(stats.ActiveMembers);
            Centers.Text = Format(stats.RegisteredCenters);
            NearDue.Text = Format(stats.NearDue);
            Overdue.Text = Format(stats.Overdue);
            NearDueSmall.Text = Format(stats.NearDue);
            Suspended.Text = Format(stats.Suspended);
            MonthRevenue.Text = Format(stats.MonthRevenue) + " افغانی";
            Teachers.Text = TypeCount(stats, "معلم"); Scholars.Text = TypeCount(stats, "عالم"); Professors.Text = TypeCount(stats, "استاد پوهنتون");
            Cultural.Text = TypeCount(stats, "فرهنگی"); Students.Text = TypeCount(stats, "شاگرد");
            Summary.Text = Format(stats.ActiveMembers) + " عضو فعال، " + Format(stats.RegisteredCenters) + " مرکز و " + Format(stats.ActiveSectors) + " سکتور در سیستم ثبت است.";
        }

        private static string TypeCount(Models.DashboardStats stats, string type) => stats.MemberTypes.ContainsKey(type) ? Format(stats.MemberTypes[type]) : "0";
        private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
