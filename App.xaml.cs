using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Dialogs;
using SnapAfghanistan.Native.Models;
using SnapAfghanistan.Native.Services;

namespace SnapAfghanistan.Native
{
    public partial class App : Application
    {
        private Mutex? _singleInstance;
        private LanServerHost? _server;

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
                    Shutdown(); return;
                }

                if (Array.Exists(e.Args, value => string.Equals(value, "--self-test", StringComparison.OrdinalIgnoreCase)))
                {
                    Database.Initialize();
                    var localTest = new LocalSnapService();
                    SnapServices.Configure(localTest);
                    RunSelfTest(localTest);
                    Shutdown(0); return;
                }

                var config = NetworkConfigurationService.Load();
                if (!config.IsValid)
                {
                    var setup = new NetworkSetupWindow();
                    if (setup.ShowDialog() != true) { Shutdown(); return; }
                    config = setup.Result; NetworkConfigurationService.Save(config);
                }

                if (config.IsServer)
                {
                    Database.Initialize();
                    var local = new LocalSnapService();
                    SnapServices.Configure(local);
                    _server = new LanServerHost(config, local);
                    _server.Start();
                }
                else
                {
                    var remote = new RemoteSnapService(config);
                    if (!remote.Ping()) throw new InvalidOperationException("کامپیوتر اصلی در شبکه در دسترس نیست. Server را روشن کنید یا تنظیم شبکه را اصلاح کنید.");
                    SnapServices.Configure(remote);
                }

