using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class OperationsService
    {
        public string RegisterPayment(string centerId, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes)
        {
            ValidatePayment(amount, coveredMonths);
            if (string.IsNullOrWhiteSpace(receiptNo)) receiptNo = GenerateReceiptNo();
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                EnsureReceiptAvailable(connection, transaction, receiptNo, "");
                var previousDue = GetCenterDue(connection, transaction, centerId);
                DateTime baseDate;
                if (!DateService.TryParseIso(previousDue, out baseDate)) throw new InvalidOperationException("ابتدا سررسید اشتراک مرکز را تنظیم کنید.");
                var newDue = baseDate.AddMonths(coveredMonths);
                var id = Guid.NewGuid().ToString("N");
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO subscription_payments(id,partner_id,payment_date,amount,receipt_no,covered_months,previous_due_date,new_due_date,notes)
VALUES(@id,@partner,@date,@amount,@receipt,@months,@previous,@newDue,@notes)";
                    command.Parameters.AddWithValue("@id", id);
                    command.Parameters.AddWithValue("@partner", centerId);
                    command.Parameters.AddWithValue("@date", DateService.Iso(paymentDate));
                    command.Parameters.AddWithValue("@amount", amount);
                    command.Parameters.AddWithValue("@receipt", receiptNo.Trim());
                    command.Parameters.AddWithValue("@months", coveredMonths);
                    command.Parameters.AddWithValue("@previous", previousDue);
                    command.Parameters.AddWithValue("@newDue", DateService.Iso(newDue));
                    command.Parameters.AddWithValue("@notes", notes?.Trim() ?? "");
                    command.ExecuteNonQuery();
                }
                SetCenterDue(connection, transaction, centerId, DateService.Iso(newDue));
                Audit(connection, transaction, "payment", id, "create", receiptNo + " - " + amount.ToString("0", CultureInfo.InvariantCulture));
                transaction.Commit();
                return receiptNo;
            }
        }

        public void UpdatePayment(PaymentItem payment, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes)
        {
            if (payment == null || string.IsNullOrWhiteSpace(payment.Id)) throw new InvalidOperationException("پرداخت انتخاب نشده است.");
            ValidatePayment(amount, coveredMonths);
            if (string.IsNullOrWhiteSpace(receiptNo)) receiptNo = payment.ReceiptNo;
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                var baseline = GetPaymentBaseline(connection, transaction, payment.CenterId);
                EnsureReceiptAvailable(connection, transaction, receiptNo, payment.Id);
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"UPDATE subscription_payments SET payment_date=@date,amount=@amount,receipt_no=@receipt,covered_months=@months,notes=@notes WHERE id=@id";
                    command.Parameters.AddWithValue("@date", DateService.Iso(paymentDate));
                    command.Parameters.AddWithValue("@amount", amount);
                    command.Parameters.AddWithValue("@receipt", receiptNo.Trim());
                    command.Parameters.AddWithValue("@months", coveredMonths);
                    command.Parameters.AddWithValue("@notes", notes?.Trim() ?? "");
                    command.Parameters.AddWithValue("@id", payment.Id);
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("پرداخت پیدا نشد.");
                }
                RebuildPaymentChain(connection, transaction, payment.CenterId, baseline);
                Audit(connection, transaction, "payment", payment.Id, "update", receiptNo + " - " + amount.ToString("0", CultureInfo.InvariantCulture));
                transaction.Commit();
            }
        }

        public void DeletePayment(PaymentItem payment)
        {
            if (payment == null || string.IsNullOrWhiteSpace(payment.Id)) throw new InvalidOperationException("پرداخت انتخاب نشده است.");
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                var baseline = GetPaymentBaseline(connection, transaction, payment.CenterId);
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM subscription_payments WHERE id=@id";
                    command.Parameters.AddWithValue("@id", payment.Id);
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("پرداخت پیدا نشد.");
                }
                RebuildPaymentChain(connection, transaction, payment.CenterId, baseline);
                Audit(connection, transaction, "payment", payment.Id, "delete", payment.ReceiptNo);
                transaction.Commit();
            }
        }

        public IReadOnlyList<ArchivedItem> GetArchived(string entityType = "همه")
        {
            var result = new List<ArchivedItem>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                var sql = new List<string>();
                if (entityType == "همه" || entityType == "عضو") sql.Add("SELECT 'عضو',id,first_name,member_code,COALESCE(archived_at,updated_at) FROM members WHERE deleted_at IS NULL AND status='بایگانی'");
                if (entityType == "همه" || entityType == "مرکز") sql.Add("SELECT 'مرکز',id,legal_name,partner_code,COALESCE(archived_at,updated_at) FROM partners WHERE deleted_at IS NULL AND status='بایگانی'");
                if (entityType == "همه" || entityType == "سکتور") sql.Add("SELECT 'سکتور',id,name,description,updated_at FROM sectors WHERE deleted_at IS NULL AND status='بایگانی'");
                if (sql.Count == 0) return result;
                command.CommandText = string.Join(" UNION ALL ", sql) + " ORDER BY 5 DESC";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new ArchivedItem
                    {
                        EntityType = Text(reader, 0), EntityId = Text(reader, 1), Title = Text(reader, 2), Detail = Text(reader, 3), ArchivedAt = Text(reader, 4)
                    });
                }
            }
            return result;
        }

        public long CountArchived(string entityType)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                switch (entityType)
                {
                    case "عضو": command.CommandText = "SELECT COUNT(*) FROM members WHERE deleted_at IS NULL AND status='بایگانی'"; break;
                    case "مرکز": command.CommandText = "SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND status='بایگانی'"; break;
                    case "سکتور": command.CommandText = "SELECT COUNT(*) FROM sectors WHERE deleted_at IS NULL AND status='بایگانی'"; break;
                    default: command.CommandText = @"SELECT
(SELECT COUNT(*) FROM members WHERE deleted_at IS NULL AND status='بایگانی')+
(SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND status='بایگانی')+
(SELECT COUNT(*) FROM sectors WHERE deleted_at IS NULL AND status='بایگانی')"; break;
                }
                return Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
            }
        }

        public void ArchiveSector(string id)
        {
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                using (var check = connection.CreateCommand())
                {
                    check.Transaction = transaction;
                    check.CommandText = "SELECT COUNT(*) FROM partners WHERE sector_id=@id AND deleted_at IS NULL AND status!='بایگانی'";
                    check.Parameters.AddWithValue("@id", id);
                    if (Convert.ToInt32(check.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0)
                        throw new InvalidOperationException("این سکتور مرکز فعال دارد؛ ابتدا مراکز آن را انتقال یا بایگانی کنید.");
                }
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE sectors SET status='بایگانی',updated_at=CURRENT_TIMESTAMP WHERE id=@id AND deleted_at IS NULL";
                    command.Parameters.AddWithValue("@id", id);
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("سکتور پیدا نشد.");
                }
                Audit(connection, transaction, "sector", id, "archive", "");
                transaction.Commit();
            }
        }

        public void RestoreArchived(ArchivedItem item)
        {
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                if (item.EntityType == "عضو") command.CommandText = "UPDATE members SET status='فعال',archived_at=NULL,updated_at=CURRENT_TIMESTAMP WHERE id=@id AND deleted_at IS NULL";
                else if (item.EntityType == "مرکز") command.CommandText = "UPDATE partners SET status='فعال',archived_at=NULL,updated_at=CURRENT_TIMESTAMP WHERE id=@id AND deleted_at IS NULL";
                else if (item.EntityType == "سکتور") command.CommandText = "UPDATE sectors SET status='فعال',updated_at=CURRENT_TIMESTAMP WHERE id=@id AND deleted_at IS NULL";
                else throw new InvalidOperationException("نوع بایگانی نامعتبر است.");
                command.Parameters.AddWithValue("@id", item.EntityId);
                command.ExecuteNonQuery();
                Audit(connection, transaction, item.EntityType, item.EntityId, "restore-archive", item.Title);
                transaction.Commit();
            }
        }

        public void MoveArchivedToTrash(ArchivedItem item)
        {
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                var table = item.EntityType == "عضو" ? "members" : item.EntityType == "مرکز" ? "partners" : item.EntityType == "سکتور" ? "sectors" : throw new InvalidOperationException("نوع بایگانی نامعتبر است.");
                command.CommandText = "UPDATE " + table + " SET deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                command.Parameters.AddWithValue("@id", item.EntityId);
                command.ExecuteNonQuery();
                Audit(connection, transaction, item.EntityType, item.EntityId, "archive-to-trash", item.Title);
                transaction.Commit();
            }
        }

        private static void RebuildPaymentChain(SQLiteConnection connection, SQLiteTransaction transaction, string centerId, string baselineIso)
        {
            DateTime due;
            if (!DateService.TryParseIso(baselineIso, out due)) throw new InvalidOperationException("سررسید پایه اشتراک معتبر نیست.");
            var rows = new List<Tuple<string, int>>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT id,covered_months FROM subscription_payments WHERE partner_id=@center ORDER BY payment_date,created_at,id";
                command.Parameters.AddWithValue("@center", centerId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(Tuple.Create(Text(reader, 0), Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
            foreach (var row in rows)
            {
                var previous = due;
                due = due.AddMonths(Math.Max(1, row.Item2));
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE subscription_payments SET previous_due_date=@previous,new_due_date=@newDue WHERE id=@id";
                    update.Parameters.AddWithValue("@previous", DateService.Iso(previous));
                    update.Parameters.AddWithValue("@newDue", DateService.Iso(due));
                    update.Parameters.AddWithValue("@id", row.Item1);
                    update.ExecuteNonQuery();
                }
            }
            SetCenterDue(connection, transaction, centerId, DateService.Iso(due));
        }

        private static string GetPaymentBaseline(SQLiteConnection connection, SQLiteTransaction transaction, string centerId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT previous_due_date FROM subscription_payments WHERE partner_id=@center ORDER BY payment_date,created_at,id LIMIT 1";
                command.Parameters.AddWithValue("@center", centerId);
                var raw = command.ExecuteScalar();
                if (raw != null && raw != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(raw, CultureInfo.InvariantCulture)))
                    return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
            }
            return GetCenterDue(connection, transaction, centerId);
        }

        private static string GetCenterDue(SQLiteConnection connection, SQLiteTransaction transaction, string centerId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COALESCE(next_due_date,'') FROM partners WHERE id=@id AND deleted_at IS NULL";
                command.Parameters.AddWithValue("@id", centerId);
                var raw = command.ExecuteScalar();
                if (raw == null) throw new InvalidOperationException("مرکز پیدا نشد.");
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
            }
        }

        private static void SetCenterDue(SQLiteConnection connection, SQLiteTransaction transaction, string centerId, string due)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE partners SET next_due_date=@due,subscription_suspended=0,updated_at=CURRENT_TIMESTAMP,version=version+1 WHERE id=@id";
                command.Parameters.AddWithValue("@due", due);
                command.Parameters.AddWithValue("@id", centerId);
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureReceiptAvailable(SQLiteConnection connection, SQLiteTransaction transaction, string receiptNo, string excludeId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(*) FROM subscription_payments WHERE receipt_no=@receipt" + (string.IsNullOrWhiteSpace(excludeId) ? "" : " AND id<>@id");
                command.Parameters.AddWithValue("@receipt", receiptNo.Trim());
                if (!string.IsNullOrWhiteSpace(excludeId)) command.Parameters.AddWithValue("@id", excludeId);
                if (Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0) throw new InvalidOperationException("این شماره رسید قبلاً ثبت شده است.");
            }
        }

        private static void ValidatePayment(decimal amount, int coveredMonths)
        {
            if (amount <= 0) throw new InvalidOperationException("مبلغ پرداخت باید بیشتر از صفر باشد.");
            if (coveredMonths < 1 || coveredMonths > 60) throw new InvalidOperationException("تعداد ماه باید بین ۱ تا ۶۰ باشد.");
        }

        private static string GenerateReceiptNo() => "RCP-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + new Random().Next(100, 999).ToString(CultureInfo.InvariantCulture);

        private static string Text(SQLiteDataReader reader, int index) => reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? "";

        private static void Audit(SQLiteConnection connection, SQLiteTransaction transaction, string entityType, string entityId, string action, string summary)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO audit_log(entity_type,entity_id,action,summary) VALUES(@type,@id,@action,@summary)";
                command.Parameters.AddWithValue("@type", entityType);
                command.Parameters.AddWithValue("@id", entityId);
                command.Parameters.AddWithValue("@action", action);
                command.Parameters.AddWithValue("@summary", summary ?? "");
                command.ExecuteNonQuery();
            }
        }
    }
}