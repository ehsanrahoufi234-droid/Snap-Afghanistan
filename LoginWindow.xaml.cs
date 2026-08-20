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
                ModeText.Text = "برای نخستین ورود، حساب مدیر اصلی را بسازید";
                LoginButton.Content = "ایجاد حساب مدیر و ورود";
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

                var session = _auth.Authenticate(Username.Text, Password.Password);
                if (session.User.MustChangePassword)
                {
                    MessageBox.Show("این حساب با رمز موقت وارد شده است. برای امنیت، رمز را از بخش تنظیمات تغییر دهید.",
                        "رمز موقت", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                DialogResult = true;
            }
            catch (Exception exception)
            {
                SessionContext.End();
                ErrorText.Text = exception.Message;
                Password.SelectAll();
                Password.Focus();
            }
        }
    }
}
