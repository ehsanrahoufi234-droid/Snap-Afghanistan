using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Views
{
    public partial class NotesView : UserControl, IRefreshable
    {
        private readonly SnapRepository _repository; private readonly Action<string> _toast;
        public NotesView(SnapRepository repository, Action<string> toast) { InitializeComponent(); _repository = repository; _toast = toast; StatusFilter.ItemsSource = new[] { "همه", "باز", "در حال انجام", "انجام شد" }; StatusFilter.SelectedIndex = 0; }
        public void RefreshData() { Grid.ItemsSource = _repository.GetNotes(Convert.ToString(StatusFilter.SelectedItem) ?? "همه"); }
        private NoteItem? Selected() => Grid.SelectedItem as NoteItem;
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshData(); }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null) EditSelected(); }
        private void Add_Click(object sender, RoutedEventArgs e) { var dialog = new NoteDialog { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private void EditSelected() { var selected = Selected(); if (selected == null) { _toast("ابتدا یک یادداشت را انتخاب کنید."); return; } var dialog = new NoteDialog(selected) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Save(NoteDialog dialog) { try { _repository.SaveNote(dialog.Result); RefreshData(); _toast("یادداشت ذخیره شد."); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); } }
        private void Delete_Click(object sender, RoutedEventArgs e) { var selected = Selected(); if (selected == null) { _toast("ابتدا یک یادداشت را انتخاب کنید."); return; } if (MessageBox.Show("این یادداشت به سطل زباله انتقال یابد؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; _repository.DeleteNote(selected.Id); RefreshData(); _toast("یادداشت حذف شد."); }
    }
}
