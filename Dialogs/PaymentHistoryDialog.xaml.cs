using System;
using System.Windows;
using System.Windows.Input;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class PaymentHistoryDialog : Window
    {
        private readonly ISnapService _service; private readonly string _centerId; public bool Changed { get; private set; }
        public PaymentHistoryDialog(ISnapService service, string centerId, string centerName)
        {
            InitializeComponent(); _service = service; _centerId = centerId; CenterName.Text = centerName;
            EditButton.Visibility = SessionContext.Has(PermissionCatalog.SubscriptionsWrite) ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = SessionContext.Has(PermissionCatalog.SubscriptionsDelete) ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }
        private void Refresh() => Grid.ItemsSource = _service.GetPayments(_centerId);
        private PaymentItem? Selected() => Grid.SelectedItem as PaymentItem;
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null && SessionContext.Has(PermissionCatalog.SubscriptionsWrite)) EditSelected(); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private void EditSelected()
        {
            var payment = Selected(); if (payment == null) { MessageBox.Show("ابتدا یک پرداخت را انتخاب کنید.", "سوابق پرداخت", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var center = _service.GetCenter(_centerId); if (center == null) return; var dialog = new PaymentDialog(center, payment) { Owner = this }; if (dialog.ShowDialog() != true) return;
            try { _service.UpdatePayment(payment, dialog.Amount, dialog.Date, dialog.Months, dialog.ReceiptNo, dialog.Notes); Changed = true; Refresh(); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ویرایش نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var payment = Selected(); if (payment == null) { MessageBox.Show("ابتدا یک پرداخت را انتخاب کنید.", "سوابق پرداخت", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (MessageBox.Show("پرداخت رسید «" + payment.ReceiptNo + "» حذف شود؟\nسررسید و آمار درآمد دوباره محاسبه می‌شود.", "تأیید حذف پرداخت", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try { _service.DeletePayment(payment); Changed = true; Refresh(); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "حذف نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}
