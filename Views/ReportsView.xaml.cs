using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class ReportsView : UserControl, IRefreshable
    {
        private sealed class ReportChoice { public string Key { get; set; } = ""; public string Name { get; set; } = ""; public override string ToString() => Name; }
        private readonly SnapRepository _repository;
        private readonly Action<string> _toast;
        private DataTable? _table;

        public ReportsView(SnapRepository repository, Action<string> toast)
        {
            InitializeComponent();
            _repository = repository;
            _toast = toast;
            ReportType.ItemsSource = new[]
            {
                new ReportChoice { Key="members",Name="فهرست اعضا" },
                new ReportChoice { Key="centers",Name="فهرست مراکز" },
                new ReportChoice { Key="sectors",Name="فهرست سکتورها" },
                new ReportChoice { Key="debtors",Name="مراکز بدهکار" },
                new ReportChoice { Key="payments",Name="درآمد و پرداخت‌ها" }
            };
            ReportType.SelectedIndex = 0;
            MemberType.ItemsSource = new[] { "همه" }.Concat(SnapRepository.MemberTypes).ToArray();
            MemberType.SelectedIndex = 0;
        }

        public void RefreshData() { if (_table == null) Preview(); }
        private void ReportType_Changed(object sender, SelectionChangedEventArgs e) { if (MemberType != null) MemberType.IsEnabled = ((ReportChoice)ReportType.SelectedItem).Key == "members"; }

        private void Grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.MinWidth = 125;
            e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            if (e.Column is DataGridTextColumn textColumn)
            {
                var cellStyle = new Style(typeof(TextBlock));
                cellStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                cellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                cellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                textColumn.ElementStyle = cellStyle;
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e) => Preview();

        private void Preview()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var choice = (ReportChoice)ReportType.SelectedItem;
                _table = _repository.BuildReport(choice.Key, Convert.ToString(MemberType.SelectedItem) ?? "همه");
                DateService.NormalizeReportDates(_table);
                Grid.ItemsSource = _table.DefaultView;
                CountText.Text = "تعداد ردیف: " + _table.Rows.Count.ToString("N0", CultureInfo.InvariantCulture);
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "گزارش ساخته نشد", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { Mouse.OverrideCursor = null; }
        }

        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            EnsureReport(); if (_table == null) return; var choice = (ReportChoice)ReportType.SelectedItem;
            var dialog = new SaveFileDialog { Title="ذخیره PDF",Filter="PDF|*.pdf",FileName="Snap-"+choice.Key+"-Report.pdf" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { Mouse.OverrideCursor = Cursors.Wait; new ReportService().ExportTablePdf(_table, dialog.FileName, choice.Name, _repository.GetSettings().CompanyName); _toast("گزارش PDF با تاریخ هجری شمسی ساخته شد."); Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطای PDF", MessageBoxButton.OK, MessageBoxImage.Error); } finally { Mouse.OverrideCursor = null; }
        }

        private void Csv_Click(object sender, RoutedEventArgs e)
        {
            EnsureReport(); if (_table == null) return; var choice = (ReportChoice)ReportType.SelectedItem;
            var dialog = new SaveFileDialog { Title="ذخیره CSV",Filter="CSV|*.csv",FileName="Snap-"+choice.Key+"-Report.csv" }; if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { new ReportService().ExportCsv(_table, dialog.FileName); _toast("فایل CSV با تاریخ هجری شمسی ساخته شد."); Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "خطای CSV", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void EnsureReport() { if (_table == null) Preview(); }
    }
}