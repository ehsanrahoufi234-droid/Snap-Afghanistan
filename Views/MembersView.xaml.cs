using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class MembersView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository;
        private readonly Action<string> _toast;
        private int _page = 1;
        private const int PageSize = 200;
        private PagedResult<MemberListItem>? _current;

        public MembersView(SnapRepository repository, Action<string> toast)
        {
            InitializeComponent(); _repository = repository; _toast = toast;
            TypeFilter.ItemsSource = new[] { "همه" }.Concat(SnapRepository.MemberTypes).ToArray(); TypeFilter.SelectedIndex = 0;
            StatusFilter.ItemsSource = new[] { "همه", "فعال", "غیرفعال", "بایگانی" }; StatusFilter.SelectedIndex = 0;
        }

        public void RefreshData()
        {
            try
            {
                _current = _repository.SearchMembers(SearchText.Text, Convert.ToString(TypeFilter.SelectedItem) ?? "همه", Convert.ToString(StatusFilter.SelectedItem) ?? "همه", _page, PageSize);
                if (_page > _current.TotalPages) { _page = _current.TotalPages; RefreshData(); return; }
                MembersGrid.ItemsSource = _current.Items;
                CountText.Text = "تعداد نتیجه: " + _current.Total.ToString("N0", CultureInfo.InvariantCulture);
                PageText.Text = "صفحه " + _current.Page + " از " + _current.TotalPages;
                PreviousButton.IsEnabled = _page > 1; NextButton.IsEnabled = _page < _current.TotalPages;
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private MemberListItem? Selected() => MembersGrid.SelectedItem as MemberListItem;
        private void Search_Click(object sender, RoutedEventArgs e) { _page = 1; RefreshData(); }
        private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { _page = 1; RefreshData(); } }
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) { _page = 1; RefreshData(); } }
        private void Previous_Click(object sender, RoutedEventArgs e) { if (_page > 1) { _page--; RefreshData(); } }
        private void Next_Click(object sender, RoutedEventArgs e) { if (_current != null && _page < _current.TotalPages) { _page++; RefreshData(); } }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null) EditSelected(); }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MemberDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            Save(dialog);
        }

        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private void EditSelected()
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            var record = _repository.GetMember(selected.Id); if (record == null) return;
            var dialog = new MemberDialog(record) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) Save(dialog);
        }

        private void Save(MemberDialog dialog)
        {
            try { _repository.SaveMember(dialog.Result, dialog.NewAttachmentPath); RefreshData(); _toast("پرونده عضو با موفقیت ذخیره شد."); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            var record = _repository.GetMember(selected.Id); if (record == null) return;
            new MemberDialog(record, true) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            var record = _repository.GetMember(selected.Id); if (record == null) return;
            var dialog = new SaveFileDialog { Title = "ذخیره پرونده PDF", Filter = "PDF|*.pdf", FileName = "Member-" + record.Code + ".pdf" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { new ReportService().ExportMemberProfile(record, dialog.FileName, _repository.GetSettings().CompanyName); _toast("فایل PDF ساخته شد."); Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطای PDF", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            if (MessageBox.Show("این عضو بایگانی شود؟", "تأیید بایگانی", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _repository.ArchiveMember(selected.Id); RefreshData(); _toast("عضو بایگانی شد.");
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک عضو را انتخاب کنید."); return; }
            if (MessageBox.Show("عضو «" + selected.Name + "» به سطل زباله انتقال یابد؟\nبعداً می‌توانید آن را بازیابی کنید.", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _repository.DeleteMember(selected.Id); RefreshData(); _toast("عضو به سطل زباله انتقال یافت.");
        }
    }
}
