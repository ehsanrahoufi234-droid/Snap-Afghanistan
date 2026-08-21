using System;
using System.Globalization;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class CenterDialog : Window
    {
        private readonly CenterRecord _record;
        public CenterRecord Result => _record;

        public CenterDialog(ISnapService service, CenterRecord? record = null)
        {
            InitializeComponent(); _record = record ?? new CenterRecord();
            SectorCombo.ItemsSource = service.GetSectors(false);
            FeeBasisCombo.ItemsSource = new[] { "اشتراک ماهانه", "فیصدی بل", "مبلغ ثابت روزانه", "بدون حق‌الخدمت" };
            StatusCombo.ItemsSource = new[] { "فعال", "غیرفعال" }; LoadRecord();
        }

        private void LoadRecord()
        {
            HeaderTitle.Text = string.IsNullOrWhiteSpace(_record.Id) ? "ثبت مرکز جدید" : "ویرایش مرکز"; CodeText.Text = string.IsNullOrWhiteSpace(_record.Code) ? "پس از ذخیره ساخته می‌شود" : _record.Code;
            SectorCombo.SelectedValue = _record.SectorId; LegalNameText.Text = _record.LegalName; TradeNameText.Text = _record.TradeName; LicenseText.Text = _record.LicenseNo; RepresentativeText.Text = _record.Representative;
            PhoneText.Text = _record.Phone; DiscountText.Text = _record.DiscountRate; AddressText.Text = _record.Address; FeeBasisCombo.SelectedItem = _record.FeeBasis; FeeAmountText.Text = _record.FeeAmount == 0 ? "" : _record.FeeAmount.ToString("0.##", CultureInfo.InvariantCulture);
            StatusCombo.SelectedItem = _record.Status == "غیرفعال" ? "غیرفعال" : "فعال"; NotesText.Text = _record.Notes;
            DateTime date; if (DateService.TryParseIso(_record.ContractStart, out date)) { ContractStartText.Text = DateService.Solar(date); ContractStartGregorian.Text = "معادل میلادی: " + DateService.Gregorian(date); }
            if (DateService.TryParseIso(_record.ContractEnd, out date)) { ContractEndText.Text = DateService.Solar(date); ContractEndGregorian.Text = "معادل میلادی: " + DateService.Gregorian(date); }
            if (SectorCombo.SelectedIndex < 0 && SectorCombo.Items.Count > 0) SectorCombo.SelectedIndex = 0; if (FeeBasisCombo.SelectedIndex < 0) FeeBasisCombo.SelectedItem = "اشتراک ماهانه"; if (StatusCombo.SelectedIndex < 0) StatusCombo.SelectedItem = "فعال";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Text = ""; _record.SectorId = Convert.ToString(SectorCombo.SelectedValue) ?? ""; _record.LegalName = LegalNameText.Text.Trim(); _record.TradeName = TradeNameText.Text.Trim(); _record.LicenseNo = LicenseText.Text.Trim(); _record.Representative = RepresentativeText.Text.Trim(); _record.Phone = PhoneText.Text.Trim(); _record.DiscountRate = DiscountText.Text.Trim(); _record.Address = AddressText.Text.Trim();
                DateTime startDate; if (string.IsNullOrWhiteSpace(ContractStartText.Text)) _record.ContractStart = ""; else if (DateService.TryParseSolar(ContractStartText.Text, out startDate)) _record.ContractStart = DateService.Iso(startDate); else throw new InvalidOperationException("تاریخ آغاز قرارداد معتبر نیست. نمونه: 1405/05/30");
                DateTime endDate; if (string.IsNullOrWhiteSpace(ContractEndText.Text)) _record.ContractEnd = ""; else if (DateService.TryParseSolar(ContractEndText.Text, out endDate)) _record.ContractEnd = DateService.Iso(endDate); else throw new InvalidOperationException("تاریخ پایان قرارداد معتبر نیست. نمونه: 1406/05/30");
                if (!string.IsNullOrWhiteSpace(_record.ContractStart) && !string.IsNullOrWhiteSpace(_record.ContractEnd)) { DateTime start, end; DateService.TryParseIso(_record.ContractStart, out start); DateService.TryParseIso(_record.ContractEnd, out end); if (end.Date < start.Date) throw new InvalidOperationException("پایان قرارداد نمی‌تواند قبل از آغاز قرارداد باشد."); }
                _record.FeeBasis = Convert.ToString(FeeBasisCombo.SelectedItem) ?? "اشتراک ماهانه"; decimal amount; if (!decimal.TryParse(FeeAmountText.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) amount = 0; _record.FeeAmount = amount; _record.Status = Convert.ToString(StatusCombo.SelectedItem) ?? "فعال"; _record.Notes = NotesText.Text.Trim();
                if (string.IsNullOrWhiteSpace(_record.SectorId) || string.IsNullOrWhiteSpace(_record.LegalName)) throw new InvalidOperationException("سکتور و نام قانونی مرکز ضروری است."); DialogResult = true;
            }
            catch (Exception exception) { ErrorText.Text = exception.Message; }
        }
    }
}
