using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class CentersView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository;
        private readonly OperationsService _operations = new OperationsService();
        private readonly Action<string> _toast;
        private int _page = 1;
        private const int PageSize = 200;
        private PagedResult<CenterListItem>? _current;

        public CentersView(SnapRepository repository, Action<string> toast)
        {
            InitializeComponent();
            _repository = repository;
            _toast = toast;
            StatusFilter.ItemsSource = new[] { "همه", "فعال", "نزدیک سررسید", "معوق", "تعلیق", "تنظیم نشده" };
            StatusFilter.SelectedIndex = 0;
            LoadSectors();
        }

        private void LoadSectors()
        {
            var sectors = new List<SectorItem> { new SectorItem { Id = "همه", Name = "همه سکتورها" } };
            sectors.AddRange(_repository.GetSectors(false).Where(x => x.Status != "بایگانی"));
            SectorFilter.ItemsSource = sectors;
            SectorFilter.SelectedIndex = 0;
        }

        public void RefreshData()
        {
            try
            {
                var sector = Convert.ToString(SectorFilter.SelectedValue) ?? "همه";
                _current = _repository.SearchCenters(SearchText.Text, sector, Convert.ToString(StatusFilter.SelectedItem) ?? "همه", _page, PageSize);
                if (_page > _current.TotalPages) { _page = _current.TotalPages; RefreshData(); return; }
                var visible = _current.Items.Where(x => x.Status != "بایگانی").ToList();
                CentersGrid.ItemsSource = visible;
                CountText.Text = "مراکز قابل نمایش در این صفحه: " + visible.Count.ToString("N0", CultureInfo.InvariantCulture);
                PageText.Text = "صفحه " + _current.Page.ToString(CultureInfo.InvariantCulture) + " از " + _current.TotalPages.ToString(CultureInfo.InvariantCulture);
                PreviousButton.IsEnabled = _page > 1;
                NextButton.IsEnabled = _page < _current.TotalPages;
                ArchivedCentersButton.Content = "بایگانی‌شده‌ها  " + _operations.CountArchived("مرکز").ToString("N0", CultureInfo.InvariantCulture);
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private CenterListItem? Selected() => CentersGrid.SelectedItem as CenterListItem;
        private void Search_Click(object sender, RoutedEventArgs e) { _page = 1; RefreshData(); }
        private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { _page = 1; RefreshData(); } }
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) { _page = 1; RefreshData(); } }
        private void Previous_Click(object sender, RoutedEventArgs e) { if (_page > 1) { _page--; RefreshData(); } }
        private void Next_Click(object sender, RoutedEventArgs e) { if (_current != null && _page < _current.TotalPages) { _page++; RefreshData(); } }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null) EditSelected(); }
        private void Add_Click(object sender, RoutedEventArgs e) { var dialog = new CenterDialog(_repository) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

        private void EditSelected()
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; }
            var record = _repository.GetCenter(selected.Id); if (record == null) return;
            var dialog = new CenterDialog(_repository, record) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) Save(dialog);
        }

        private void Save(CenterDialog dialog)
        {
            try { _repository.SaveCenter(dialog.Result); RefreshData(); _toast("مرکز با موفقیت ذخیره شد."); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Sectors_Click(object sender, RoutedEventArgs e)
        {
            new SectorsDialog(_repository) { Owner = Window.GetWindow(this) }.ShowDialog();
            LoadSectors();
            RefreshData();
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; }
            if (MessageBox.Show("مرکز «" + selected.Name + "» بایگانی شود؟", "تأیید", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _repository.ArchiveCenter(selected.Id);
            RefreshData();
            _toast("مرکز بایگانی شد.");
        }

        private void Archived_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ArchiveDialog("مرکز") { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (dialog.Changed) RefreshData();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; }
            if (MessageBox.Show("مرکز «" + selected.Name + "» به سطل زباله انتقال یابد؟\nسوابق پرداخت تا زمان حذف دائمی حفظ می‌شود.", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _repository.DeleteCenter(selected.Id);
            RefreshData();
            _toast("مرکز به سطل زباله انتقال یافت.");
        }
    }
}