using System;
using System.Windows;
using System.Windows.Input;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native
{
    public partial class LoginWindow : Window
    {
        private readonly ISnapService _service = SnapServices.Current;
        private readonly bool _firstRun;

        public LoginWindow()
        {
            InitializeComponent();
            _firstRun = !_service.IsConfigured();
            ConfirmPanel.Visibility = _firstRun ? Visibility.Visible : Visibility.Collapsed;
            Username.Text = _firstRun ? "admin" : _service.SuggestedUsername;
            if (_firstRun)
            {
                ModeText.Text = _service.IsRemote ? "ابتدا حساب مدیر را روی کمپیوتر اصلی بسازید" : "برای نخستین ورود، حساب مدیر اصلی را بسازید";
                LoginButton.Content = "ایجاد حساب مدیر و ورود";
            }
            else ModeText.Text = _service.IsRemote ? "ورود امن از طریق شبکه داخلی دفتر" : "ورود امن به کمپیوتر اصلی";
            Loaded += (sender, args) => Password.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e) => Submit();
        private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Submit(); }

        private void Submit()
        {
            try
            {
                ErrorText.Text = "";
                if (_firstRun)
                {
                    if (_service.IsRemote) throw new InvalidOperationException("حساب مدیر هنوز روی کمپیوتر اصلی ساخته نشده است. ابتدا برنامه را روی Server باز و مدیر را ایجاد کنید.");
                    if (Password.Password != ConfirmPassword.Password) throw new InvalidOperationException("تکرار رمز با رمز اصلی یکسان نیست.");
                    _service.CreateFirstAdministrator(Username.Text, Password.Password);
                }

                var session = _service.Authenticate(Username.Text, Password.Password);
                if (session.User.MustChangePassword)
                {
                    MessageBox.Show("این حساب با رمز موقت وارد شده است. برای امنیت، رمز را از بخش تنظیمات تغییر دهید.", "رمز موقت", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                DialogResult = true;
            }
            catch (Exception exception)
            {
                SessionContext.End();
                ErrorText.Text = exception.Message;
                Password.SelectAll(); Password.Focus();
            }
        }
    }
}
