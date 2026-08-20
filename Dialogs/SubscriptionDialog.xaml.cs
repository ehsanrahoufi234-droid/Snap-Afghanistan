using System;
using System.Globalization;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class SubscriptionDialog : Window
    {
        public decimal Amount { get; private set; } public DateTime Start { get; private set; } public DateTime Due { get; private set; } public bool Suspended => SuspendedCheck.IsChecked == true;
        public SubscriptionDialog(CenterRecord center)
        {
            InitializeComponent(); CenterName.Text = center.LegalName + " — " + center.Code; AmountText.Text = center.MonthlySubscription <= 0 ? "" : center.MonthlySubscription.ToString("0", CultureInfo.InvariantCulture);
            DateTime date; StartDate.SelectedDate = DateService.TryParseIso(center.SubscriptionStart, out date) ? date : DateTime.Today;
            DueDate.SelectedDate = DateService.TryParseIso(center.NextDueDate, out date) ? date : DateTime.Today.AddMonths(1); SuspendedCheck.IsChecked = center.SubscriptionSuspended;
        }
        private void Date_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { StartSolar.Text = StartDate.SelectedDate.HasValue ? "هجری شمسی: " + DateService.Solar(StartDate.SelectedDate.Value) : ""; DueSolar.Text = DueDate.SelectedDate.HasValue ? "هجری شمسی: " + DateService.Solar(DueDate.SelectedDate.Value) : ""; }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal amount; if (!decimal.TryParse(AmountText.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || amount < 0) { ErrorText.Text = "مبلغ اشتراک معتبر نیست."; return; }
            if (!StartDate.SelectedDate.HasValue || !DueDate.SelectedDate.HasValue) { ErrorText.Text = "تاریخ آغاز و سررسید را انتخاب کنید."; return; }
            if (DueDate.SelectedDate.Value.Date < StartDate.SelectedDate.Value.Date) { ErrorText.Text = "سررسید باید پس از تاریخ آغاز باشد."; return; }
            Amount = amount; Start = StartDate.SelectedDate.Value; Due = DueDate.SelectedDate.Value; DialogResult = true;
        }
    }
}
