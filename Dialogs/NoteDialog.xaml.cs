using System;
using System.Windows;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class NoteDialog : Window
    {
        private readonly NoteItem _note;
        public NoteItem Result => _note;

        public NoteDialog(NoteItem? note = null)
        {
            InitializeComponent();
            _note = note ?? new NoteItem();
            TypeCombo.ItemsSource = new[] { "عمومی", "تماس", "قرارداد", "اشتراک", "پیگیری" };
            PriorityCombo.ItemsSource = new[] { "عادی", "مهم", "فوری" };
            StatusCombo.ItemsSource = new[] { "باز", "در حال انجام", "انجام شد" };
            LoadNote();
        }

        private void LoadNote()
        {
            HeaderTitle.Text = string.IsNullOrWhiteSpace(_note.Id) ? "یادداشت جدید" : "ویرایش یادداشت";
            TitleText.Text = _note.Title;
            TypeCombo.SelectedItem = _note.Type;
            PriorityCombo.SelectedItem = _note.Priority;
            RelatedText.Text = _note.RelatedName;
            StatusCombo.SelectedItem = _note.Status;
            BodyText.Text = _note.Body;
            DateTime date;
            if (DateService.TryParseIso(_note.DueDate, out date))
            {
                DueDateText.Text = DateService.Solar(date);
                DueGregorian.Text = "معادل میلادی: " + DateService.Gregorian(date);
            }
            if (TypeCombo.SelectedIndex < 0) TypeCombo.SelectedItem = "عمومی";
            if (PriorityCombo.SelectedIndex < 0) PriorityCombo.SelectedItem = "عادی";
            if (StatusCombo.SelectedIndex < 0) StatusCombo.SelectedItem = "باز";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleText.Text) || string.IsNullOrWhiteSpace(BodyText.Text))
            {
                ErrorText.Text = "عنوان و متن یادداشت ضروری است.";
                return;
            }
            _note.Title = TitleText.Text.Trim();
            _note.Type = Convert.ToString(TypeCombo.SelectedItem) ?? "عمومی";
            _note.Priority = Convert.ToString(PriorityCombo.SelectedItem) ?? "عادی";
            _note.RelatedName = RelatedText.Text.Trim();
            _note.Status = Convert.ToString(StatusCombo.SelectedItem) ?? "باز";
            _note.Body = BodyText.Text.Trim();
            if (string.IsNullOrWhiteSpace(DueDateText.Text)) _note.DueDate = "";
            else
            {
                DateTime date;
                if (!DateService.TryParseSolar(DueDateText.Text, out date))
                {
                    ErrorText.Text = "موعد هجری شمسی معتبر نیست. نمونه: 1405/06/10";
                    return;
                }
                _note.DueDate = DateService.Iso(date);
            }
            DialogResult = true;
        }
    }
}