                var login = new LoginWindow();
                if (login.ShowDialog() == true)
                {
                    var main = new MainWindow(); MainWindow = main; ShutdownMode = ShutdownMode.OnMainWindowClose; main.Show();
                }
                else Shutdown();
            }
            catch (Exception exception)
            {
                try { Database.LogError(exception); } catch { }
                MessageBox.Show("برنامه نتوانست راه‌اندازی شود.\n\n" + exception.Message, "خطای راه‌اندازی", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _server?.Dispose(); } catch { }
            SessionContext.End();
            try { _singleInstance?.ReleaseMutex(); _singleInstance?.Dispose(); } catch { }
            base.OnExit(e);
        }

        private static void RunSelfTest(LocalSnapService service)
        {
            if (!string.Equals(Database.IntegrityCheck(), "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Database integrity self-test failed.");
            const string adminPassword = "SnapTest!12345";
            if (!service.IsConfigured()) service.CreateFirstAdministrator("selfadmin", adminPassword);
            service.Authenticate("selfadmin", adminPassword);

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
            var sector = new SectorItem { Name = "آزمایش-" + suffix, Description = "self-test", Status = "فعال" };
            service.SaveSector(sector);

            var member = new MemberRecord { Type = "معلم", FirstName = "آزمایش", FatherName = "سیستم", TazkiraNo = "SELF-" + suffix, Phone = "0700000000", OriginalAddress = "هرات", CurrentAddress = "هرات", Institution = "Snap Self Test", Status = "فعال" };
            service.SaveMember(member, "");

            var center = new CenterRecord { SectorId = sector.Id, LegalName = "مرکز آزمایشی " + suffix, TradeName = "Self Test", Phone = "0700000000", Address = "هرات", FeeBasis = "اشتراک ماهانه", Status = "فعال" };
            service.SaveCenter(center);
            service.ConfigureSubscription(center.Id, 100m, DateTime.Today, DateTime.Today.AddMonths(1), false);

            var receipt = service.RegisterPayment(center.Id, 100m, DateTime.Today, 1, "TEST-" + suffix, "native self-test");
            var payment = service.GetPayments(center.Id).FirstOrDefault(item => item.ReceiptNo == receipt);
            if (payment == null) throw new InvalidOperationException("Payment create self-test failed.");
            service.UpdatePayment(payment, 125m, DateTime.Today, 1, receipt, "edited self-test");
            payment = service.GetPayments(center.Id).FirstOrDefault(item => item.ReceiptNo == receipt);
            if (payment == null || payment.Amount != 125m) throw new InvalidOperationException("Payment edit self-test failed.");
            service.DeletePayment(payment);
            if (service.GetPayments(center.Id).Any(item => item.Id == payment.Id)) throw new InvalidOperationException("Payment delete self-test failed.");
            service.RegisterPayment(center.Id, 100m, DateTime.Today, 1, "TEST2-" + suffix, "native self-test");

            var note = new NoteItem { Title = "آزمایش سیستم " + suffix, Type = "عمومی", Priority = "عادی", Status = "باز", Body = "Native self-test" };
            service.SaveNote(note);

            var accountant = service.CreateUser("acct" + suffix.Substring(0, 4), "حسابدار آزمایشی", "accountant", "Account!123");
            if (accountant.Role != "accountant") throw new InvalidOperationException("User/role self-test failed.");

            var dashboard = service.GetDashboard();
            if (dashboard.ActiveMembers < 1 || dashboard.RegisteredCenters < 1) throw new InvalidOperationException("Repository CRUD self-test failed.");

            var memberTable = service.BuildReport("members"); var centerTable = service.BuildReport("centers"); var paymentTable = service.BuildReport("payments");
            if (memberTable.Rows.Count < 1 || centerTable.Rows.Count < 1 || paymentTable.Rows.Count < 1) throw new InvalidOperationException("Report data self-test failed.");

            service.ArchiveMember(member.Id);
            var archivedMember = service.GetArchived("عضو").FirstOrDefault(item => item.EntityId == member.Id);
            if (archivedMember == null) throw new InvalidOperationException("Archive self-test failed.");
            service.RestoreArchived(archivedMember);
            var restoredMember = service.GetMember(member.Id);
            if (restoredMember == null || restoredMember.Status != "فعال") throw new InvalidOperationException("Archive restore self-test failed.");

            service.DeleteMember(member.Id);
            var trashMember = service.GetTrash().FirstOrDefault(item => item.EntityType == "عضو" && item.EntityId == member.Id);
            if (trashMember == null) throw new InvalidOperationException("Trash self-test failed.");
            service.RestoreTrash(trashMember);
            if (service.GetMember(member.Id) == null) throw new InvalidOperationException("Trash restore self-test failed.");

            var pdf = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-SelfTest-" + suffix + ".pdf");
            var backup = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-SelfTest-" + suffix + ".snapbackup");
            try
            {
                new ReportService().ExportTablePdf(memberTable, pdf, "آزمایش گزارش", "اسنپ افغانستان");
                if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0) throw new InvalidOperationException("PDF self-test failed.");
                new BackupService().CreateBackup(backup);
                if (!File.Exists(backup) || new FileInfo(backup).Length == 0) throw new InvalidOperationException("Backup self-test failed.");

                var lanConfig = new NetworkConfig { Mode = "server", Host = "0.0.0.0", Port = 47991, Secret = NetworkConfigurationService.GenerateSecret() };
                using (var host = new LanServerHost(lanConfig, service))
                {
                    host.Start(); Thread.Sleep(100);
                    var remote = new RemoteSnapService(new NetworkConfig { Mode = "client", Host = "127.0.0.1", Port = 47991, Secret = lanConfig.Secret });
                    if (!remote.Ping()) throw new InvalidOperationException("LAN ping self-test failed.");
                    remote.Authenticate("selfadmin", adminPassword);
                    if (remote.GetDashboard().RegisteredCenters < 1) throw new InvalidOperationException("LAN authenticated RPC self-test failed.");
                }
            }
            finally
            {
                try { if (File.Exists(pdf)) File.Delete(pdf); } catch { }
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            }
        }

        private static void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try { Database.LogError(e.Exception); } catch { }
            MessageBox.Show("عملیات کامل نشد؛ اطلاعات نیمه‌کاره ثبت نشده است.\n\n" + e.Exception.Message, "خطای برنامه", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
