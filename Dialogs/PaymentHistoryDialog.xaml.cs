using System;
using System.Windows;
using System.Windows.Input;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class PaymentHistoryDialog : Window
    {
        private readonly SnapRepository _repository;
        private readonly OperationsService _operations = new OperationsService();
        private readonly string _centerId;
        private readonly string _centerName;
        public bool Changed { get; private set; }

        public PaymentHistoryDialog(SnapRepository repository, string centerId, string centerName)
        {
            InitializeComponent();
            _repository = repository;
            _centerId = centerId;
            _centerName = centerName;
            CenterName.Text = centerName;
            Refresh();
        }

        private void Refresh() => Grid.ItemsSource = _repository.GetPayments(_centerId);
        private PaymentItem? Selected() => Grid.SelectedItem as PaymentItem;
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null) EditSelected(); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

        private void EditSelected()
        {
            var payment = Selected();
            if (payment == null) { MessageBox.Show("ابتدا یک پرداخت را انتخاب کنید.", "سوابق پرداخت", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var center = _repository.GetCenter(_centerId);
            if (center == null) return;
            var dialog = new PaymentDialog(center, payment) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                _operations.UpdatePayment(payment, dialog.Amount, dialog.Date, dialog.Months, dialog.ReceiptNo, dialog.Notes);
                Changed = true;
                Refresh();
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ویرایش نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var payment = Selected();
            if (payment == null) { MessageBox.Show("ابتدا یک پرداخت را انتخاب کنید.", "سوابق پرداخت", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (MessageBox.Show("پرداخت رسید «" + payment.ReceiptNo + "» حذف شود؟\nسررسید و آمار درآمد دوباره محاسبه می‌شود.", "تأیید حذف پرداخت", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                _operations.DeletePayment(payment);
                Changed = true;
                Refresh();
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "حذف نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}