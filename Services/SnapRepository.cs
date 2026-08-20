using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using SnapAfghanistan.Native.Data;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class SnapRepository
    {
        public static readonly string[] MemberTypes = { "معلم", "عالم", "استاد پوهنتون", "فرهنگی", "شاگرد" };

        public DashboardStats GetDashboard()
        {
            var result = new DashboardStats();
            var reminderDays = GetSettings().DueReminderDays;
            using (var connection = Database.Open())
            {
                result.ActiveMembers = Scalar(connection, "SELECT COUNT(*) FROM members WHERE deleted_at IS NULL AND status='فعال'");
                result.RegisteredCenters = Scalar(connection, "SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND status!='بایگانی'");
                result.ActiveSectors = Scalar(connection, "SELECT COUNT(*) FROM sectors WHERE deleted_at IS NULL AND status='فعال'");
                result.Overdue = Scalar(connection, "SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND subscription_suspended=0 AND monthly_subscription>0 AND COALESCE(next_due_date,'')<>'' AND date(next_due_date)<date('now')");
                result.NearDue = Scalar(connection,
                    "SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND subscription_suspended=0 AND monthly_subscription>0 AND date(next_due_date)>=date('now') AND date(next_due_date)<=date('now','+" + reminderDays.ToString(CultureInfo.InvariantCulture) + " day')");
                result.Suspended = Scalar(connection, "SELECT COUNT(*) FROM partners WHERE deleted_at IS NULL AND subscription_suspended=1");
                result.MonthRevenue = Scalar(connection, "SELECT COALESCE(SUM(amount),0) FROM subscription_payments WHERE strftime('%Y-%m',payment_date)=strftime('%Y-%m','now','localtime')");

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT member_type,COUNT(*) FROM members WHERE deleted_at IS NULL AND status='فعال' GROUP BY member_type";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) result.MemberTypes[Text(reader, 0)] = Number(reader, 1);
                    }
                }
            }

            foreach (var type in MemberTypes)
                if (!result.MemberTypes.ContainsKey(type)) result.MemberTypes[type] = 0;
            return result;
        }

        public PagedResult<MemberListItem> SearchMembers(string search, string memberType, string status, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(25, Math.Min(500, pageSize));
            var where = " WHERE deleted_at IS NULL ";
            if (!string.IsNullOrWhiteSpace(search))
                where += " AND (member_code LIKE @search OR first_name LIKE @search OR father_name LIKE @search OR tazkira_no LIKE @search OR phone LIKE @search OR institution LIKE @search) ";
            if (!string.IsNullOrWhiteSpace(memberType) && memberType != "همه") where += " AND member_type=@type ";
            if (!string.IsNullOrWhiteSpace(status) && status != "همه") where += " AND status=@status ";

            using (var connection = Database.Open())
            {
                int total;
                using (var count = connection.CreateCommand())
                {
                    count.CommandText = "SELECT COUNT(*) FROM members" + where;
                    AddMemberFilters(count, search, memberType, status);
                    total = Convert.ToInt32(count.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
                }

                var items = new List<MemberListItem>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT id,member_code,member_type,first_name,father_name,tazkira_no,
COALESCE(phone,''),current_address,COALESCE(institution,''),status,created_at
FROM members" + where + " ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                    AddMemberFilters(command, search, memberType, status);
                    command.Parameters.AddWithValue("@limit", pageSize);
                    command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new MemberListItem
                            {
                                Id = Text(reader, 0), Code = Text(reader, 1), Type = Text(reader, 2), Name = Text(reader, 3),
                                FatherName = Text(reader, 4), Tazkira = Text(reader, 5), Phone = Text(reader, 6), Address = Text(reader, 7),
                                Institution = Text(reader, 8), Status = Text(reader, 9), CreatedAt = Text(reader, 10)
                            });
                        }
                    }
                }
                return new PagedResult<MemberListItem> { Items = items, Total = total, Page = page, PageSize = pageSize };
            }
        }

        public MemberRecord? GetMember(string id)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT id,member_code,member_type,first_name,father_name,tazkira_no,COALESCE(phone,''),
