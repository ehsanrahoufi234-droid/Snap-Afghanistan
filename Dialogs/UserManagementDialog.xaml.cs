using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class UserManagementDialog : Window
    {
        public sealed class RoleChoice { public string Key { get; set; } = "employee"; public string Name { get; set; } = "کارمند"; }
        public sealed class PermissionRow { public string Key { get; set; } = ""; public string Title { get; set; } = ""; public bool Allowed { get; set; } }
        private readonly ISnapService _service;
        private UserAccount? _selected;
        private List<PermissionRow> _permissions = new List<PermissionRow>();

        public UserManagementDialog(ISnapService service)
        {
            InitializeComponent();
            _service = service;
            RoleCombo.ItemsSource = new[]
            {
                new RoleChoice { Key="admin",Name="مدیر" }, new RoleChoice { Key="accountant",Name="حسابدار" }, new RoleChoice { Key="employee",Name="کارمند" }
            };
            NewMode();
            RefreshUsers();
        }

        private void RefreshUsers()
        {
            UsersGrid.ItemsSource = _service.GetUsers();
        }

        private void New_Click(object sender, RoutedEventArgs e) => NewMode();
        private void NewMode()
        {
            _selected = null; EditorTitle.Text = "کاربر جدید"; DisplayNameText.Text = ""; UsernameText.Text = ""; UsernameText.IsReadOnly = false;
            RoleCombo.SelectedValue = "employee"; ActiveCheck.IsChecked = true; PasswordText.Clear(); ResetPasswordButton.IsEnabled = false;
            PermissionList.ItemsSource = null; ErrorText.Text = "";
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = UsersGrid.SelectedItem as UserAccount;
            if (_selected == null) return;
            EditorTitle.Text = "ویرایش «" + _selected.DisplayName + "»"; DisplayNameText.Text = _selected.DisplayName; UsernameText.Text = _selected.Username; UsernameText.IsReadOnly = true;
            RoleCombo.SelectedValue = _selected.Role; ActiveCheck.IsChecked = _selected.IsActive; PasswordText.Clear(); ResetPasswordButton.IsEnabled = true; ErrorText.Text = "";
            LoadPermissions();
        }

        private void LoadPermissions()
        {
            if (_selected == null) { PermissionList.ItemsSource = null; return; }
            var effective = _service.GetEffectivePermissions(_selected.Id);
            _permissions = PermissionCatalog.All.Select(key => new PermissionRow { Key = key, Title = PermissionCatalog.PermissionTitle(key), Allowed = effective.ContainsKey(key) && effective[key] }).ToList();
            PermissionList.ItemsSource = _permissions;
        }

        private void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Text = "";
                var role = Convert.ToString(RoleCombo.SelectedValue) ?? "employee";
                if (_selected == null)
                {
                    var created = _service.CreateUser(UsernameText.Text, DisplayNameText.Text, role, PasswordText.Password);
                    RefreshUsers();
                    SelectUser(created.Id);
                }
                else
                {
                    _service.UpdateUser(_selected.Id, DisplayNameText.Text, role, ActiveCheck.IsChecked == true);
                    RefreshUsers(); SelectUser(_selected.Id);
                }
            }
            catch (Exception ex) { ErrorText.Text = ex.Message; }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try { ErrorText.Text = ""; _service.ResetPassword(_selected.Id, PasswordText.Password); PasswordText.Clear(); MessageBox.Show("رمز موقت تنظیم شد. کاربر در ورود بعدی باید آن را تغییر دهد.", "رمز موقت", MessageBoxButton.OK, MessageBoxImage.Information); RefreshUsers(); SelectUser(_selected.Id); }
            catch (Exception ex) { ErrorText.Text = ex.Message; }
        }

        private void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { ErrorText.Text = "ابتدا یک کاربر را انتخاب کنید."; return; }
            try
            {
                ErrorText.Text = "";
                foreach (var row in _permissions) _service.SetPermissionOverride(_selected.Id, row.Key, row.Allowed);
                MessageBox.Show("صلاحیت‌ها ذخیره شد و از ورود بعدی کاربر اعمال می‌شوند.", "ذخیره شد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ErrorText.Text = ex.Message; }
        }

        private void ResetPermissions_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try { _service.ClearPermissionOverrides(_selected.Id); LoadPermissions(); }
            catch (Exception ex) { ErrorText.Text = ex.Message; }
        }

        private void SelectUser(string id)
        {
            foreach (var item in UsersGrid.Items)
            {
                var user = item as UserAccount;
                if (user != null && string.Equals(user.Id, id, StringComparison.OrdinalIgnoreCase)) { UsersGrid.SelectedItem = user; UsersGrid.ScrollIntoView(user); break; }
            }
        }
    }
}
