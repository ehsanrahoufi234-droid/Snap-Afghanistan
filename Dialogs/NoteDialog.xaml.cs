using System;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class NoteDialog : Window
    {
        private readonly NoteItem _note; public NoteItem Result => _note;
        public NoteDialog(NoteItem? note = null)
        {
            InitializeComponent(); _note = note ?? new NoteItem(); TypeCombo.ItemsSource = new[] { "عمومی", "تماس", "قرارداد", "اشتراک", "پیگیری" }; PriorityCombo.ItemsSource = new[] { "عادی", "مهم", "فوری" }; StatusCombo.ItemsSource = new[] { "باز", "در حال انجام", "انجام شد" }; LoadNote();
        }
        private void LoadNote() { HeaderTitle.Text = string.IsNullOrWhiteSpace(_note.Id) ? "یادداشت جدید" : "ویرایش یادداشت"; TitleText.Text = _note.Title; TypeCombo.SelectedItem = _note.Type; PriorityCombo.SelectedItem = _note.Priority; RelatedText.Text = _note.RelatedName; StatusCombo.SelectedItem = _note.Status; BodyText.Text = _note.Body; DateTime date; if (DateService.TryParseIso(_note.DueDate, out date)) DueDate.SelectedDate = date; if (TypeCombo.SelectedIndex < 0) TypeCombo.SelectedItem = "عمومی"; if (PriorityCombo.SelectedIndex < 0) PriorityCombo.SelectedItem = "عادی"; if (StatusCombo.SelectedIndex < 0) StatusCombo.SelectedItem = "باز"; }
        private void Date_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { DueSolar.Text = DueDate.SelectedDate.HasValue ? "هجری شمسی: " + DateService.Solar(DueDate.SelectedDate.Value) : ""; }
        private void Save_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(TitleText.Text) || string.IsNullOrWhiteSpace(BodyText.Text)) { ErrorText.Text = "عنوان و متن یادداشت ضروری است."; return; } _note.Title = TitleText.Text.Trim(); _note.Type = Convert.ToString(TypeCombo.SelectedItem) ?? "عمومی"; _note.Priority = Convert.ToString(PriorityCombo.SelectedItem) ?? "عادی"; _note.RelatedName = RelatedText.Text.Trim(); _note.Status = Convert.ToString(StatusCombo.SelectedItem) ?? "باز"; _note.Body = BodyText.Text.Trim(); _note.DueDate = DueDate.SelectedDate.HasValue ? DateService.Iso(DueDate.SelectedDate.Value) : ""; DialogResult = true; }
    }
}
