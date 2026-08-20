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
            ActiveMembers.Text = stats.ActiveMembers.ToString("N0", CultureInfo.InvariantCulture);
            Centers.Text = stats.RegisteredCenters.ToString("N0", CultureInfo.InvariantCulture);
            NearDue.Text = stats.NearDue.ToString("N0", CultureInfo.InvariantCulture);
            Overdue.Text = stats.Overdue.ToString("N0", CultureInfo.InvariantCulture);
            NearDueSmall.Text = stats.NearDue.ToString("N0", CultureInfo.InvariantCulture);
            Suspended.Text = stats.Suspended.ToString("N0", CultureInfo.InvariantCulture);
            MonthRevenue.Text = stats.MonthRevenue.ToString("N0", CultureInfo.InvariantCulture) + " افغانی";
            Teachers.Text = TypeCount(stats, "معلم"); Scholars.Text = TypeCount(stats, "عالم"); Professors.Text = TypeCount(stats, "استاد پوهنتون");
            Cultural.Text = TypeCount(stats, "فرهنگی"); Students.Text = TypeCount(stats, "شاگرد");
            Summary.Text = stats.ActiveMembers.ToString("N0") + " عضو فعال، " + stats.RegisteredCenters.ToString("N0") + " مرکز و " + stats.ActiveSectors.ToString("N0") + " سکتور در سیستم ثبت است.";
        }

        private static string TypeCount(Models.DashboardStats stats, string type) => stats.MemberTypes.ContainsKey(type) ? stats.MemberTypes[type].ToString("N0") : "0";
    }
}
