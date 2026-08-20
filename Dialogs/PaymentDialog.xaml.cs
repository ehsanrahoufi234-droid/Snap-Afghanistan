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
        public decimal Amount { get; private set; }
        public DateTime Date { get; private set; }
        public int Months { get; private set; }
        public string ReceiptNo => ReceiptText.Text.Trim();
        public string Notes => NotesText.Text.Trim();

        public PaymentDialog(CenterRecord center, PaymentItem? payment = null)
        {
            InitializeComponent();
            CenterName.Text = center.LegalName + " — " + center.Code;
            MonthsCombo.ItemsSource = Enumerable.Range(1, 60).ToArray();
            if (payment == null)
            {
                HeaderTitle.Text = "ثبت پرداخت اشتراک";
                SaveButton.Content = "ثبت و صدور رسید";
                AmountText.Text = center.MonthlySubscription > 0 ? center.MonthlySubscription.ToString("0", CultureInfo.InvariantCulture) : "";
                PaymentDateText.Text = DateService.Solar(DateTime.Today);
                MonthsCombo.SelectedItem = 1;
            }
            else
            {
                HeaderTitle.Text = "ویرایش پرداخت ثبت‌شده";
                SaveButton.Content = "تأیید و ذخیره اصلاحات";
                AmountText.Text = payment.Amount.ToString("0.##", CultureInfo.InvariantCulture);
                DateTime date;
                PaymentDateText.Text = DateService.TryParseIso(payment.PaymentDate, out date) ? DateService.Solar(date) : DateService.Solar(DateTime.Today);
                MonthsCombo.SelectedItem = payment.CoveredMonths <= 0 ? 1 : payment.CoveredMonths;
                ReceiptText.Text = payment.ReceiptNo;
                NotesText.Text = payment.Notes;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal amount;
            if (!decimal.TryParse(AmountText.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                ErrorText.Text = "مبلغ پرداخت معتبر نیست.";
                return;
            }
            DateTime date;
            if (!DateService.TryParseSolar(PaymentDateText.Text, out date))
            {
                ErrorText.Text = "تاریخ هجری شمسی معتبر نیست. نمونه: 1405/05/30";
                return;
            }
            Amount = amount;
            Date = date;
            Months = Convert.ToInt32(MonthsCombo.SelectedItem ?? 1);
            PaymentGregorian.Text = "معادل میلادی: " + DateService.Gregorian(date);
            DialogResult = true;
        }
    }
}