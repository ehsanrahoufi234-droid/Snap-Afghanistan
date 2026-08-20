using System;
using System.Globalization;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class SubscriptionDialog : Window
    {
        public decimal Amount { get; private set; }
        public DateTime Start { get; private set; }
        public DateTime Due { get; private set; }
        public bool Suspended => SuspendedCheck.IsChecked == true;

        public SubscriptionDialog(CenterRecord center)
        {
            InitializeComponent();
            CenterName.Text = center.LegalName + " — " + center.Code;
            AmountText.Text = center.MonthlySubscription <= 0 ? "" : center.MonthlySubscription.ToString("0", CultureInfo.InvariantCulture);
            DateTime date;
            var start = DateService.TryParseIso(center.SubscriptionStart, out date) ? date : DateTime.Today;
            var due = DateService.TryParseIso(center.NextDueDate, out date) ? date : DateTime.Today.AddMonths(1);
            StartDateText.Text = DateService.Solar(start);
            DueDateText.Text = DateService.Solar(due);
            StartGregorian.Text = "معادل میلادی: " + DateService.Gregorian(start);
            DueGregorian.Text = "معادل میلادی: " + DateService.Gregorian(due);
            SuspendedCheck.IsChecked = center.SubscriptionSuspended;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal amount;
            if (!decimal.TryParse(AmountText.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || amount < 0)
            {
                ErrorText.Text = "مبلغ اشتراک معتبر نیست.";
                return;
            }
            DateTime start, due;
            if (!DateService.TryParseSolar(StartDateText.Text, out start) || !DateService.TryParseSolar(DueDateText.Text, out due))
            {
                ErrorText.Text = "تاریخ آغاز و سررسید را به شکل هجری شمسی وارد کنید؛ نمونه: 1405/05/30";
                return;
            }
            if (due.Date < start.Date)
            {
                ErrorText.Text = "سررسید باید پس از تاریخ آغاز باشد.";
                return;
            }
            Amount = amount;
            Start = start;
            Due = due;
            DialogResult = true;
        }
    }
}