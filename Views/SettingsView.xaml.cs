using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class SettingsView : UserControl, IRefreshable
    {
        private readonly ISnapService _service; private readonly Action<string> _toast;
        public SettingsView(ISnapService service, Action<string> toast)
        {
            InitializeComponent(); _service = service; _toast = toast;
            GeneralTab.Visibility = SessionContext.Has(PermissionCatalog.SettingsGeneral) ? Visibility.Visible : Visibility.Collapsed;
            TrashTab.Visibility = SessionContext.Has(PermissionCatalog.SettingsGeneral) ? Visibility.Visible : Visibility.Collapsed;
            UsersTab.Visibility = (SessionContext.Has(PermissionCatalog.UsersManage) || SessionContext.Has(PermissionCatalog.SettingsGeneral)) ? Visibility.Visible : Visibility.Collapsed;
            ManageUsersButton.Visibility = SessionContext.Has(PermissionCatalog.UsersManage) ? Visibility.Visible : Visibility.Collapsed;
            NetworkSetupButton.Visibility = SessionContext.Has(PermissionCatalog.SettingsGeneral) ? Visibility.Visible : Visibility.Collapsed;
            BackupButton.Visibility = (!_service.IsRemote && SessionContext.Has(PermissionCatalog.BackupCreate)) ? Visibility.Visible : Visibility.Collapsed;
            RestoreButton.Visibility = (!_service.IsRemote && SessionContext.Has(PermissionCatalog.BackupRestore)) ? Visibility.Visible : Visibility.Collapsed;
            OpenDataButton.Visibility = (!_service.IsRemote && SessionContext.Has(PermissionCatalog.SettingsGeneral)) ? Visibility.Visible : Visibility.Collapsed;
            PurgeButton.Visibility = SessionContext.Has(PermissionCatalog.TrashPurge) ? Visibility.Visible : Visibility.Collapsed;
            if (_service.IsRemote) BackupHelpText.Text = "این کمپیوتر Client است. بکاپ و بازیابی فقط روی کمپیوتر اصلی انجام می‌شود تا دیتابیس در یک نقطه کنترل شود.";
        }

        public void RefreshData()
        {
            try
            {
                var settings = _service.GetSettings(); CompanyNameText.Text = settings.CompanyName; ProvinceText.Text = settings.Province; PrefixText.Text = settings.MemberPrefix; ReminderText.Text = settings.DueReminderDays.ToString(CultureInfo.InvariantCulture); AutoBackupCheck.IsChecked = settings.AutoBackup;
                UsernameText.Text = SessionContext.Current?.User.Username ?? ""; CurrentUserText.Text = "کاربر فعلی: " + (SessionContext.Current?.ActorLabel ?? "—");
                if (SessionContext.Has(PermissionCatalog.SettingsGeneral)) TrashGrid.ItemsSource = _service.GetTrash();
                if (_service.IsRemote) { DataPathText.Text = "Server / Data"; DbSizeText.Text = "اطلاعات روی کمپیوتر اصلی نگهداری می‌شود."; }
                else { DataPathText.Text = Database.DataDirectory; try { DbSizeText.Text = "حجم دیتابیس: " + FormatBytes(new FileInfo(Database.PathName).Length); } catch { DbSizeText.Text = ""; } }
                RefreshNetworkInfo();
            }
            catch (Exception ex) { MessageBox.Show(UiMessages.Friendly(ex), "تنظیمات", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void RefreshNetworkInfo()
        {
            var config = NetworkConfigurationService.Load();
            if (config.IsServer)
            {
                NetworkModeText.Text = "نقش این کمپیوتر: Server / اصلی";
                NetworkAddressText.Text = "IP: " + NetworkConfigurationService.SuggestedServerAddress() + ":" + config.Port.ToString(CultureInfo.InvariantCulture);
                PairingText.Text = SessionContext.Has(PermissionCatalog.UsersManage) ? "کُد اتصال: " + config.Secret : "کُد اتصال فقط برای مدیر قابل نمایش است.";
            }
            else
            {
                NetworkModeText.Text = "نقش این کمپیوتر: Client / کاربر";
                NetworkAddressText.Text = "Server: " + config.Host + ":" + config.Port.ToString(CultureInfo.InvariantCulture);
                PairingText.Text = "ارتباط رمزگذاری‌شده روی شبکه داخلی";
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try { int reminder; if (!int.TryParse(ReminderText.Text.Trim(), out reminder)) throw new InvalidOperationException("تعداد روز یادآوری معتبر نیست."); _service.SaveSettings(new AppSettingsRecord { CompanyName=CompanyNameText.Text,Province=ProvinceText.Text,MemberPrefix=PrefixText.Text,DueReminderDays=reminder,AutoBackup=AutoBackupCheck.IsChecked==true }); _toast("تنظیمات ذخیره شد."); (Window.GetWindow(this) as MainWindow)?.RefreshAllPages(); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try { if (NewPassword.Password != ConfirmPassword.Password) throw new InvalidOperationException("تکرار رمز جدید یکسان نیست."); _service.ChangeOwnPassword(CurrentPassword.Password, NewPassword.Password, UsernameText.Text); CurrentPassword.Clear(); NewPassword.Clear(); ConfirmPassword.Clear(); _toast("نام کاربری و رمز با موفقیت تغییر کرد."); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "تغییر رمز انجام نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            if (_service.IsRemote) { _toast("بکاپ فقط روی Server ساخته می‌شود."); return; }
            var dialog = new SaveFileDialog { Title="ذخیره بکاپ اسنپ",Filter="Snap Backup|*.snapbackup",FileName="Snap-Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".snapbackup" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { var path = new BackupService().CreateBackup(dialog.FileName); _toast("بکاپ بدون خطا ساخته شد."); Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)) { UseShellExecute=true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بکاپ ساخته نشد", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (_service.IsRemote) { _toast("بازیابی بکاپ فقط روی Server انجام می‌شود."); return; }
            var dialog = new OpenFileDialog { Title="انتخاب بکاپ اسنپ",Filter="Snap Backup|*.snapbackup|همه فایل‌ها|*.*" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            if (MessageBox.Show("اطلاعات فعلی با بکاپ انتخاب‌شده جایگزین شود؟\nپیش از بازیابی، سیستم یک بکاپ اضطراری می‌سازد.", "تأیید بازیابی", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try { var emergency = new BackupService().RestoreBackup(dialog.FileName); MessageBox.Show("بازیابی کامل شد. برنامه اکنون بسته می‌شود؛ دوباره آن را باز کنید.\n\nبکاپ اضطراری:\n"+emergency,"بازیابی موفق",MessageBoxButton.OK,MessageBoxImage.Information); Application.Current.Shutdown(); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بازیابی انجام نشد", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OpenData_Click(object sender, RoutedEventArgs e) { if (_service.IsRemote) return; Directory.CreateDirectory(Database.DataDirectory); Process.Start(new ProcessStartInfo(Database.DataDirectory) { UseShellExecute=true }); }
        private void ManageUsers_Click(object sender, RoutedEventArgs e) { new UserManagementDialog(_service) { Owner = Window.GetWindow(this) }.ShowDialog(); RefreshData(); }
        private void NetworkSetup_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("تغییر نقش شبکه نیاز به راه‌اندازی مجدد برنامه دارد. ادامه می‌دهید؟", "تنظیم شبکه", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var dialog = new NetworkSetupWindow(NetworkConfigurationService.Load()) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() != true) return;
            NetworkConfigurationService.Save(dialog.Result); MessageBox.Show("تنظیم شبکه ذخیره شد. برنامه بسته می‌شود؛ دوباره آن را باز کنید.", "تنظیم شبکه", MessageBoxButton.OK, MessageBoxImage.Information); Application.Current.Shutdown();
        }
        private TrashItem? SelectedTrash() => TrashGrid.SelectedItem as TrashItem;
        private void RestoreTrash_Click(object sender, RoutedEventArgs e) { var item=SelectedTrash(); if(item==null){_toast("ابتدا یک مورد را انتخاب کنید.");return;} try{_service.RestoreTrash(item);RefreshData();_toast("رکورد بازیابی شد.");}catch(Exception ex){MessageBox.Show(UiMessages.Friendly(ex));} }
        private void Purge_Click(object sender, RoutedEventArgs e) { var item=SelectedTrash(); if(item==null){_toast("ابتدا یک مورد را انتخاب کنید.");return;} if(MessageBox.Show("«"+item.Title+"» برای همیشه حذف شود؟\nاین کار قابل برگشت نیست.","حذف دایمی",MessageBoxButton.YesNo,MessageBoxImage.Stop)!=MessageBoxResult.Yes)return; try{_service.PermanentlyDeleteTrash(item);RefreshData();_toast("رکورد برای همیشه حذف شد.");}catch(Exception ex){MessageBox.Show(UiMessages.Friendly(ex));} }
        private static string FormatBytes(long bytes) { if (bytes < 1024) return bytes + " B"; if (bytes < 1024*1024) return (bytes/1024d).ToString("0.0")+" KB"; return (bytes/(1024d*1024d)).ToString("0.0")+" MB"; }
    }
}
