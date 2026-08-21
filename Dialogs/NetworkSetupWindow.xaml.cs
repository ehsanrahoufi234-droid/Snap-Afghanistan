using System;
using System.Windows;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native.Dialogs
{
    public partial class NetworkSetupWindow : Window
    {
        public NetworkConfig Result { get; private set; } = new NetworkConfig();

        public NetworkSetupWindow(NetworkConfig? existing = null)
        {
            InitializeComponent();
            var config = existing ?? new NetworkConfig();
            ServerAddress.Text = NetworkConfigurationService.SuggestedServerAddress();
            ServerSecret.Text = config.IsServer && !string.IsNullOrWhiteSpace(config.Secret) ? config.Secret : NetworkConfigurationService.GenerateSecret();
            ClientHost.Text = config.IsClient && !string.IsNullOrWhiteSpace(config.Host) ? config.Host : "192.168.1.2";
            ClientSecret.Text = config.IsClient ? config.Secret : "";
            if (config.IsClient) ClientMode.IsChecked = true; else ServerMode.IsChecked = true;
            UpdateMode();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e) => UpdateMode();
        private void UpdateMode()
        {
            if (ServerPanel == null || ClientPanel == null) return;
            var server = ServerMode.IsChecked == true;
            ServerPanel.IsEnabled = server;
            ClientPanel.IsEnabled = !server;
            TestButton.IsEnabled = !server;
        }

        private NetworkConfig Build()
        {
            if (ServerMode.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(ServerSecret.Text)) throw new InvalidOperationException("کُد اتصال ساخته نشده است.");
                return new NetworkConfig { Mode = "server", Host = "0.0.0.0", Port = 47821, Secret = ServerSecret.Text.Trim() };
            }
            if (string.IsNullOrWhiteSpace(ClientHost.Text)) throw new InvalidOperationException("IP کمپیوتر اصلی را وارد کنید.");
            if (string.IsNullOrWhiteSpace(ClientSecret.Text)) throw new InvalidOperationException("کُد اتصال امن را وارد کنید.");
            return new NetworkConfig { Mode = "client", Host = ClientHost.Text.Trim(), Port = 47821, Secret = ClientSecret.Text.Trim() };
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Text = "در حال بررسی اتصال...";
                var config = Build();
                var remote = new RemoteSnapService(config);
                if (!remote.Ping()) throw new InvalidOperationException("پاسخی از کمپیوتر اصلی دریافت نشد.");
                ErrorText.Text = "✓ اتصال امن با کمپیوتر اصلی برقرار شد.";
            }
            catch (Exception exception) { ErrorText.Text = exception.Message; }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Text = "";
                Result = Build();
                if (Result.IsClient)
                {
                    var remote = new RemoteSnapService(Result);
                    if (!remote.Ping()) throw new InvalidOperationException("پیش از ادامه، کمپیوتر اصلی را روشن کنید و اتصال شبکه را بررسی کنید.");
                }
                DialogResult = true;
            }
            catch (Exception exception) { ErrorText.Text = exception.Message; }
        }
    }
}