original_address,current_address,COALESCE(institution,''),status,COALESCE(notes,''),created_at,updated_at
FROM members WHERE id=@id LIMIT 1";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    var record = new MemberRecord
                    {
                        Id = Text(reader, 0), Code = Text(reader, 1), Type = Text(reader, 2), FirstName = Text(reader, 3),
                        FatherName = Text(reader, 4), TazkiraNo = Text(reader, 5), Phone = Text(reader, 6),
                        OriginalAddress = Text(reader, 7), CurrentAddress = Text(reader, 8), Institution = Text(reader, 9),
                        Status = Text(reader, 10), Notes = Text(reader, 11), CreatedAt = Text(reader, 12), UpdatedAt = Text(reader, 13)
                    };
                    reader.Close();
                    using (var attachment = connection.CreateCommand())
                    {
                        attachment.CommandText = "SELECT original_name,stored_name FROM member_attachments WHERE member_id=@id ORDER BY created_at DESC LIMIT 1";
                        attachment.Parameters.AddWithValue("@id", id);
                        using (var ar = attachment.ExecuteReader())
                        {
                            if (ar.Read())
                            {
                                record.AttachmentName = Text(ar, 0);
                                record.AttachmentPath = Path.Combine(Database.AttachmentsDirectory, Text(ar, 1));
                            }
                        }
                    }
                    return record;
                }
            }
        }

        public string SaveMember(MemberRecord member, string newAttachmentPath)
        {
            ValidateMember(member);
            var isNew = string.IsNullOrWhiteSpace(member.Id);
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                if (isNew)
                {
                    member.Id = Guid.NewGuid().ToString("N");
                    member.Code = NextCode(connection, transaction, "members", Database.GetSetting("member_code_prefix", "SNP-HRT"));
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"INSERT INTO members(id,member_code,member_type,first_name,father_name,tazkira_no,phone,original_address,current_address,institution,status,notes)
VALUES(@id,@code,@type,@name,@father,@tazkira,@phone,@original,@current,@institution,@status,@notes)";
                        AddMemberParameters(command, member);
                        command.ExecuteNonQuery();
                    }
                    Audit(connection, transaction, "member", member.Id, "create", member.Code + " - " + member.FirstName);
                }
                else
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"UPDATE members SET member_type=@type,first_name=@name,father_name=@father,tazkira_no=@tazkira,
phone=@phone,original_address=@original,current_address=@current,institution=@institution,status=@status,notes=@notes,
updated_at=CURRENT_TIMESTAMP,version=version+1 WHERE id=@id";
                        AddMemberParameters(command, member);
                        command.ExecuteNonQuery();
                    }
                    Audit(connection, transaction, "member", member.Id, "update", member.Code + " - " + member.FirstName);
                }
                transaction.Commit();
            }

            if (!string.IsNullOrWhiteSpace(newAttachmentPath)) AddMemberAttachment(member.Id, newAttachmentPath);
            return member.Id;
        }

        public void ArchiveMember(string id)
        {
            ExecuteEntityAction("UPDATE members SET status='بایگانی',archived_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "member", id, "archive");
        }

        public void DeleteMember(string id)
        {
            ExecuteEntityAction("UPDATE members SET deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "member", id, "delete");
        }

        public IReadOnlyList<SectorItem> GetSectors(bool includeInactive = true)
        {
            var result = new List<SectorItem>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT s.id,s.name,COALESCE(s.description,''),s.status,
(SELECT COUNT(*) FROM partners p WHERE p.sector_id=s.id AND p.deleted_at IS NULL)
FROM sectors s WHERE s.deleted_at IS NULL" + (includeInactive ? "" : " AND s.status='فعال'") + " ORDER BY s.name";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new SectorItem
                    {
                        Id = Text(reader, 0), Name = Text(reader, 1), Description = Text(reader, 2), Status = Text(reader, 3), CenterCount = Number(reader, 4)
                    });
                }
            }
            return result;
        }

        public string SaveSector(SectorItem sector)
        {
            if (string.IsNullOrWhiteSpace(sector.Name)) throw new InvalidOperationException("نام سکتور ضروری است.");
            var isNew = string.IsNullOrWhiteSpace(sector.Id);
            if (isNew) sector.Id = Guid.NewGuid().ToString("N");
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = isNew
                    ? "INSERT INTO sectors(id,name,description,status) VALUES(@id,@name,@description,@status)"
                    : "UPDATE sectors SET name=@name,description=@description,status=@status,updated_at=CURRENT_TIMESTAMP,version=version+1 WHERE id=@id";
                command.Parameters.AddWithValue("@id", sector.Id);
                command.Parameters.AddWithValue("@name", sector.Name.Trim());
                command.Parameters.AddWithValue("@description", sector.Description?.Trim() ?? "");
                command.Parameters.AddWithValue("@status", sector.Status);
                command.ExecuteNonQuery();
                Audit(connection, transaction, "sector", sector.Id, isNew ? "create" : "update", sector.Name);
                transaction.Commit();
            }
            return sector.Id;
        }

        public void DeleteSector(string id)
        {
            using (var connection = Database.Open())
            {
                using (var check = connection.CreateCommand())
                {
                    check.CommandText = "SELECT COUNT(*) FROM partners WHERE sector_id=@id AND deleted_at IS NULL";
                    check.Parameters.AddWithValue("@id", id);
                    if (Convert.ToInt32(check.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0)
                        throw new InvalidOperationException("این سکتور مرکز فعال دارد؛ ابتدا مرکزها را انتقال یا حذف کنید.");
                }
            }
            ExecuteEntityAction("UPDATE sectors SET deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "sector", id, "delete");
        }

        public PagedResult<CenterListItem> SearchCenters(string search, string sectorId, string subscriptionStatus, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(25, Math.Min(500, pageSize));
            var reminderDays = GetSettings().DueReminderDays;
            var statusExpression = SubscriptionStatusSql(reminderDays);
            var where = " WHERE p.deleted_at IS NULL ";
            if (!string.IsNullOrWhiteSpace(search)) where += " AND (p.partner_code LIKE @search OR p.legal_name LIKE @search OR p.trade_name LIKE @search OR p.phone LIKE @search OR p.representative LIKE @search) ";
            if (!string.IsNullOrWhiteSpace(sectorId) && sectorId != "همه") where += " AND p.sector_id=@sector ";
            if (!string.IsNullOrWhiteSpace(subscriptionStatus) && subscriptionStatus != "همه") where += " AND " + statusExpression + "=@substatus ";

            using (var connection = Database.Open())
            {
                int total;
                using (var count = connection.CreateCommand())
                {
                    count.CommandText = "SELECT COUNT(*) FROM partners p" + where;
                    AddCenterFilters(count, search, sectorId, subscriptionStatus);
                    total = Convert.ToInt32(count.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
                }

                var items = new List<CenterListItem>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT p.id,p.partner_code,p.legal_name,s.name,COALESCE(p.representative,''),COALESCE(p.phone,''),
COALESCE(p.discount_rate,''),COALESCE(p.monthly_subscription,0),COALESCE(p.subscription_start,''),COALESCE(p.next_due_date,'')," + statusExpression + @",p.status
FROM partners p JOIN sectors s ON s.id=p.sector_id" + where + " ORDER BY p.created_at DESC LIMIT @limit OFFSET @offset";
                    AddCenterFilters(command, search, sectorId, subscriptionStatus);
                    command.Parameters.AddWithValue("@limit", pageSize);
                    command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) items.Add(new CenterListItem
                        {
                            Id = Text(reader, 0), Code = Text(reader, 1), Name = Text(reader, 2), Sector = Text(reader, 3), Representative = Text(reader, 4),
                            Phone = Text(reader, 5), Discount = Text(reader, 6), MonthlySubscription = Number(reader, 7), StartDate = Text(reader, 8),
                            DueDate = Text(reader, 9), SubscriptionStatus = Text(reader, 10), Status = Text(reader, 11)
                        });
                    }
                }
                return new PagedResult<CenterListItem> { Items = items, Total = total, Page = page, PageSize = pageSize };
            }
        }

        public CenterRecord? GetCenter(string id)
        {
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT id,partner_code,sector_id,legal_name,COALESCE(trade_name,''),COALESCE(license_no,''),
COALESCE(representative,''),COALESCE(phone,''),COALESCE(address,''),COALESCE(contract_start,''),COALESCE(contract_end,''),
COALESCE(discount_rate,''),COALESCE(fee_basis,'بدون حق‌الخدمت'),COALESCE(fee_amount,0),COALESCE(monthly_subscription,0),
COALESCE(subscription_start,''),COALESCE(next_due_date,''),COALESCE(subscription_suspended,0),status,COALESCE(notes,'')
FROM partners WHERE id=@id LIMIT 1";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new CenterRecord
                    {
                        Id = Text(reader, 0), Code = Text(reader, 1), SectorId = Text(reader, 2), LegalName = Text(reader, 3), TradeName = Text(reader, 4),
                        LicenseNo = Text(reader, 5), Representative = Text(reader, 6), Phone = Text(reader, 7), Address = Text(reader, 8),
                        ContractStart = Text(reader, 9), ContractEnd = Text(reader, 10), DiscountRate = Text(reader, 11), FeeBasis = Text(reader, 12),
                        FeeAmount = DecimalNumber(reader, 13), MonthlySubscription = DecimalNumber(reader, 14), SubscriptionStart = Text(reader, 15),
                        NextDueDate = Text(reader, 16), SubscriptionSuspended = Number(reader, 17) == 1, Status = Text(reader, 18), Notes = Text(reader, 19)
                    };
                }
            }
        }

        public string SaveCenter(CenterRecord center)
        {
            ValidateCenter(center);
            var isNew = string.IsNullOrWhiteSpace(center.Id);
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                if (isNew)
                {
                    center.Id = Guid.NewGuid().ToString("N");
                    center.Code = NextCode(connection, transaction, "centers", "CTR-HRT");
                }
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = isNew ? @"INSERT INTO partners(id,partner_code,sector_id,legal_name,trade_name,license_no,representative,phone,address,
contract_start,contract_end,discount_rate,fee_basis,fee_amount,monthly_subscription,subscription_start,next_due_date,subscription_suspended,status,notes)
VALUES(@id,@code,@sector,@legal,@trade,@license,@representative,@phone,@address,@start,@end,@discount,@feeBasis,@feeAmount,@subscription,@subStart,@due,@suspended,@status,@notes)"
                    : @"UPDATE partners SET sector_id=@sector,legal_name=@legal,trade_name=@trade,license_no=@license,representative=@representative,
