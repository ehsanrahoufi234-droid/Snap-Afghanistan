using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class ArchiveDialog : Window
    {
        private readonly OperationsService _operations = new OperationsService();
        private readonly string _initialType;
        public bool Changed { get; private set; }

        public ArchiveDialog(string initialType = "همه")
        {
            InitializeComponent();
            _initialType = string.IsNullOrWhiteSpace(initialType) ? "همه" : initialType;
            TypeFilter.ItemsSource = new[] { "همه", "عضو", "مرکز", "سکتور" };
            TypeFilter.SelectedItem = _initialType;
            Loaded += (s, e) => Refresh();
        }

        private ArchivedItem? Selected() => Grid.SelectedItem as ArchivedItem;

        private void Refresh()
        {
            var type = Convert.ToString(TypeFilter.SelectedItem) ?? _initialType;
            var items = _operations.GetArchived(type);
            Grid.ItemsSource = items;
            CountText.Text = items.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " مورد بایگانی‌شده";
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) Refresh();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected();
            if (item == null) { MessageBox.Show("ابتدا یک مورد را انتخاب کنید.", "بایگانی", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            try
            {
                _operations.RestoreArchived(item);
                Changed = true;
                Refresh();
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "بازگردانی نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected();
            if (item == null) { MessageBox.Show("ابتدا یک مورد را انتخاب کنید.", "بایگانی", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (MessageBox.Show("«" + item.Title + "» از بایگانی به سطل زباله منتقل شود؟", "تأیید", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                _operations.MoveArchivedToTrash(item);
                Changed = true;
                Refresh();
            }
            catch (Exception exception) { MessageBox.Show(UiMessages.Friendly(exception), "انتقال انجام نشد", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}