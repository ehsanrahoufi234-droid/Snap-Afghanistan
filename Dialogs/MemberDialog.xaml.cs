using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class MemberDialog : Window
    {
        private readonly MemberRecord _record;
        private readonly bool _readOnly;
        public MemberRecord Result => _record;
        public string NewAttachmentPath { get; private set; } = "";

        public MemberDialog(MemberRecord? record = null, bool readOnly = false)
        {
            InitializeComponent();
            _record = record ?? new MemberRecord();
            _readOnly = readOnly;
            TypeCombo.ItemsSource = SnapRepository.MemberTypes;
            StatusCombo.ItemsSource = new[] { "فعال", "غیرفعال", "بایگانی" };
            LoadRecord();
            if (_readOnly) SetReadOnly();
        }

        private void LoadRecord()
        {
            HeaderTitle.Text = _readOnly ? "مشاهده پرونده عضو" : string.IsNullOrWhiteSpace(_record.Id) ? "ثبت عضو جدید" : "ویرایش پرونده عضو";
            CodeText.Text = string.IsNullOrWhiteSpace(_record.Code) ? "پس از ذخیره ساخته می‌شود" : _record.Code;
            TypeCombo.SelectedItem = _record.Type; NameText.Text = _record.FirstName; FatherText.Text = _record.FatherName;
            TazkiraText.Text = _record.TazkiraNo; PhoneText.Text = _record.Phone; OriginalAddressText.Text = _record.OriginalAddress;
            CurrentAddressText.Text = _record.CurrentAddress; InstitutionText.Text = _record.Institution;
            StatusCombo.SelectedItem = string.IsNullOrWhiteSpace(_record.Status) ? "فعال" : _record.Status;
            NotesText.Text = _record.Notes; AttachmentText.Text = _record.AttachmentName;
            OpenAttachmentButton.IsEnabled = File.Exists(_record.AttachmentPath);
        }

        private void SetReadOnly()
        {
            TypeCombo.IsEnabled = false; NameText.IsReadOnly = true; FatherText.IsReadOnly = true; TazkiraText.IsReadOnly = true;
            PhoneText.IsReadOnly = true; OriginalAddressText.IsReadOnly = true; CurrentAddressText.IsReadOnly = true;
            InstitutionText.IsReadOnly = true; StatusCombo.IsEnabled = false; NotesText.IsReadOnly = true;
            ChooseAttachmentButton.Visibility = Visibility.Collapsed; SaveButton.Visibility = Visibility.Collapsed;
        }

        private void ChooseAttachment_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "انتخاب فایل تذکره", Filter = "تذکره|*.jpg;*.jpeg;*.png;*.pdf|همه فایل‌ها|*.*" };
            if (dialog.ShowDialog(this) == true)
            {
                NewAttachmentPath = dialog.FileName;
                AttachmentText.Text = Path.GetFileName(dialog.FileName);
                OpenAttachmentButton.IsEnabled = true;
            }
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            var path = !string.IsNullOrWhiteSpace(NewAttachmentPath) ? NewAttachmentPath : _record.AttachmentPath;
            if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Text = "";
                _record.Type = Convert.ToString(TypeCombo.SelectedItem) ?? "معلم"; _record.FirstName = NameText.Text.Trim();
                _record.FatherName = FatherText.Text.Trim(); _record.TazkiraNo = TazkiraText.Text.Trim(); _record.Phone = PhoneText.Text.Trim();
                _record.OriginalAddress = OriginalAddressText.Text.Trim(); _record.CurrentAddress = CurrentAddressText.Text.Trim();
                _record.Institution = InstitutionText.Text.Trim(); _record.Status = Convert.ToString(StatusCombo.SelectedItem) ?? "فعال"; _record.Notes = NotesText.Text.Trim();
                if (string.IsNullOrWhiteSpace(_record.FirstName) || string.IsNullOrWhiteSpace(_record.FatherName) || string.IsNullOrWhiteSpace(_record.TazkiraNo) || string.IsNullOrWhiteSpace(_record.OriginalAddress) || string.IsNullOrWhiteSpace(_record.CurrentAddress))
                    throw new InvalidOperationException("خانه‌های ستاره‌دار باید تکمیل شوند.");
                DialogResult = true;
            }
            catch (Exception exception) { ErrorText.Text = exception.Message; }
        }
    }
}
