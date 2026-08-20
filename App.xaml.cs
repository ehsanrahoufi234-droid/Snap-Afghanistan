using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native
{
    public partial class App : Application
    {
        private Mutex? _singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += HandleUnhandledException;
            try
            {
                bool created;
                _singleInstance = new Mutex(true, "Local\\SnapAfghanistan.Desktop.1", out created);
                if (!created)
                {
                    MessageBox.Show("اسنپ افغانستان از قبل باز است.", "برنامه باز است", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
                Database.Initialize();
                if (Array.Exists(e.Args, value => string.Equals(value, "--self-test", StringComparison.OrdinalIgnoreCase)))
                {
                    RunSelfTest();
                    Shutdown(0);
                    return;
                }
                var login = new LoginWindow();
                if (login.ShowDialog() == true)
                {
                    var main = new MainWindow();
                    MainWindow = main;
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    main.Show();
                }
                else Shutdown();
            }
            catch (Exception exception)
            {
                Database.LogError(exception);
                MessageBox.Show("برنامه نتوانست اطلاعات محلی را باز کند.\n\n" + exception.Message,
                    "خطای راه‌اندازی", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _singleInstance?.ReleaseMutex(); _singleInstance?.Dispose(); } catch { }
            base.OnExit(e);
        }

        private static void RunSelfTest()
        {
            var repository = new SnapRepository();
            repository.GetDashboard();
            var table = repository.BuildReport("members");
            var pdf = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-SelfTest.pdf");
            new ReportService().ExportTablePdf(table, pdf, "آزمایش گزارش", "اسنپ افغانستان");
            if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0) throw new InvalidOperationException("PDF self-test failed.");
            File.Delete(pdf);
        }

        private static void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Database.LogError(e.Exception);
            MessageBox.Show("عملیات کامل نشد؛ هیچ اطلاعاتی پاک نشده است.\n\n" + e.Exception.Message,
                "خطای برنامه", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
