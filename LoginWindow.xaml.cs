using System;
using System.Windows;
using System.Windows.Input;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _auth = new AuthService();
        private readonly bool _firstRun;

        public LoginWindow()
        {
            InitializeComponent();
            _firstRun = !_auth.IsConfigured();
            ConfirmPanel.Visibility = _firstRun ? Visibility.Visible : Visibility.Collapsed;
            Username.Text = _firstRun ? "admin" : _auth.Username;
            if (_firstRun)
            {
                ModeText.Text = "برای نخستین ورود، رمز مدیر را بسازید";
                LoginButton.Content = "ایجاد رمز و ورود";
            }
            Loaded += (sender, args) => Password.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e) => Submit();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Submit();
        }

        private void Submit()
        {
            try
            {
                ErrorText.Text = "";
                if (_firstRun)
                {
                    if (Password.Password != ConfirmPassword.Password) throw new InvalidOperationException("تکرار رمز با رمز اصلی یکسان نیست.");
                    _auth.SetPassword(Password.Password, Username.Text);
                }
                else if (!_auth.Verify(Username.Text, Password.Password))
                    throw new InvalidOperationException("نام کاربری یا رمز عبور نادرست است.");
                DialogResult = true;
            }
            catch (Exception exception)
            {
                ErrorText.Text = exception.Message;
                Password.SelectAll();
                Password.Focus();
            }
        }
    }
}
