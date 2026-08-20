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
        private readonly SnapRepository _repository = new SnapRepository();
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Button> _buttons;
        private readonly Dictionary<string, string[]> _titles = new Dictionary<string, string[]>
        {
            { "dashboard", new[] { "داشبورد مدیریتی", "تصویر روشن از اعضا، مراکز و سررسیدها" } },
            { "members", new[] { "مدیریت اعضا", "ثبت، ویرایش، تذکره و پرونده کامل اعضا" } },
            { "centers", new[] { "سکتورها و مراکز", "شبکه مراکز همکار، قراردادها و تخفیف‌ها" } },
            { "subscriptions", new[] { "اشتراک ماهانه مراکز", "پرداخت‌ها، رسیدها، سررسیدها و بدهکاران" } },
            { "reports", new[] { "گزارش‌ها و خروجی‌ها", "مشاهده و دریافت PDF یا CSV با لوگوی دفتر" } },
            { "notes", new[] { "یادداشت‌های دفتر", "پیگیری کارها، تماس‌ها و موعدهای مهم" } },
            { "settings", new[] { "تنظیمات و پشتیبان‌گیری", "امنیت، بکاپ، بازیابی و سطل زباله" } }
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
            GregorianDate.Text = DateService.Gregorian(DateTime.Today);
            ShowPage("dashboard");
            Loaded += async (sender, args) => await RunAutomaticBackupAsync();
        }

        private void Navigate(object sender, RoutedEventArgs e)
        {
            var key = Convert.ToString(((Button)sender).Tag) ?? "dashboard";
            ShowPage(key);
        }

        private void ShowPage(string key)
        {
            if (!_pages.ContainsKey(key)) _pages[key] = CreatePage(key);
            PageTitle.Text = _titles[key][0];
            PageSubtitle.Text = _titles[key][1];
            foreach (var pair in _buttons)
                pair.Value.Style = (Style)Application.Current.Resources[pair.Key == key ? "ActiveNavButtonStyle" : "NavButtonStyle"];
            PageHost.Content = _pages[key];
            var refreshable = _pages[key] as IRefreshable;
            refreshable?.RefreshData();
        }

        private FrameworkElement CreatePage(string key)
        {
            switch (key)
            {
                case "members": return new MembersView(_repository, ShowToast);
                case "centers": return new CentersView(_repository, ShowToast);
                case "subscriptions": return new SubscriptionsView(_repository, ShowToast);
                case "reports": return new ReportsView(_repository, ShowToast);
                case "notes": return new NotesView(_repository, ShowToast);
                case "settings": return new SettingsView(_repository, ShowToast);
                default: return new DashboardView(_repository);
            }
        }

        public void RefreshAllPages()
        {
            foreach (var page in _pages.Values)
            {
                var refreshable = page as IRefreshable;
                refreshable?.RefreshData();
            }
        }

        public void ShowToast(string message)
        {
            ToastText.Text = message;
            Toast.Visibility = Visibility.Visible;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.7) };
            timer.Tick += (sender, args) => { timer.Stop(); Toast.Visibility = Visibility.Collapsed; };
            timer.Start();
        }

        private async Task RunAutomaticBackupAsync()
        {
            var settings = _repository.GetSettings();
            if (!settings.AutoBackup || Database.GetSetting("last_auto_backup_date") == DateTime.Today.ToString("yyyy-MM-dd")) return;
            try
            {
                await Task.Run(() => new BackupService().CreateAutomaticBackup());
                Database.SetSetting("last_auto_backup_date", DateTime.Today.ToString("yyyy-MM-dd"));
            }
            catch (Exception exception)
            {
                Database.LogError(exception);
            }
        }
    }
}
