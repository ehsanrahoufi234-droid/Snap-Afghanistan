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
        private readonly ISnapService _service; private readonly Action<string> _toast;
        public NotesView(ISnapService service, Action<string> toast)
        {
            InitializeComponent(); _service = service; _toast = toast; StatusFilter.ItemsSource = new[] { "همه", "باز", "در حال انجام", "انجام شد" }; StatusFilter.SelectedIndex = 0;
            AddButton.Visibility = EditButton.Visibility = SessionContext.Has(PermissionCatalog.NotesWrite) ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = SessionContext.Has(PermissionCatalog.NotesDelete) ? Visibility.Visible : Visibility.Collapsed;
        }
        public void RefreshData() { try { Grid.ItemsSource = _service.GetNotes(Convert.ToString(StatusFilter.SelectedItem) ?? "همه"); } catch (Exception ex) { MessageBox.Show(UiMessages.Friendly(ex), "یادداشت‌ها", MessageBoxButton.OK, MessageBoxImage.Warning); } }
        private NoteItem? Selected() => Grid.SelectedItem as NoteItem;
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshData(); }
        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) { if (Selected() != null && SessionContext.Has(PermissionCatalog.NotesWrite)) EditSelected(); }
        private void Add_Click(object sender, RoutedEventArgs e) { var dialog = new NoteDialog { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private void EditSelected() { var selected = Selected(); if (selected == null) { _toast("ابتدا یک یادداشت را انتخاب کنید."); return; } var dialog = new NoteDialog(selected) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) Save(dialog); }
        private void Save(NoteDialog dialog) { try { _service.SaveNote(dialog.Result); RefreshData(); _toast("یادداشت ذخیره شد."); } catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "ذخیره نشد", MessageBoxButton.OK, MessageBoxImage.Warning); } }
        private void Delete_Click(object sender, RoutedEventArgs e) { var selected = Selected(); if (selected == null) { _toast("ابتدا یک یادداشت را انتخاب کنید."); return; } if (MessageBox.Show("این یادداشت به سطل زباله انتقال یابد؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { _service.DeleteNote(selected.Id); RefreshData(); _toast("یادداشت حذف شد."); } catch (Exception ex) { MessageBox.Show(UiMessages.Friendly(ex), "حذف نشد", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    }
}
