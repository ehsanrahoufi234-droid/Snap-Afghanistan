using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class PaymentDialog : Window
    {
        public decimal Amount { get; private set; } public DateTime Date { get; private set; } public int Months { get; private set; } public string ReceiptNo => ReceiptText.Text.Trim(); public string Notes => NotesText.Text.Trim();
        public PaymentDialog(CenterRecord center)
        {
            InitializeComponent(); CenterName.Text = center.LegalName + " — " + center.Code; AmountText.Text = center.MonthlySubscription > 0 ? center.MonthlySubscription.ToString("0", CultureInfo.InvariantCulture) : "";
            PaymentDate.SelectedDate = DateTime.Today; MonthsCombo.ItemsSource = Enumerable.Range(1, 24).ToArray(); MonthsCombo.SelectedItem = 1;
        }
        private void Date_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { PaymentSolar.Text = PaymentDate.SelectedDate.HasValue ? "هجری شمسی: " + DateService.Solar(PaymentDate.SelectedDate.Value) : ""; }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal amount; if (!decimal.TryParse(AmountText.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || amount <= 0) { ErrorText.Text = "مبلغ پرداخت معتبر نیست."; return; }
            if (!PaymentDate.SelectedDate.HasValue) { ErrorText.Text = "تاریخ پرداخت را انتخاب کنید."; return; }
            Amount = amount; Date = PaymentDate.SelectedDate.Value; Months = Convert.ToInt32(MonthsCombo.SelectedItem ?? 1); DialogResult = true;
        }
    }
}
