using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Services;
using SnapAfghanistan.Native.Views;

namespace SnapAfghanistan.Native
{
    public partial class MainWindow : Window
    {
        private readonly ISnapService _service = SnapServices.Current;
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Button> _buttons;
        private readonly Dictionary<string, string[]> _titles = new Dictionary<string, string[]>
        {
            { "dashboard", new[] { "داشبورد مدیریتی", "تصویر روشن از اعضا، مراکز و سررسیدها" } },
            { "members", new[] { "مدیریت اعضا", "ثبت، ویرایش، تذکره و پرونده کامل اعضا" } },
            { "centers", new[] { "سکتورها و مراکز", "شبکه مراکز همکار، قراردادها و تخفیف‌ها" } },
            { "subscriptions", new[] { "اشتراک ماهانه مراکز", "پرداخت‌ها، اصلاح رسیدها، سررسیدها و بدهکاران" } },
            { "reports", new[] { "گزارش‌ها و خروجی‌ها", "مشاهده و دریافت PDF یا CSV با لوگوی دفتر" } },
            { "notes", new[] { "یادداشت‌های دفتر", "پیگیری کارها، تماس‌ها و موعدهای مهم" } },
            { "settings", new[] { "تنظیمات، کاربران و پشتیبان‌گیری", "امنیت، شبکه، کاربران، بکاپ و بازیابی" } }
        };

        public MainWindow()
        {
            InitializeComponent();
            _buttons = new Dictionary<string, Button>
            {
                { "dashboard", DashboardButton }, { "members", MembersButton }, { "centers", CentersButton },
                { "subscriptions", SubscriptionsButton }, { "reports", ReportsButton }, { "notes", NotesButton }, { "settings", SettingsButton }
            };
            SolarDate.Text = DateService.Solar(DateTime.Today);
            GregorianDate.Text = "میلادی: " + DateService.Gregorian(DateTime.Today);
            ApplyPermissions();
            var current = SessionContext.Current;
            SessionStatusText.Text = current == null ? "—" : current.User.DisplayName + " • " + current.User.RoleName;
            NetworkStatusText.Text = _service.IsRemote ? "Client • شبکه داخلی" : "Server • دیتابیس مرکزی";
            ShowPage("dashboard");
            Loaded += async (sender, args) => await RunAutomaticBackupAsync();
        }

        private void ApplyPermissions()
        {
            MembersButton.Visibility = Visible(PermissionCatalog.MembersView);
            CentersButton.Visibility = Visible(PermissionCatalog.CentersView);
            SubscriptionsButton.Visibility = Visible(PermissionCatalog.SubscriptionsView);
            ReportsButton.Visibility = Visible(PermissionCatalog.ReportsView);
            NotesButton.Visibility = Visible(PermissionCatalog.NotesView);
            SettingsButton.Visibility = (SessionContext.Has(PermissionCatalog.SettingsGeneral) || SessionContext.Has(PermissionCatalog.UsersManage) || SessionContext.Has(PermissionCatalog.BackupCreate) || SessionContext.Has(PermissionCatalog.BackupRestore)) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Visibility Visible(string permission) => SessionContext.Has(permission) ? Visibility.Visible : Visibility.Collapsed;

        private void Navigate(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (button.Visibility != Visibility.Visible) return;
            var key = Convert.ToString(button.Tag) ?? "dashboard";
            ShowPage(key);
        }

        private void ShowPage(string key)
        {
            if (!_buttons.ContainsKey(key) || _buttons[key].Visibility != Visibility.Visible) key = "dashboard";
            if (!_pages.ContainsKey(key)) _pages[key] = CreatePage(key);
            PageTitle.Text = _titles[key][0]; PageSubtitle.Text = _titles[key][1];
            foreach (var pair in _buttons) pair.Value.Style = (Style)Application.Current.Resources[pair.Key == key ? "ActiveNavButtonStyle" : "NavButtonStyle"];
            PageHost.Content = _pages[key];
            var refreshable = _pages[key] as IRefreshable; refreshable?.RefreshData();
        }

        private FrameworkElement CreatePage(string key)
        {
            switch (key)
            {
                case "members": return new MembersView(_service, ShowToast);
                case "centers": return new CentersView(_service, ShowToast);
                case "subscriptions": return new SubscriptionsView(_service, ShowToast);
                case "reports": return new ReportsView(_service, ShowToast);
                case "notes": return new NotesView(_service, ShowToast);
                case "settings": return new SettingsView(_service, ShowToast);
                default: return new DashboardView(_service, OpenSubscriptions);
            }
        }

        private void OpenSubscriptions(string status)
        {
            if (!SessionContext.Has(PermissionCatalog.SubscriptionsView)) return;
            ShowPage("subscriptions");
            var view = _pages["subscriptions"] as SubscriptionsView; view?.SetStatusFilter(status);
        }

        public void RefreshAllPages()
        {
            foreach (var page in _pages.Values) (page as IRefreshable)?.RefreshData();
        }

        public void ShowToast(string message)
        {
            ToastText.Text = message; Toast.Visibility = Visibility.Visible;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.7) };
            timer.Tick += (sender, args) => { timer.Stop(); Toast.Visibility = Visibility.Collapsed; }; timer.Start();
        }

        private async Task RunAutomaticBackupAsync()
        {
            if (_service.IsRemote) return;
            var settings = _service.GetSettings();
            if (!settings.AutoBackup || Database.GetSetting("last_auto_backup_date") == DateTime.Today.ToString("yyyy-MM-dd")) return;
            try
            {
                await Task.Run(() => new BackupService().CreateAutomaticBackup());
                Database.SetSetting("last_auto_backup_date", DateTime.Today.ToString("yyyy-MM-dd"));
            }
            catch (Exception exception) { Database.LogError(exception); }
        }
    }
}
