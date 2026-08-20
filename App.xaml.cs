using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;
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
            if (!string.Equals(Database.IntegrityCheck(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Database integrity self-test failed.");

            var repository = new SnapRepository();
            var operations = new OperationsService();
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 10);

            var sector = new SectorItem { Name = "آزمایش-" + suffix, Description = "self-test", Status = "فعال" };
            repository.SaveSector(sector);

            var member = new MemberRecord
            {
                Type = "معلم",
                FirstName = "آزمایش",
                FatherName = "سیستم",
                TazkiraNo = "SELF-" + suffix,
                Phone = "0700000000",
                OriginalAddress = "هرات",
                CurrentAddress = "هرات",
                Institution = "Snap Self Test",
                Status = "فعال"
            };
            repository.SaveMember(member, "");

            var center = new CenterRecord
            {
                SectorId = sector.Id,
                LegalName = "مرکز آزمایشی " + suffix,
                TradeName = "Self Test",
                Phone = "0700000000",
                Address = "هرات",
                FeeBasis = "اشتراک ماهانه",
                Status = "فعال"
            };
            repository.SaveCenter(center);
            repository.ConfigureSubscription(center.Id, 100m, DateTime.Today, DateTime.Today.AddMonths(1), false);

            var receipt = operations.RegisterPayment(center.Id, 100m, DateTime.Today, 1, "TEST-" + suffix, "native self-test");
            var payment = repository.GetPayments(center.Id).FirstOrDefault(item => item.ReceiptNo == receipt);
            if (payment == null) throw new InvalidOperationException("Payment create self-test failed.");
            operations.UpdatePayment(payment, 125m, DateTime.Today, 1, receipt, "edited self-test");
            payment = repository.GetPayments(center.Id).FirstOrDefault(item => item.ReceiptNo == receipt);
            if (payment == null || payment.Amount != 125m) throw new InvalidOperationException("Payment edit self-test failed.");
            operations.DeletePayment(payment);
            if (repository.GetPayments(center.Id).Any(item => item.Id == payment.Id))
                throw new InvalidOperationException("Payment delete self-test failed.");
            operations.RegisterPayment(center.Id, 100m, DateTime.Today, 1, "TEST2-" + suffix, "native self-test");

            var note = new NoteItem
            {
                Title = "آزمایش سیستم " + suffix,
                Type = "عمومی",
                Priority = "عادی",
                Status = "باز",
                Body = "Native self-test"
            };
            repository.SaveNote(note);

            var dashboard = repository.GetDashboard();
            if (dashboard.ActiveMembers < 1 || dashboard.RegisteredCenters < 1)
                throw new InvalidOperationException("Repository CRUD self-test failed.");

            var memberTable = repository.BuildReport("members");
            var centerTable = repository.BuildReport("centers");
            var paymentTable = repository.BuildReport("payments");
            if (memberTable.Rows.Count < 1 || centerTable.Rows.Count < 1 || paymentTable.Rows.Count < 1)
                throw new InvalidOperationException("Report data self-test failed.");

            repository.ArchiveMember(member.Id);
            var archivedMember = operations.GetArchived("عضو").FirstOrDefault(item => item.EntityId == member.Id);
            if (archivedMember == null) throw new InvalidOperationException("Archive self-test failed.");
            operations.RestoreArchived(archivedMember);
            var restoredMember = repository.GetMember(member.Id);
            if (restoredMember == null || restoredMember.Status != "فعال") throw new InvalidOperationException("Archive restore self-test failed.");

            repository.DeleteMember(member.Id);
            var trashMember = repository.GetTrash().FirstOrDefault(item => item.EntityType == "عضو" && item.EntityId == member.Id);
            if (trashMember == null) throw new InvalidOperationException("Trash self-test failed.");
            repository.RestoreTrash(trashMember);
            if (repository.GetMember(member.Id) == null) throw new InvalidOperationException("Trash restore self-test failed.");

            var pdf = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-SelfTest-" + suffix + ".pdf");
            var backup = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-SelfTest-" + suffix + ".snapbackup");
            try
            {
                new ReportService().ExportTablePdf(memberTable, pdf, "آزمایش گزارش", "اسنپ افغانستان");
                if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0)
                    throw new InvalidOperationException("PDF self-test failed.");

                new BackupService().CreateBackup(backup);
                if (!File.Exists(backup) || new FileInfo(backup).Length == 0)
                    throw new InvalidOperationException("Backup self-test failed.");
            }
            finally
            {
                try { if (File.Exists(pdf)) File.Delete(pdf); } catch { }
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            }
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