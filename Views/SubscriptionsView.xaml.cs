using System;
using System.Diagnostics;
using System.Globalization;
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
    public partial class SubscriptionsView : UserControl, IRefreshable
    {
        private readonly ISnapService _service; private readonly Action<string> _toast;
        public SubscriptionsView(ISnapService service, Action<string> toast)
        {
            InitializeComponent(); _service = service; _toast = toast; StatusFilter.ItemsSource = new[] { "همه", "فعال", "نزدیک سررسید", "معوق", "تعلیق", "تنظیم نشده" }; StatusFilter.SelectedIndex = 0;
            ConfigureButton.Visibility = PaymentButton.Visibility = SessionContext.Has(PermissionCatalog.SubscriptionsWrite) ? Visibility.Visible : Visibility.Collapsed;
        }
        public void SetStatusFilter(string status) { StatusFilter.SelectedItem = string.IsNullOrWhiteSpace(status) ? "همه" : status; if (StatusFilter.SelectedIndex < 0) StatusFilter.SelectedItem = "همه"; if (IsLoaded) RefreshData(); }
        public void RefreshData()
        {
            try
            {
                var status = Convert.ToString(StatusFilter.SelectedItem) ?? "همه"; var data = _service.SearchCenters(SearchText.Text, "همه", status, 1, 500); var visible = data.Items.Where(item => item.Status != "بایگانی").ToList(); Grid.ItemsSource = visible;
                var stats = _service.GetDashboard(); RevenueCard.Text = stats.MonthRevenue.ToString("N0", CultureInfo.InvariantCulture) + " افغانی"; NearCard.Text = stats.NearDue.ToString("N0", CultureInfo.InvariantCulture); OverdueCard.Text = stats.Overdue.ToString("N0", CultureInfo.InvariantCulture);
                var active = _service.SearchCenters("", "همه", "فعال", 1, 500).Items.Count(item => item.Status != "بایگانی"); ActiveCard.Text = active.ToString("N0", CultureInfo.InvariantCulture);
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private CenterListItem? Selected() => Grid.SelectedItem as CenterListItem;
        private void Search_Click(object sender, RoutedEventArgs e) => RefreshData();
        private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) RefreshData(); }
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshData(); }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null && SessionContext.Has(PermissionCatalog.SubscriptionsWrite)) ConfigureSelected(); }
        private void Configure_Click(object sender, RoutedEventArgs e) => ConfigureSelected();
        private void ConfigureSelected()
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; } var center = _service.GetCenter(selected.Id); if (center == null) return;
            var dialog = new SubscriptionDialog(center) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() != true) return;
            try { _service.ConfigureSubscription(center.Id, dialog.Amount, dialog.Start, dialog.Due, dialog.Suspended); RefreshData(); _toast("اشتراک مرکز تنظیم شد."); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void Payment_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; } var center = _service.GetCenter(selected.Id); if (center == null) return;
            if (center.MonthlySubscription <= 0) { MessageBox.Show("ابتدا مبلغ اشتراک ماهانه این مرکز را تنظیم کنید.", "اشتراک تنظیم نشده", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (string.IsNullOrWhiteSpace(center.NextDueDate)) { MessageBox.Show("ابتدا سررسید اشتراک این مرکز را تنظیم کنید.", "سررسید تنظیم نشده", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dialog = new PaymentDialog(center) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() != true) return;
            try { var receipt = _service.RegisterPayment(center.Id, dialog.Amount, dialog.Date, dialog.Months, dialog.ReceiptNo, dialog.Notes); RefreshData(); MessageBox.Show("پرداخت ثبت شد.\nشماره رسید: " + receipt, "ثبت موفق", MessageBoxButton.OK, MessageBoxImage.Information); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "پرداخت ثبت نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void History_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected(); if (selected == null) { _toast("ابتدا یک مرکز را انتخاب کنید."); return; }
            var dialog = new PaymentHistoryDialog(_service, selected.Id, selected.Name) { Owner = Window.GetWindow(this) }; dialog.ShowDialog(); if (dialog.Changed) { RefreshData(); _toast("سوابق پرداخت و سررسید به‌روز شد."); }
        }
        private void OverduePdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = "ذخیره گزارش مراکز معوق", Filter = "PDF|*.pdf", FileName = "Snap-Overdue-Centers.pdf" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { var table = _service.BuildReport("debtors"); DateService.NormalizeReportDates(table); new ReportService().ExportTablePdf(table, dialog.FileName, "مراکز دارای اشتراک معوق", _service.GetSettings().CompanyName); _toast("گزارش PDF با تاریخ هجری شمسی ساخته شد."); Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطای PDF", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
