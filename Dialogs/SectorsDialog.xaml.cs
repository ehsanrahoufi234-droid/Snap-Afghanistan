using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class SectorsDialog : Window
    {
        private readonly ISnapService _service;
        public SectorsDialog(ISnapService service) { InitializeComponent(); _service = service; Loaded += (s, e) => Refresh(); }
        private void Refresh() { Grid.ItemsSource = _service.GetSectors().Where(x => x.Status != "بایگانی").ToList(); ArchivedButton.Content = "بایگانی‌شده‌ها  " + _service.CountArchived("سکتور").ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        private SectorItem? Selected() => Grid.SelectedItem as SectorItem;
        private void Add_Click(object sender, RoutedEventArgs e) => EditSector(new SectorItem());
        private void Edit_Click(object sender, RoutedEventArgs e) { var item = Selected(); if (item != null) EditSector(item); }
        private void Edit_DoubleClick(object sender, MouseButtonEventArgs e) { var item = Selected(); if (item != null) EditSector(item); }
        private void EditSector(SectorItem sector)
        {
            var editor = new SectorEditorDialog(sector) { Owner = this }; if (editor.ShowDialog() != true) return;
            try { _service.SaveSector(editor.Result); Refresh(); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected(); if (item == null) { MessageBox.Show("ابتدا یک سکتور را انتخاب کنید."); return; }
            if (MessageBox.Show("سکتور «" + item.Name + "» بایگانی شود؟", "تأیید بایگانی", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { _service.ArchiveSector(item.Id); Refresh(); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بایگانی نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void Archived_Click(object sender, RoutedEventArgs e) { var dialog = new ArchiveDialog(_service, "سکتور") { Owner = this }; dialog.ShowDialog(); if (dialog.Changed) Refresh(); }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected(); if (item == null) return; if (MessageBox.Show("سکتور «" + item.Name + "» به سطل زباله انتقال یابد؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try { _service.DeleteSector(item.Id); Refresh(); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "حذف نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    internal sealed class SectorEditorDialog : Window
    {
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _description = new TextBox { Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        private readonly ComboBox _status = new ComboBox { ItemsSource = new[] { "فعال", "غیرفعال" } };
        public SectorItem Result { get; }
        public SectorEditorDialog(SectorItem source)
        {
            Result = new SectorItem { Id = source.Id, Name = source.Name, Description = source.Description, Status = source.Status, CenterCount = source.CenterCount, Version = source.Version };
            Title = string.IsNullOrWhiteSpace(source.Id) ? "سکتور جدید" : "ویرایش سکتور"; Width = 540; Height = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
            var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(new TextBlock { Text = Title, FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 22) });
            panel.Children.Add(new TextBlock { Text = "نام سکتور *", Style = (Style)Application.Current.Resources["FieldLabelStyle"] }); _name.Text = source.Name; panel.Children.Add(_name);
            panel.Children.Add(new TextBlock { Text = "توضیحات", Style = (Style)Application.Current.Resources["FieldLabelStyle"], Margin = new Thickness(0, 15, 0, 7) }); _description.Text = source.Description; panel.Children.Add(_description);
            panel.Children.Add(new TextBlock { Text = "وضعیت", Style = (Style)Application.Current.Resources["FieldLabelStyle"], Margin = new Thickness(0, 15, 0, 7) }); _status.SelectedItem = string.IsNullOrWhiteSpace(source.Status) ? "فعال" : source.Status; panel.Children.Add(_status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0) };
            var cancel = new Button { Content = "انصراف", Width = 100, IsCancel = true, Style = (Style)Application.Current.Resources["GhostButton"] };
            var save = new Button { Content = "ذخیره", Width = 110, Margin = new Thickness(8, 0, 0, 0), Style = (Style)Application.Current.Resources["PrimaryButton"] };
            save.Click += (s, e) => { if (string.IsNullOrWhiteSpace(_name.Text)) { MessageBox.Show("نام سکتور ضروری است."); return; } Result.Name = _name.Text.Trim(); Result.Description = _description.Text.Trim(); Result.Status = Convert.ToString(_status.SelectedItem) ?? "فعال"; DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); Content = panel;
        }
    }
}
