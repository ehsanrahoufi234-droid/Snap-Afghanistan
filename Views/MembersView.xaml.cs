using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class MembersView : UserControl, IRefreshable
    {
        private readonly ISnapService _service;
        private readonly Action<string> _toast;
        private int _page = 1;
        private const int PageSize = 200;
        private PagedResult<MemberListItem>? _current;
        private MemberRecord? _previewRecord;

        public MembersView(ISnapService service, Action<string> toast)
        {
            InitializeComponent(); _service = service; _toast = toast;
            TypeFilter.ItemsSource = new[] { "همه" }.Concat(SnapRepository.MemberTypes).ToArray(); TypeFilter.SelectedIndex = 0;
            StatusFilter.ItemsSource = new[] { "فعال", "غیرفعال" }; StatusFilter.SelectedItem = "فعال";
            AddButton.Visibility = EditButton.Visibility = SessionContext.Has(PermissionCatalog.MembersWrite) ? Visibility.Visible : Visibility.Collapsed;
            ArchiveButton.Visibility = DeleteButton.Visibility = SessionContext.Has(PermissionCatalog.MembersDelete) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RefreshData()
        {
            try
            {
                _current = _service.SearchMembers(SearchText.Text, Convert.ToString(TypeFilter.SelectedItem) ?? "همه", Convert.ToString(StatusFilter.SelectedItem) ?? "فعال", _page, PageSize);
                if (_page > _current.TotalPages) { _page = _current.TotalPages; RefreshData(); return; }
                MembersGrid.ItemsSource = _current.Items; CountText.Text = "تعداد نتیجه: " + _current.Total.ToString("N0", CultureInfo.InvariantCulture);
                PageText.Text = "صفحه " + _current.Page.ToString(CultureInfo.InvariantCulture) + " از " + _current.TotalPages.ToString(CultureInfo.InvariantCulture);
                PreviousButton.IsEnabled = _page > 1; NextButton.IsEnabled = _page < _current.TotalPages;
                ArchivedMembersButton.Content = "بایگانی‌شده‌ها  " + _service.CountArchived("عضو").ToString("N0", CultureInfo.InvariantCulture); ClearPreview();
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private MemberListItem? Selected() => MembersGrid.SelectedItem as MemberListItem;
        private void Search_Click(object sender, RoutedEventArgs e) { _page = 1; RefreshData(); }
        private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { _page = 1; RefreshData(); } }
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) { _page = 1; RefreshData(); } }
        private void Previous_Click(object sender, RoutedEventArgs e) { if (_page > 1) { _page--; RefreshData(); } }
        private void Next_Click(object sender, RoutedEventArgs e) { if (_current != null && _page < _current.TotalPages) { _page++; RefreshData(); } }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null) ViewSelected(); }

        private void MembersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { ClearPreview(); return; }
            try { _previewRecord = _service.GetMember(selected.Id); ShowPreview(_previewRecord); }
            catch (Exception ex) { ClearPreview(); _toast(UiMessages.Friendly(ex)); }
        }

        private void ShowPreview(MemberRecord? record)
        {
            TazkiraPreview.Source = null; if (record == null) { ClearPreview(); return; }
            QuickName.Text = record.FirstName + " — " + record.Type; QuickCode.Text = record.Code;
            QuickDetails.Text = "نام پدر: " + record.FatherName + "\nشماره تذکره: " + record.TazkiraNo + "\nموبایل: " + (string.IsNullOrWhiteSpace(record.Phone) ? "—" : record.Phone) + "\nاداره / مکتب: " + (string.IsNullOrWhiteSpace(record.Institution) ? "—" : record.Institution) + "\nآدرس فعلی: " + record.CurrentAddress;
            var exists = !string.IsNullOrWhiteSpace(record.AttachmentPath) && File.Exists(record.AttachmentPath); OpenTazkiraButton.IsEnabled = exists; SaveTazkiraButton.IsEnabled = exists;
            if (!exists) { PreviewHint.Text = "برای این عضو فایل تذکره ثبت نشده است."; PreviewHint.Visibility = Visibility.Visible; return; }
            var extension = Path.GetExtension(record.AttachmentPath).ToLowerInvariant();
            if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
            {
                try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(record.AttachmentPath, UriKind.Absolute); image.EndInit(); image.Freeze(); TazkiraPreview.Source = image; PreviewHint.Visibility = Visibility.Collapsed; }
                catch { PreviewHint.Text = "پیش‌نمایش تصویر ممکن نیست؛ از دکمه مشاهده اصل تذکره استفاده کنید."; PreviewHint.Visibility = Visibility.Visible; }
            }
            else { PreviewHint.Text = "فایل تذکره PDF است.\nبرای باز کردن، «مشاهده اصل تذکره» را بزنید."; PreviewHint.Visibility = Visibility.Visible; }
        }

        private void ClearPreview()
        {
            _previewRecord = null; TazkiraPreview.Source = null; PreviewHint.Text = "یک عضو را از فهرست انتخاب کنید."; PreviewHint.Visibility = Visibility.Visible;
            QuickName.Text = "—"; QuickCode.Text = "—"; QuickDetails.Text = "مشخصات کامل عضو بعد از انتخاب در این بخش دیده می‌شود."; OpenTazkiraButton.IsEnabled = false; SaveTazkiraButton.IsEnabled = false;
        }

        private void Add_Click(object sender, RoutedEventArgs e) { var dialog = new MemberDialog { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private void EditSelected()
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            var record = _service.GetMember(selected.Id); if (record == null) return; var dialog = new MemberDialog(record) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog);
        }
        private void Save(MemberDialog dialog) { try { _service.SaveMember(dialog.Result, dialog.NewAttachmentPath); RefreshData(); _toast("پرونده عضو با موفقیت ذخیره شد."); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); } }
        private void View_Click(object sender, RoutedEventArgs e) => ViewSelected();
        private void ViewSelected() { var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; } var record = _service.GetMember(selected.Id); if (record != null) new MemberDialog(record, true) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        private void OpenTazkira_Click(object sender, RoutedEventArgs e) { if (_previewRecord != null && File.Exists(_previewRecord.AttachmentPath)) Process.Start(new ProcessStartInfo(_previewRecord.AttachmentPath) { UseShellExecute = true }); }
        private void SaveTazkira_Click(object sender, RoutedEventArgs e)
        {
            if (_previewRecord == null || !File.Exists(_previewRecord.AttachmentPath)) return; var extension = Path.GetExtension(_previewRecord.AttachmentPath);
            var dialog = new SaveFileDialog { Title = "ذخیره کاپی تذکره", FileName = "Tazkira-" + _previewRecord.Code + extension, Filter = "فایل تذکره|*" + extension + "|همه فایل‌ها|*.*" };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true) { File.Copy(_previewRecord.AttachmentPath, dialog.FileName, true); _toast("کاپی تذکره ذخیره شد."); }
        }
        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; } var record = _service.GetMember(selected.Id); if (record == null) return;
            var dialog = new SaveFileDialog { Title = "ذخیره پرونده PDF", Filter = "PDF|*.pdf", FileName = "Member-" + record.Code + ".pdf" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { new ReportService().ExportMemberProfile(record, dialog.FileName, _service.GetSettings().CompanyName); _toast("فایل PDF ساخته شد."); Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطای PDF", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void Archive_Click(object sender, RoutedEventArgs e) { var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; } if (MessageBox.Show("عضو «" + selected.Name + "» بایگانی شود؟", "تأیید بایگانی", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; _service.ArchiveMember(selected.Id); RefreshData(); _toast("عضو بایگانی شد."); }
        private void Archived_Click(object sender, RoutedEventArgs e) { var dialog = new ArchiveDialog(_service, "عضو") { Owner = Window.GetWindow(this) }; dialog.ShowDialog(); if (dialog.Changed) RefreshData(); }
        private void Delete_Click(object sender, RoutedEventArgs e) { var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; } if (MessageBox.Show("عضو «" + selected.Name + "» به سطل زباله انتقال یابد؟\nبعداً می‌توانید آن را بازیابی کنید.", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; _service.DeleteMember(selected.Id); RefreshData(); _toast("عضو به سطل زباله انتقال یافت."); }
    }
}