phone=@phone,address=@address,contract_start=@start,contract_end=@end,discount_rate=@discount,fee_basis=@feeBasis,fee_amount=@feeAmount,
monthly_subscription=@subscription,subscription_start=@subStart,next_due_date=@due,subscription_suspended=@suspended,status=@status,
notes=@notes,updated_at=CURRENT_TIMESTAMP,version=version+1 WHERE id=@id";
                    AddCenterParameters(command, center);
                    command.ExecuteNonQuery();
                }
                Audit(connection, transaction, "center", center.Id, isNew ? "create" : "update", center.Code + " - " + center.LegalName);
                transaction.Commit();
            }
            return center.Id;
        }

        public void ArchiveCenter(string id)
        {
            ExecuteEntityAction("UPDATE partners SET status='بایگانی',archived_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "center", id, "archive");
        }

        public void DeleteCenter(string id)
        {
            ExecuteEntityAction("UPDATE partners SET deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "center", id, "delete");
        }

        public void ConfigureSubscription(string centerId, decimal amount, DateTime start, DateTime due, bool suspended)
        {
            if (amount < 0) throw new InvalidOperationException("مبلغ اشتراک نمی‌تواند منفی باشد.");
            if (amount > 0 && due.Date < start.Date) throw new InvalidOperationException("تاریخ سررسید باید پس از تاریخ آغاز باشد.");
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"UPDATE partners SET monthly_subscription=@amount,subscription_start=@start,next_due_date=@due,
subscription_suspended=@suspended,updated_at=CURRENT_TIMESTAMP,version=version+1 WHERE id=@id";
                command.Parameters.AddWithValue("@amount", amount);
                command.Parameters.AddWithValue("@start", DateIso(start));
                command.Parameters.AddWithValue("@due", DateIso(due));
                command.Parameters.AddWithValue("@suspended", suspended ? 1 : 0);
                command.Parameters.AddWithValue("@id", centerId);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("مرکز پیدا نشد.");
                Audit(connection, transaction, "subscription", centerId, "configure", amount.ToString("0", CultureInfo.InvariantCulture));
                transaction.Commit();
            }
        }

        public string RegisterPayment(string centerId, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes)
        {
            if (amount <= 0) throw new InvalidOperationException("مبلغ پرداخت باید بیشتر از صفر باشد.");
            if (coveredMonths < 1 || coveredMonths > 60) throw new InvalidOperationException("تعداد ماه باید بین ۱ تا ۶۰ باشد.");
            if (string.IsNullOrWhiteSpace(receiptNo)) receiptNo = GenerateReceiptNo();

            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                string previousDue;
                using (var current = connection.CreateCommand())
                {
                    current.Transaction = transaction;
                    current.CommandText = "SELECT COALESCE(next_due_date,'') FROM partners WHERE id=@id AND deleted_at IS NULL";
                    current.Parameters.AddWithValue("@id", centerId);
                    var raw = current.ExecuteScalar();
                    if (raw == null) throw new InvalidOperationException("مرکز پیدا نشد.");
                    previousDue = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                }

                DateTime baseDate;
                if (!DateTime.TryParseExact(previousDue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out baseDate) || baseDate < paymentDate.Date)
                    baseDate = paymentDate.Date;
                var newDue = baseDate.AddMonths(coveredMonths);
                var paymentId = Guid.NewGuid().ToString("N");
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = @"INSERT INTO subscription_payments(id,partner_id,payment_date,amount,receipt_no,covered_months,previous_due_date,new_due_date,notes)
VALUES(@id,@partner,@date,@amount,@receipt,@months,@previous,@newDue,@notes)";
                    insert.Parameters.AddWithValue("@id", paymentId);
                    insert.Parameters.AddWithValue("@partner", centerId);
                    insert.Parameters.AddWithValue("@date", DateIso(paymentDate));
                    insert.Parameters.AddWithValue("@amount", amount);
                    insert.Parameters.AddWithValue("@receipt", receiptNo.Trim());
                    insert.Parameters.AddWithValue("@months", coveredMonths);
                    insert.Parameters.AddWithValue("@previous", previousDue);
                    insert.Parameters.AddWithValue("@newDue", DateIso(newDue));
                    insert.Parameters.AddWithValue("@notes", notes?.Trim() ?? "");
                    insert.ExecuteNonQuery();
                }
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE partners SET next_due_date=@due,subscription_suspended=0,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                    update.Parameters.AddWithValue("@due", DateIso(newDue));
                    update.Parameters.AddWithValue("@id", centerId);
                    update.ExecuteNonQuery();
                }
                Audit(connection, transaction, "payment", paymentId, "create", receiptNo + " - " + amount.ToString("0", CultureInfo.InvariantCulture));
                transaction.Commit();
                return receiptNo;
            }
        }

        public IReadOnlyList<PaymentItem> GetPayments(string centerId = "", int limit = 500)
        {
            var result = new List<PaymentItem>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT sp.id,sp.partner_id,p.legal_name,sp.payment_date,sp.amount,sp.receipt_no,sp.covered_months,sp.new_due_date,COALESCE(sp.notes,'')
FROM subscription_payments sp JOIN partners p ON p.id=sp.partner_id" + (string.IsNullOrWhiteSpace(centerId) ? "" : " WHERE sp.partner_id=@center") + " ORDER BY sp.payment_date DESC,sp.created_at DESC LIMIT @limit";
                if (!string.IsNullOrWhiteSpace(centerId)) command.Parameters.AddWithValue("@center", centerId);
                command.Parameters.AddWithValue("@limit", limit);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new PaymentItem
                    {
                        Id = Text(reader, 0), CenterId = Text(reader, 1), CenterName = Text(reader, 2), PaymentDate = Text(reader, 3),
                        Amount = DecimalNumber(reader, 4), ReceiptNo = Text(reader, 5), CoveredMonths = (int)Number(reader, 6),
                        NewDueDate = Text(reader, 7), Notes = Text(reader, 8)
                    });
                }
            }
            return result;
        }

        public IReadOnlyList<NoteItem> GetNotes(string status = "همه")
        {
            var result = new List<NoteItem>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT id,title,note_type,COALESCE(related_name,''),priority,COALESCE(due_date,''),status,body,updated_at
FROM notes WHERE deleted_at IS NULL" + (status == "همه" ? "" : " AND status=@status") + " ORDER BY CASE priority WHEN 'فوری' THEN 0 WHEN 'مهم' THEN 1 ELSE 2 END,due_date,updated_at DESC";
                if (status != "همه") command.Parameters.AddWithValue("@status", status);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new NoteItem
                    {
                        Id = Text(reader, 0), Title = Text(reader, 1), Type = Text(reader, 2), RelatedName = Text(reader, 3),
                        Priority = Text(reader, 4), DueDate = Text(reader, 5), Status = Text(reader, 6), Body = Text(reader, 7), UpdatedAt = Text(reader, 8)
                    });
                }
            }
            return result;
        }

        public string SaveNote(NoteItem note)
        {
            if (string.IsNullOrWhiteSpace(note.Title)) throw new InvalidOperationException("عنوان یادداشت ضروری است.");
            if (string.IsNullOrWhiteSpace(note.Body)) throw new InvalidOperationException("متن یادداشت ضروری است.");
            var isNew = string.IsNullOrWhiteSpace(note.Id);
            if (isNew) note.Id = Guid.NewGuid().ToString("N");
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = isNew
                    ? "INSERT INTO notes(id,title,note_type,related_name,priority,due_date,status,body) VALUES(@id,@title,@type,@related,@priority,@due,@status,@body)"
                    : "UPDATE notes SET title=@title,note_type=@type,related_name=@related,priority=@priority,due_date=@due,status=@status,body=@body,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
                command.Parameters.AddWithValue("@id", note.Id);
                command.Parameters.AddWithValue("@title", note.Title.Trim());
                command.Parameters.AddWithValue("@type", note.Type);
                command.Parameters.AddWithValue("@related", note.RelatedName?.Trim() ?? "");
                command.Parameters.AddWithValue("@priority", note.Priority);
                command.Parameters.AddWithValue("@due", note.DueDate?.Trim() ?? "");
                command.Parameters.AddWithValue("@status", note.Status);
                command.Parameters.AddWithValue("@body", note.Body.Trim());
                command.ExecuteNonQuery();
                Audit(connection, transaction, "note", note.Id, isNew ? "create" : "update", note.Title);
                transaction.Commit();
            }
            return note.Id;
        }

        public void DeleteNote(string id)
        {
            ExecuteEntityAction("UPDATE notes SET deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=@id", "note", id, "delete");
        }

        public IReadOnlyList<TrashItem> GetTrash()
        {
            var result = new List<TrashItem>();
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT 'عضو',id,first_name,member_code,deleted_at FROM members WHERE deleted_at IS NOT NULL
UNION ALL SELECT 'مرکز',id,legal_name,partner_code,deleted_at FROM partners WHERE deleted_at IS NOT NULL
UNION ALL SELECT 'سکتور',id,name,status,deleted_at FROM sectors WHERE deleted_at IS NOT NULL
UNION ALL SELECT 'یادداشت',id,title,note_type,deleted_at FROM notes WHERE deleted_at IS NOT NULL
ORDER BY deleted_at DESC";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new TrashItem
                    {
                        EntityType = Text(reader, 0), EntityId = Text(reader, 1), Title = Text(reader, 2), Detail = Text(reader, 3), DeletedAt = Text(reader, 4)
                    });
                }
            }
            return result;
        }

        public void RestoreTrash(TrashItem item)
        {
            var table = TableFor(item.EntityType);
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE " + table + " SET deleted_at=NULL" + (table == "members" || table == "partners" ? ",status='فعال'" : "") + " WHERE id=@id";
                command.Parameters.AddWithValue("@id", item.EntityId);
                command.ExecuteNonQuery();
                Audit(connection, transaction, item.EntityType, item.EntityId, "restore", item.Title);
                transaction.Commit();
            }
        }

        public void PermanentlyDeleteTrash(TrashItem item)
        {
            var table = TableFor(item.EntityType);
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                if (table == "members")
                {
                    using (var a = connection.CreateCommand()) { a.Transaction = transaction; a.CommandText = "DELETE FROM member_attachments WHERE member_id=@id"; a.Parameters.AddWithValue("@id", item.EntityId); a.ExecuteNonQuery(); }
                }
                if (table == "partners")
                {
                    using (var p = connection.CreateCommand()) { p.Transaction = transaction; p.CommandText = "DELETE FROM subscription_payments WHERE partner_id=@id"; p.Parameters.AddWithValue("@id", item.EntityId); p.ExecuteNonQuery(); }
                }
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM " + table + " WHERE id=@id AND deleted_at IS NOT NULL";
                    command.Parameters.AddWithValue("@id", item.EntityId);
                    command.ExecuteNonQuery();
                }
                Audit(connection, transaction, item.EntityType, item.EntityId, "purge", item.Title);
                transaction.Commit();
            }
            if (table == "members")
            {
                var folder = Path.Combine(Database.AttachmentsDirectory, "members", item.EntityId);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        public AppSettingsRecord GetSettings()
        {
            int reminder;
            int.TryParse(Database.GetSetting("due_reminder_days", "7"), NumberStyles.Integer, CultureInfo.InvariantCulture, out reminder);
            return new AppSettingsRecord
            {
                CompanyName = Database.GetSetting("company_name", "اسنپ افغانستان"),
                Province = Database.GetSetting("province", "هرات"),
                MemberPrefix = Database.GetSetting("member_code_prefix", "SNP-HRT"),
                DueReminderDays = reminder <= 0 ? 7 : reminder,
                AutoBackup = Database.GetSetting("auto_backup", "1") == "1"
            };
        }

        public void SaveSettings(AppSettingsRecord settings)
        {
            if (string.IsNullOrWhiteSpace(settings.CompanyName)) throw new InvalidOperationException("نام دفتر ضروری است.");
            if (string.IsNullOrWhiteSpace(settings.MemberPrefix)) throw new InvalidOperationException("پیشوند کد عضو ضروری است.");
            Database.SetSetting("company_name", settings.CompanyName.Trim());
            Database.SetSetting("province", settings.Province.Trim());
            Database.SetSetting("member_code_prefix", settings.MemberPrefix.Trim().ToUpperInvariant());
            Database.SetSetting("due_reminder_days", Math.Max(1, Math.Min(60, settings.DueReminderDays)).ToString(CultureInfo.InvariantCulture));
            Database.SetSetting("auto_backup", settings.AutoBackup ? "1" : "0");
        }

        public DataTable BuildReport(string reportKey, string memberType = "همه")
        {
            switch (reportKey)
            {
                case "members": return MemberReport(memberType);
                case "centers": return CenterReport(false);
                case "debtors": return CenterReport(true);
                case "sectors": return SectorReport();
                case "payments": return PaymentReport();
                default: throw new InvalidOperationException("نوع گزارش نامعتبر است.");
            }
        }

        private DataTable MemberReport(string memberType)
        {
            var table = NewTable("کد", "نام", "نام پدر", "گروه", "شماره تذکره", "موبایل", "آدرس فعلی", "اداره/مکتب", "وضعیت");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT member_code,first_name,father_name,member_type,tazkira_no,COALESCE(phone,''),current_address,COALESCE(institution,''),status
FROM members WHERE deleted_at IS NULL" + (memberType == "همه" ? "" : " AND member_type=@type") + " ORDER BY first_name";
                if (memberType != "همه") command.Parameters.AddWithValue("@type", memberType);
                FillTable(table, command);
            }
            return table;
        }

        private DataTable CenterReport(bool onlyDebtors)
        {
            var table = NewTable("کد", "نام مرکز", "سکتور", "نماینده", "موبایل", "تخفیف", "اشتراک ماهانه", "سررسید", "وضعیت اشتراک");
            var statusSql = SubscriptionStatusSql(GetSettings().DueReminderDays);
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT p.partner_code,p.legal_name,s.name,COALESCE(p.representative,''),COALESCE(p.phone,''),COALESCE(p.discount_rate,''),
printf('%,d',COALESCE(p.monthly_subscription,0)),COALESCE(p.next_due_date,'')," + statusSql + @"
FROM partners p JOIN sectors s ON s.id=p.sector_id WHERE p.deleted_at IS NULL" + (onlyDebtors ? " AND " + statusSql + "='معوق'" : "") + " ORDER BY p.legal_name";
                FillTable(table, command);
            }
            return table;
        }

        private DataTable SectorReport()
        {
            var table = NewTable("سکتور", "توضیحات", "وضعیت", "تعداد مراکز");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT s.name,COALESCE(s.description,''),s.status,
(SELECT COUNT(*) FROM partners p WHERE p.sector_id=s.id AND p.deleted_at IS NULL)
FROM sectors s WHERE s.deleted_at IS NULL ORDER BY s.name";
                FillTable(table, command);
            }
            return table;
        }

        private DataTable PaymentReport()
        {
            var table = NewTable("شماره رسید", "مرکز", "تاریخ پرداخت", "مبلغ افغانی", "ماه", "سررسید جدید", "توضیحات");
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT sp.receipt_no,p.legal_name,sp.payment_date,printf('%,d',sp.amount),sp.covered_months,sp.new_due_date,COALESCE(sp.notes,'')
FROM subscription_payments sp JOIN partners p ON p.id=sp.partner_id ORDER BY sp.payment_date DESC,sp.created_at DESC";
                FillTable(table, command);
            }
            return table;
        }

        private static void FillTable(DataTable table, SQLiteCommand command)
        {
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var values = new object[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++) values[i] = reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
                    table.Rows.Add(values);
                }
            }
        }

        private static DataTable NewTable(params string[] columns)
        {
            var table = new DataTable();
            foreach (var column in columns) table.Columns.Add(column, typeof(string));
            return table;
        }

        private static void AddMemberFilters(SQLiteCommand command, string search, string memberType, string status)
        {
            if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", "%" + search.Trim() + "%");
            if (!string.IsNullOrWhiteSpace(memberType) && memberType != "همه") command.Parameters.AddWithValue("@type", memberType);
            if (!string.IsNullOrWhiteSpace(status) && status != "همه") command.Parameters.AddWithValue("@status", status);
        }

        private static void AddCenterFilters(SQLiteCommand command, string search, string sectorId, string subscriptionStatus)
        {
            if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", "%" + search.Trim() + "%");
            if (!string.IsNullOrWhiteSpace(sectorId) && sectorId != "همه") command.Parameters.AddWithValue("@sector", sectorId);
            if (!string.IsNullOrWhiteSpace(subscriptionStatus) && subscriptionStatus != "همه") command.Parameters.AddWithValue("@substatus", subscriptionStatus);
        }

        private static void AddMemberParameters(SQLiteCommand command, MemberRecord member)
        {
            command.Parameters.AddWithValue("@id", member.Id);
            command.Parameters.AddWithValue("@code", member.Code);
            command.Parameters.AddWithValue("@type", member.Type);
            command.Parameters.AddWithValue("@name", member.FirstName.Trim());
            command.Parameters.AddWithValue("@father", member.FatherName.Trim());
            command.Parameters.AddWithValue("@tazkira", member.TazkiraNo.Trim());
            command.Parameters.AddWithValue("@phone", member.Phone?.Trim() ?? "");
            command.Parameters.AddWithValue("@original", member.OriginalAddress.Trim());
            command.Parameters.AddWithValue("@current", member.CurrentAddress.Trim());
            command.Parameters.AddWithValue("@institution", member.Institution?.Trim() ?? "");
            command.Parameters.AddWithValue("@status", member.Status);
            command.Parameters.AddWithValue("@notes", member.Notes?.Trim() ?? "");
        }

        private static void AddCenterParameters(SQLiteCommand command, CenterRecord center)
        {
            command.Parameters.AddWithValue("@id", center.Id);
            command.Parameters.AddWithValue("@code", center.Code);
            command.Parameters.AddWithValue("@sector", center.SectorId);
            command.Parameters.AddWithValue("@legal", center.LegalName.Trim());
            command.Parameters.AddWithValue("@trade", center.TradeName?.Trim() ?? "");
            command.Parameters.AddWithValue("@license", center.LicenseNo?.Trim() ?? "");
            command.Parameters.AddWithValue("@representative", center.Representative?.Trim() ?? "");
            command.Parameters.AddWithValue("@phone", center.Phone?.Trim() ?? "");
            command.Parameters.AddWithValue("@address", center.Address?.Trim() ?? "");
            command.Parameters.AddWithValue("@start", center.ContractStart?.Trim() ?? "");
            command.Parameters.AddWithValue("@end", center.ContractEnd?.Trim() ?? "");
            command.Parameters.AddWithValue("@discount", center.DiscountRate?.Trim() ?? "");
            command.Parameters.AddWithValue("@feeBasis", center.FeeBasis ?? "بدون حق‌الخدمت");
            command.Parameters.AddWithValue("@feeAmount", center.FeeAmount);
            command.Parameters.AddWithValue("@subscription", center.MonthlySubscription);
            command.Parameters.AddWithValue("@subStart", center.SubscriptionStart ?? "");
            command.Parameters.AddWithValue("@due", center.NextDueDate ?? "");
            command.Parameters.AddWithValue("@suspended", center.SubscriptionSuspended ? 1 : 0);
            command.Parameters.AddWithValue("@status", center.Status);
            command.Parameters.AddWithValue("@notes", center.Notes?.Trim() ?? "");
        }

        private static void ValidateMember(MemberRecord member)
        {
            if (string.IsNullOrWhiteSpace(member.FirstName)) throw new InvalidOperationException("نام عضو ضروری است.");
            if (string.IsNullOrWhiteSpace(member.FatherName)) throw new InvalidOperationException("نام پدر ضروری است.");
            if (string.IsNullOrWhiteSpace(member.TazkiraNo)) throw new InvalidOperationException("شماره تذکره ضروری است.");
            if (string.IsNullOrWhiteSpace(member.OriginalAddress)) throw new InvalidOperationException("آدرس اصلی ضروری است.");
            if (string.IsNullOrWhiteSpace(member.CurrentAddress)) throw new InvalidOperationException("آدرس فعلی ضروری است.");
        }

        private static void ValidateCenter(CenterRecord center)
        {
            if (string.IsNullOrWhiteSpace(center.SectorId)) throw new InvalidOperationException("سکتور را انتخاب کنید.");
            if (string.IsNullOrWhiteSpace(center.LegalName)) throw new InvalidOperationException("نام قانونی مرکز ضروری است.");
        }

        private static string NextCode(SQLiteConnection connection, SQLiteTransaction transaction, string counter, string prefix)
        {
            using (var create = connection.CreateCommand())
            {
                create.Transaction = transaction;
                create.CommandText = "INSERT OR IGNORE INTO counters(name,value) VALUES(@name,0); UPDATE counters SET value=value+1 WHERE name=@name;";
                create.Parameters.AddWithValue("@name", counter);
                create.ExecuteNonQuery();
            }
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT value FROM counters WHERE name=@name";
                read.Parameters.AddWithValue("@name", counter);
                var number = Convert.ToInt64(read.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
                return prefix + "-" + number.ToString("000000", CultureInfo.InvariantCulture);
            }
        }

        private void AddMemberAttachment(string memberId, string sourcePath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("فایل تذکره پیدا نشد.", sourcePath);
            var info = new FileInfo(sourcePath);
            if (info.Length > 12 * 1024 * 1024) throw new InvalidOperationException("حجم فایل تذکره باید کمتر از ۱۲ مگابایت باشد.");
            var extension = info.Extension.ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".pdf")
                throw new InvalidOperationException("فقط فایل JPG، PNG یا PDF قابل قبول است.");

            var relativeFolder = Path.Combine("members", memberId);
            var targetFolder = Path.Combine(Database.AttachmentsDirectory, relativeFolder);
            Directory.CreateDirectory(targetFolder);
            var storedFile = Guid.NewGuid().ToString("N") + extension;
            var relativePath = Path.Combine(relativeFolder, storedFile);
            var targetPath = Path.Combine(Database.AttachmentsDirectory, relativePath);
            File.Copy(sourcePath, targetPath, false);

            string hash;
            using (var stream = File.OpenRead(targetPath))
            using (var sha = SHA256.Create()) hash = ToHex(sha.ComputeHash(stream));
            using (var connection = Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO member_attachments(id,member_id,document_type,original_name,stored_name,mime_type,size_bytes,sha256)
VALUES(@id,@member,'تذکره',@original,@stored,@mime,@size,@hash)";
                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("@member", memberId);
                command.Parameters.AddWithValue("@original", info.Name);
                command.Parameters.AddWithValue("@stored", relativePath);
                command.Parameters.AddWithValue("@mime", extension == ".pdf" ? "application/pdf" : "image/" + extension.TrimStart('.').Replace("jpg", "jpeg"));
                command.Parameters.AddWithValue("@size", info.Length);
                command.Parameters.AddWithValue("@hash", hash);
                command.ExecuteNonQuery();
            }
        }

        private void ExecuteEntityAction(string sql, string entityType, string id, string action)
        {
            using (var connection = Database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
                Audit(connection, transaction, entityType, id, action, "");
                transaction.Commit();
            }
        }

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

        private static string SubscriptionStatusSql(int reminderDays)
        {
            return $@"CASE
WHEN COALESCE(p.subscription_suspended,0)=1 THEN 'تعلیق'
WHEN COALESCE(p.monthly_subscription,0)<=0 OR COALESCE(p.next_due_date,'')='' THEN 'تنظیم نشده'
WHEN date(p.next_due_date)<date('now') THEN 'معوق'
WHEN date(p.next_due_date)<=date('now','+{reminderDays.ToString(CultureInfo.InvariantCulture)} day') THEN 'نزدیک سررسید'
ELSE 'فعال' END";
        }

        private static string GenerateReceiptNo()
        {
            return "RCP-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + new Random().Next(100, 999).ToString(CultureInfo.InvariantCulture);
        }

        private static string TableFor(string entityType)
        {
            switch (entityType)
            {
                case "عضو": return "members";
                case "مرکز": return "partners";
                case "سکتور": return "sectors";
                case "یادداشت": return "notes";
                default: throw new InvalidOperationException("نوع رکورد نامعتبر است.");
            }
        }

        private static long Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand()) { command.CommandText = sql; return Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture); }
        }

        private static string Text(SQLiteDataReader reader, int index) => reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? "";
        private static long Number(SQLiteDataReader reader, int index) => reader.IsDBNull(index) ? 0 : Convert.ToInt64(reader.GetValue(index), CultureInfo.InvariantCulture);
        private static decimal DecimalNumber(SQLiteDataReader reader, int index) => reader.IsDBNull(index) ? 0 : Convert.ToDecimal(reader.GetValue(index), CultureInfo.InvariantCulture);
        private static string DateIso(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
