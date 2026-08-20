using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class SettingsView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository; private readonly Action<string> _toast; private readonly AuthService _auth = new AuthService();
        public SettingsView(SnapRepository repository, Action<string> toast) { InitializeComponent(); _repository = repository; _toast = toast; DataPathText.Text = Database.DataDirectory; }
        public void RefreshData()
        {
            var settings = _repository.GetSettings(); CompanyNameText.Text = settings.CompanyName; ProvinceText.Text = settings.Province; PrefixText.Text = settings.MemberPrefix; ReminderText.Text = settings.DueReminderDays.ToString(CultureInfo.InvariantCulture); AutoBackupCheck.IsChecked = settings.AutoBackup; UsernameText.Text = _auth.Username;
            TrashGrid.ItemsSource = _repository.GetTrash(); try { DbSizeText.Text = "حجم دیتابیس: " + FormatBytes(new FileInfo(Database.PathName).Length); } catch { DbSizeText.Text = ""; }
        }
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try { int reminder; if (!int.TryParse(ReminderText.Text.Trim(), out reminder)) throw new InvalidOperationException("تعداد روز یادآوری معتبر نیست."); _repository.SaveSettings(new AppSettingsRecord { CompanyName=CompanyNameText.Text,Province=ProvinceText.Text,MemberPrefix=PrefixText.Text,DueReminderDays=reminder,AutoBackup=AutoBackupCheck.IsChecked==true }); _toast("تنظیمات ذخیره شد."); var main = Window.GetWindow(this) as MainWindow; main?.RefreshAllPages(); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try { if (NewPassword.Password != ConfirmPassword.Password) throw new InvalidOperationException("تکرار رمز جدید یکسان نیست."); _auth.ChangePassword(CurrentPassword.Password, NewPassword.Password, UsernameText.Text); CurrentPassword.Clear(); NewPassword.Clear(); ConfirmPassword.Clear(); _toast("نام کاربری و رمز با موفقیت تغییر کرد."); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "تغییر رمز انجام نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title="ذخیره بکاپ اسنپ",Filter="Snap Backup|*.snapbackup",FileName="Snap-Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".snapbackup" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { var path = new BackupService().CreateBackup(dialog.FileName); _toast("بکاپ بدون خطا ساخته شد."); Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)) { UseShellExecute=true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بکاپ ساخته نشد", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title="انتخاب بکاپ اسنپ",Filter="Snap Backup|*.snapbackup|همه فایل‌ها|*.*" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            if (MessageBox.Show("اطلاعات فعلی با بکاپ انتخاب‌شده جایگزین شود؟\nپیش از بازیابی، سیستم یک بکاپ اضطراری می‌سازد.", "تأیید بازیابی", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try { var emergency = new BackupService().RestoreBackup(dialog.FileName); MessageBox.Show("بازیابی کامل شد. برنامه اکنون بسته می‌شود؛ دوباره آن را باز کنید.\n\nبکاپ اضطراری:\n"+emergency,"بازیابی موفق",MessageBoxButton.OK,MessageBoxImage.Information); Application.Current.Shutdown(); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بازیابی انجام نشد", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void OpenData_Click(object sender, RoutedEventArgs e) { Directory.CreateDirectory(Database.DataDirectory); Process.Start(new ProcessStartInfo(Database.DataDirectory) { UseShellExecute=true }); }
        private TrashItem? SelectedTrash() => TrashGrid.SelectedItem as TrashItem;
        private void RestoreTrash_Click(object sender, RoutedEventArgs e) { var item=SelectedTrash(); if(item==null){_toast("ابتدا یک مورد را انتخاب کنید.");return;} try{_repository.RestoreTrash(item);RefreshData();_toast("رکورد بازیابی شد.");}catch(Exception ex){MessageBox.Show(UiMessages.Friendly(ex));} }
        private void Purge_Click(object sender, RoutedEventArgs e) { var item=SelectedTrash(); if(item==null){_toast("ابتدا یک مورد را انتخاب کنید.");return;} if(MessageBox.Show("«"+item.Title+"» برای همیشه حذف شود؟\nاین کار قابل برگشت نیست.","حذف دایمی",MessageBoxButton.YesNo,MessageBoxImage.Stop)!=MessageBoxResult.Yes)return; try{_repository.PermanentlyDeleteTrash(item);RefreshData();_toast("رکورد برای همیشه حذف شد.");}catch(Exception ex){MessageBox.Show(UiMessages.Friendly(ex));} }
        private static string FormatBytes(long bytes) { if (bytes < 1024) return bytes + " B"; if (bytes < 1024*1024) return (bytes/1024d).ToString("0.0")+" KB"; return (bytes/(1024d*1024d)).ToString("0.0")+" MB"; }
    }
}
