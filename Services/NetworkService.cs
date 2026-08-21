using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using SnapAfghanistan.Native.Models;

namespace SnapAfghanistan.Native.Services
{
    public sealed class NetworkConfig
    {
        public string Mode { get; set; } = "";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 47821;
        public string Secret { get; set; } = "";
        public bool IsServer => string.Equals(Mode, "server", StringComparison.OrdinalIgnoreCase);
        public bool IsClient => string.Equals(Mode, "client", StringComparison.OrdinalIgnoreCase);
        public bool IsValid => (IsServer || IsClient) && Port > 0 && Port <= 65535 && !string.IsNullOrWhiteSpace(Secret) && (IsServer || !string.IsNullOrWhiteSpace(Host));
    }

    public static class NetworkConfigurationService
    {
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapAfghanistan", "network.config");
        public static string PathName => ConfigPath;

        public static NetworkConfig Load()
        {
            if (!File.Exists(ConfigPath)) return new NetworkConfig();
            try
            {
                var lines = File.ReadAllLines(ConfigPath);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines)
                {
                    var index = line.IndexOf('='); if (index <= 0) continue;
                    map[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
                }
                int port; if (!int.TryParse(Value(map, "port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)) port = 47821;
                return new NetworkConfig
                {
                    Mode = Value(map, "mode"), Host = Value(map, "host"), Port = port,
                    Secret = Unprotect(Value(map, "secret"))
                };
            }
            catch { return new NetworkConfig(); }
        }

        public static void Save(NetworkConfig config)
        {
            if (config == null || !config.IsValid) throw new InvalidOperationException("تنظیم شبکه کامل نیست.");
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
            File.WriteAllLines(ConfigPath, new[]
            {
                "mode=" + config.Mode.ToLowerInvariant(),
                "host=" + (config.IsServer ? "0.0.0.0" : config.Host.Trim()),
                "port=" + config.Port.ToString(CultureInfo.InvariantCulture),
                "secret=" + Protect(config.Secret.Trim())
            });
        }

        public static void Clear() { try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); } catch { } }

        public static string GenerateSecret()
        {
            var bytes = new byte[18]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 20);
        }

        public static string SuggestedServerAddress()
        {
            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) return address.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        private static string Value(Dictionary<string, string> map, string key) { string value; return map.TryGetValue(key, out value) ? value : ""; }
        private static string Protect(string value)
        {
            var raw = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser));
        }
        private static string Unprotect(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var raw = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
    }

    internal sealed class RpcRequest { public string Operation { get; set; } = ""; public string Token { get; set; } = ""; public string Machine { get; set; } = ""; public string Payload { get; set; } = ""; }
    internal sealed class RpcResponse { public bool Ok { get; set; } public string Error { get; set; } = ""; public string Payload { get; set; } = ""; }
    internal sealed class AuthReply { public string Token { get; set; } = ""; public UserAccount User { get; set; } = new UserAccount(); public List<string> Permissions { get; set; } = new List<string>(); }
    internal sealed class AuthArgs { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
    internal sealed class FirstAdminArgs { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
    internal sealed class PasswordArgs { public string Current { get; set; } = ""; public string Next { get; set; } = ""; public string Username { get; set; } = ""; }
    internal sealed class IdArgs { public string Id { get; set; } = ""; }
    internal sealed class TypeArgs { public string Type { get; set; } = "همه"; }
    internal sealed class MemberSearchArgs { public string Search { get; set; } = ""; public string Type { get; set; } = "همه"; public string Status { get; set; } = "همه"; public int Page { get; set; } public int PageSize { get; set; } }
    internal sealed class CenterSearchArgs { public string Search { get; set; } = ""; public string Sector { get; set; } = "همه"; public string Status { get; set; } = "همه"; public int Page { get; set; } public int PageSize { get; set; } }
    internal sealed class MemberPageDto { public List<MemberListItem> Items { get; set; } = new List<MemberListItem>(); public int Total { get; set; } public int Page { get; set; } public int PageSize { get; set; } }
    internal sealed class CenterPageDto { public List<CenterListItem> Items { get; set; } = new List<CenterListItem>(); public int Total { get; set; } public int Page { get; set; } public int PageSize { get; set; } }
    internal sealed class MemberEnvelope { public MemberRecord? Member { get; set; } public string AttachmentName { get; set; } = ""; public string AttachmentBase64 { get; set; } = ""; }
    internal sealed class MemberSaveArgs { public MemberRecord Member { get; set; } = new MemberRecord(); public string AttachmentName { get; set; } = ""; public string AttachmentBase64 { get; set; } = ""; }
    internal sealed class SectorArgs { public SectorItem Sector { get; set; } = new SectorItem(); }
    internal sealed class CenterArgs { public CenterRecord Center { get; set; } = new CenterRecord(); }
    internal sealed class SubscriptionArgs { public string CenterId { get; set; } = ""; public decimal Amount { get; set; } public string Start { get; set; } = ""; public string Due { get; set; } = ""; public bool Suspended { get; set; } }
    internal sealed class PaymentArgs { public string CenterId { get; set; } = ""; public decimal Amount { get; set; } public string Date { get; set; } = ""; public int Months { get; set; } public string Receipt { get; set; } = ""; public string Notes { get; set; } = ""; }
    internal sealed class PaymentUpdateArgs { public PaymentItem Payment { get; set; } = new PaymentItem(); public decimal Amount { get; set; } public string Date { get; set; } = ""; public int Months { get; set; } public string Receipt { get; set; } = ""; public string Notes { get; set; } = ""; }
    internal sealed class PaymentListArgs { public string CenterId { get; set; } = ""; public int Limit { get; set; } = 500; }
    internal sealed class NoteArgs { public NoteItem Note { get; set; } = new NoteItem(); }
    internal sealed class ReportArgs { public string Key { get; set; } = ""; public string MemberType { get; set; } = "همه"; }
    internal sealed class TableDto { public List<string> Columns { get; set; } = new List<string>(); public List<List<string>> Rows { get; set; } = new List<List<string>>(); }
    internal sealed class UserCreateArgs { public string Username { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Role { get; set; } = "employee"; public string Password { get; set; } = ""; }
    internal sealed class UserUpdateArgs { public string Id { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Role { get; set; } = "employee"; public bool Active { get; set; } }
    internal sealed class ResetArgs { public string Id { get; set; } = ""; public string Password { get; set; } = ""; }
    internal sealed class PermissionArgs { public string Id { get; set; } = ""; public string Permission { get; set; } = ""; public bool Allowed { get; set; } }
    internal sealed class SettingsArgs { public AppSettingsRecord Settings { get; set; } = new AppSettingsRecord(); }
    internal sealed class ArchiveArgs { public ArchivedItem Item { get; set; } = new ArchivedItem(); }
    internal sealed class TrashArgs { public TrashItem Item { get; set; } = new TrashItem(); }
    internal sealed class BoolArgs { public bool Value { get; set; } }

    public sealed class LanServerHost : IDisposable
    {
        private sealed class ServerSession { public UserSession Session = null!; public DateTime ExpiresUtc; }
        private readonly NetworkConfig _config;
        private readonly LocalSnapService _service;
        private readonly JavaScriptSerializer _json = NewSerializer();
        private readonly Dictionary<string, ServerSession> _sessions = new Dictionary<string, ServerSession>(StringComparer.Ordinal);
        private readonly object _sessionLock = new object();
        private TcpListener? _listener;
        private Thread? _thread;
        private volatile bool _stopping;

        public LanServerHost(NetworkConfig config, LocalSnapService service) { _config = config; _service = service; }
        public bool IsRunning => _listener != null && !_stopping;

        public void Start()
        {
            if (IsRunning) return;
            _stopping = false;
            _listener = new TcpListener(IPAddress.Any, _config.Port);
            _listener.Start(50);
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "Snap LAN Server" };
            _thread.Start();
        }

        private void AcceptLoop()
        {
            while (!_stopping)
            {
                try
                {
                    var client = _listener?.AcceptTcpClient();
                    if (client != null) ThreadPool.QueueUserWorkItem(_ => Handle(client));
                }
                catch (SocketException) { if (!_stopping) Thread.Sleep(100); }
                catch { if (!_stopping) Thread.Sleep(100); }
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 30000; client.SendTimeout = 30000;
                try
                {
                    var requestJson = SecureWire.Read(client.GetStream(), _config.Secret);
                    var request = _json.Deserialize<RpcRequest>(requestJson) ?? new RpcRequest();
                    var response = Dispatch(request);
                    SecureWire.Write(client.GetStream(), _config.Secret, _json.Serialize(response));
                }
                catch (Exception ex)
                {
                    try { SecureWire.Write(client.GetStream(), _config.Secret, _json.Serialize(new RpcResponse { Ok = false, Error = Friendly(ex) })); } catch { }
                }
                finally { SessionContext.End(); }
            }
        }

        private RpcResponse Dispatch(RpcRequest request)
        {
            try
            {
                if (request.Operation == "ping") return Ok("Snap Afghanistan 1.4");
                if (request.Operation == "configured") return Ok(_service.IsConfigured());
                if (request.Operation == "suggestedUser") return Ok(_service.SuggestedUsername);
                if (request.Operation == "firstAdmin")
                {
                    var a = Arg<FirstAdminArgs>(request); _service.CreateFirstAdministrator(a.Username, a.Password); return Ok(true);
                }
                if (request.Operation == "login")
                {
                    var a = Arg<AuthArgs>(request);
                    var session = _service.Authenticate(a.Username, a.Password);
                    var token = CreateToken();
                    var clientSession = new UserSession(session.User, session.Permissions, request.Machine);
                    lock (_sessionLock) _sessions[token] = new ServerSession { Session = clientSession, ExpiresUtc = DateTime.UtcNow.AddHours(12) };
                    SessionContext.End();
                    return Ok(new AuthReply { Token = token, User = clientSession.User, Permissions = clientSession.Permissions.ToList() });
                }

                StartRequestSession(request);
                switch (request.Operation)
                {
                    case "changePassword": { var a = Arg<PasswordArgs>(request); _service.ChangeOwnPassword(a.Current, a.Next, a.Username); return Ok(true); }
                    case "dashboard": return Ok(_service.GetDashboard());
                    case "revenue": return Ok(_service.GetRevenueTrend(Arg<IdArgs>(request).Id.Length == 0 ? 6 : Convert.ToInt32(Arg<IdArgs>(request).Id, CultureInfo.InvariantCulture)));
                    case "members.search": { var a = Arg<MemberSearchArgs>(request); var p = _service.SearchMembers(a.Search, a.Type, a.Status, a.Page, a.PageSize); return Ok(new MemberPageDto { Items = p.Items.ToList(), Total = p.Total, Page = p.Page, PageSize = p.PageSize }); }
                    case "member.get": return Ok(MemberForTransport(Arg<IdArgs>(request).Id));
                    case "member.save": return Ok(SaveMember(Arg<MemberSaveArgs>(request)));
                    case "member.archive": _service.ArchiveMember(Arg<IdArgs>(request).Id); return Ok(true);
                    case "member.delete": _service.DeleteMember(Arg<IdArgs>(request).Id); return Ok(true);
                    case "sectors": return Ok(_service.GetSectors(Arg<BoolArgs>(request).Value));
                    case "sector.save": return Ok(_service.SaveSector(Arg<SectorArgs>(request).Sector));
                    case "sector.archive": _service.ArchiveSector(Arg<IdArgs>(request).Id); return Ok(true);
                    case "sector.delete": _service.DeleteSector(Arg<IdArgs>(request).Id); return Ok(true);
                    case "centers.search": { var a = Arg<CenterSearchArgs>(request); var p = _service.SearchCenters(a.Search, a.Sector, a.Status, a.Page, a.PageSize); return Ok(new CenterPageDto { Items = p.Items.ToList(), Total = p.Total, Page = p.Page, PageSize = p.PageSize }); }
                    case "center.get": return Ok(_service.GetCenter(Arg<IdArgs>(request).Id));
                    case "center.save": return Ok(_service.SaveCenter(Arg<CenterArgs>(request).Center));
                    case "center.archive": _service.ArchiveCenter(Arg<IdArgs>(request).Id); return Ok(true);
                    case "center.delete": _service.DeleteCenter(Arg<IdArgs>(request).Id); return Ok(true);
                    case "subscription.configure": { var a = Arg<SubscriptionArgs>(request); _service.ConfigureSubscription(a.CenterId, a.Amount, ParseDate(a.Start), ParseDate(a.Due), a.Suspended); return Ok(true); }
                    case "payments": { var a = Arg<PaymentListArgs>(request); return Ok(_service.GetPayments(a.CenterId, a.Limit)); }
                    case "payment.add": { var a = Arg<PaymentArgs>(request); return Ok(_service.RegisterPayment(a.CenterId, a.Amount, ParseDate(a.Date), a.Months, a.Receipt, a.Notes)); }
                    case "payment.update": { var a = Arg<PaymentUpdateArgs>(request); _service.UpdatePayment(a.Payment, a.Amount, ParseDate(a.Date), a.Months, a.Receipt, a.Notes); return Ok(true); }
                    case "payment.delete": _service.DeletePayment(Arg<PaymentUpdateArgs>(request).Payment); return Ok(true);
                    case "archive.get": return Ok(_service.GetArchived(Arg<TypeArgs>(request).Type));
                    case "archive.count": return Ok(_service.CountArchived(Arg<TypeArgs>(request).Type));
                    case "archive.restore": _service.RestoreArchived(Arg<ArchiveArgs>(request).Item); return Ok(true);
                    case "archive.trash": _service.MoveArchivedToTrash(Arg<ArchiveArgs>(request).Item); return Ok(true);
                    case "notes": return Ok(_service.GetNotes(Arg<TypeArgs>(request).Type));
                    case "note.save": return Ok(_service.SaveNote(Arg<NoteArgs>(request).Note));
                    case "note.delete": _service.DeleteNote(Arg<IdArgs>(request).Id); return Ok(true);
                    case "trash": return Ok(_service.GetTrash());
                    case "trash.restore": _service.RestoreTrash(Arg<TrashArgs>(request).Item); return Ok(true);
                    case "trash.purge": _service.PermanentlyDeleteTrash(Arg<TrashArgs>(request).Item); return Ok(true);
                    case "settings": return Ok(_service.GetSettings());
                    case "settings.save": _service.SaveSettings(Arg<SettingsArgs>(request).Settings); return Ok(true);
                    case "report": { var a = Arg<ReportArgs>(request); return Ok(ToTable(_service.BuildReport(a.Key, a.MemberType))); }
                    case "users": return Ok(_service.GetUsers());
                    case "user.create": { var a = Arg<UserCreateArgs>(request); return Ok(_service.CreateUser(a.Username, a.DisplayName, a.Role, a.Password)); }
                    case "user.update": { var a = Arg<UserUpdateArgs>(request); _service.UpdateUser(a.Id, a.DisplayName, a.Role, a.Active); return Ok(true); }
                    case "user.reset": { var a = Arg<ResetArgs>(request); _service.ResetPassword(a.Id, a.Password); return Ok(true); }
                    case "permissions": return Ok(_service.GetEffectivePermissions(Arg<IdArgs>(request).Id));
                    case "permission.set": { var a = Arg<PermissionArgs>(request); _service.SetPermissionOverride(a.Id, a.Permission, a.Allowed); return Ok(true); }
                    case "permission.clear": _service.ClearPermissionOverrides(Arg<IdArgs>(request).Id); return Ok(true);
                    default: throw new InvalidOperationException("درخواست شبکه ناشناخته است.");
                }
            }
            catch (Exception ex) { return new RpcResponse { Ok = false, Error = Friendly(ex) }; }
        }

        private void StartRequestSession(RpcRequest request)
        {
            ServerSession stored;
            lock (_sessionLock)
            {
                if (!_sessions.TryGetValue(request.Token ?? "", out stored!) || stored.ExpiresUtc <= DateTime.UtcNow)
                {
                    if (!string.IsNullOrWhiteSpace(request.Token)) _sessions.Remove(request.Token);
                    throw new UnauthorizedAccessException("جلسه شبکه پایان یافته است؛ دوباره وارد شوید.");
                }
                stored.ExpiresUtc = DateTime.UtcNow.AddHours(12);
            }
            SessionContext.Start(new UserSession(stored.Session.User, stored.Session.Permissions, request.Machine));
        }

        private MemberEnvelope MemberForTransport(string id)
        {
            var record = _service.GetMember(id);
            if (record == null) return new MemberEnvelope();
            var envelope = new MemberEnvelope { Member = record, AttachmentName = record.AttachmentName };
            if (!string.IsNullOrWhiteSpace(record.AttachmentPath) && File.Exists(record.AttachmentPath)) envelope.AttachmentBase64 = Convert.ToBase64String(File.ReadAllBytes(record.AttachmentPath));
            record.AttachmentPath = "";
            return envelope;
        }

        private string SaveMember(MemberSaveArgs args)
        {
            string temp = "";
            try
            {
                if (!string.IsNullOrWhiteSpace(args.AttachmentBase64))
                {
                    var extension = Path.GetExtension(args.AttachmentName); if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";
                    temp = Path.Combine(Path.GetTempPath(), "SnapUpload-" + Guid.NewGuid().ToString("N") + extension);
                    File.WriteAllBytes(temp, Convert.FromBase64String(args.AttachmentBase64));
                }
                return _service.SaveMember(args.Member, temp);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        private T Arg<T>(RpcRequest request) where T : new() => string.IsNullOrWhiteSpace(request.Payload) ? new T() : (_json.Deserialize<T>(request.Payload) ?? new T());
        private RpcResponse Ok(object value) => new RpcResponse { Ok = true, Payload = _json.Serialize(value) };
        private static string CreateToken() { var bytes = new byte[24]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); return Convert.ToBase64String(bytes); }
        private static DateTime ParseDate(string value) { DateTime date; if (!DateService.TryParseIso(value, out date)) throw new InvalidOperationException("تاریخ شبکه معتبر نیست."); return date; }
        private static string Friendly(Exception ex) { if (ex is UnauthorizedAccessException) return ex.Message; return UiMessages.Friendly(ex); }

        private static TableDto ToTable(DataTable table)
        {
            var dto = new TableDto(); foreach (DataColumn column in table.Columns) dto.Columns.Add(column.ColumnName);
            foreach (DataRow row in table.Rows) { var values = new List<string>(); foreach (DataColumn column in table.Columns) values.Add(Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? ""); dto.Rows.Add(values); }
            return dto;
        }

        public void Dispose()
        {
            _stopping = true;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            try { if (_thread != null && _thread.IsAlive) _thread.Join(1000); } catch { }
        }

        internal static JavaScriptSerializer NewSerializer() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 100 };
    }

    public sealed class RemoteSnapService : ISnapService
    {
        private readonly NetworkConfig _config;
        private readonly JavaScriptSerializer _json = LanServerHost.NewSerializer();
        private string _token = "";
        public RemoteSnapService(NetworkConfig config) { _config = config ?? throw new ArgumentNullException(nameof(config)); }
        public bool IsRemote => true;
        public bool Ping() { try { return Call<string>("ping", null, false).Length > 0; } catch { return false; } }
        public bool IsConfigured() => Call<bool>("configured", null, false);
        public string SuggestedUsername => Call<string>("suggestedUser", null, false);
        public void CreateFirstAdministrator(string username, string password) => Call<bool>("firstAdmin", new FirstAdminArgs { Username = username, Password = password }, false);

        public UserSession Authenticate(string username, string password)
        {
            var result = Call<AuthReply>("login", new AuthArgs { Username = username, Password = password }, false);
            _token = result.Token;
            var session = new UserSession(result.User, result.Permissions, Environment.MachineName);
            SessionContext.Start(session); return session;
        }

        public void ChangeOwnPassword(string currentPassword, string newPassword, string username) => Call<bool>("changePassword", new PasswordArgs { Current = currentPassword, Next = newPassword, Username = username });
        public DashboardStats GetDashboard() => Call<DashboardStats>("dashboard");
        public IReadOnlyList<RevenueTrendPoint> GetRevenueTrend(int months = 6) => Call<List<RevenueTrendPoint>>("revenue", new IdArgs { Id = months.ToString(CultureInfo.InvariantCulture) });

        public PagedResult<MemberListItem> SearchMembers(string search, string memberType, string status, int page, int pageSize)
        {
            var p = Call<MemberPageDto>("members.search", new MemberSearchArgs { Search = search, Type = memberType, Status = status, Page = page, PageSize = pageSize });
            return new PagedResult<MemberListItem> { Items = p.Items, Total = p.Total, Page = p.Page, PageSize = p.PageSize };
        }

        public MemberRecord? GetMember(string id)
        {
            var envelope = Call<MemberEnvelope>("member.get", new IdArgs { Id = id });
            if (envelope.Member == null) return null;
            envelope.Member.AttachmentName = envelope.AttachmentName;
            if (!string.IsNullOrWhiteSpace(envelope.AttachmentBase64))
            {
                var extension = Path.GetExtension(envelope.AttachmentName); if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";
                var folder = Path.Combine(Path.GetTempPath(), "SnapAfghanistan-ClientCache"); Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, id + extension); File.WriteAllBytes(path, Convert.FromBase64String(envelope.AttachmentBase64)); envelope.Member.AttachmentPath = path;
            }
            return envelope.Member;
        }

        public string SaveMember(MemberRecord member, string newAttachmentPath)
        {
            var a = new MemberSaveArgs { Member = member };
            if (!string.IsNullOrWhiteSpace(newAttachmentPath) && File.Exists(newAttachmentPath)) { a.AttachmentName = Path.GetFileName(newAttachmentPath); a.AttachmentBase64 = Convert.ToBase64String(File.ReadAllBytes(newAttachmentPath)); }
            return Call<string>("member.save", a);
        }

        public void ArchiveMember(string id) => Call<bool>("member.archive", new IdArgs { Id = id });
        public void DeleteMember(string id) => Call<bool>("member.delete", new IdArgs { Id = id });
        public IReadOnlyList<SectorItem> GetSectors(bool includeInactive = true) => Call<List<SectorItem>>("sectors", new BoolArgs { Value = includeInactive });
        public string SaveSector(SectorItem sector) => Call<string>("sector.save", new SectorArgs { Sector = sector });
        public void ArchiveSector(string id) => Call<bool>("sector.archive", new IdArgs { Id = id });
        public void DeleteSector(string id) => Call<bool>("sector.delete", new IdArgs { Id = id });

        public PagedResult<CenterListItem> SearchCenters(string search, string sectorId, string subscriptionStatus, int page, int pageSize)
        {
            var p = Call<CenterPageDto>("centers.search", new CenterSearchArgs { Search = search, Sector = sectorId, Status = subscriptionStatus, Page = page, PageSize = pageSize });
            return new PagedResult<CenterListItem> { Items = p.Items, Total = p.Total, Page = p.Page, PageSize = p.PageSize };
        }

        public CenterRecord? GetCenter(string id) => Call<CenterRecord>("center.get", new IdArgs { Id = id });
        public string SaveCenter(CenterRecord center) => Call<string>("center.save", new CenterArgs { Center = center });
        public void ArchiveCenter(string id) => Call<bool>("center.archive", new IdArgs { Id = id });
        public void DeleteCenter(string id) => Call<bool>("center.delete", new IdArgs { Id = id });
        public void ConfigureSubscription(string centerId, decimal amount, DateTime start, DateTime due, bool suspended) => Call<bool>("subscription.configure", new SubscriptionArgs { CenterId = centerId, Amount = amount, Start = DateService.Iso(start), Due = DateService.Iso(due), Suspended = suspended });
        public IReadOnlyList<PaymentItem> GetPayments(string centerId = "", int limit = 500) => Call<List<PaymentItem>>("payments", new PaymentListArgs { CenterId = centerId, Limit = limit });
        public string RegisterPayment(string centerId, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes) => Call<string>("payment.add", new PaymentArgs { CenterId = centerId, Amount = amount, Date = DateService.Iso(paymentDate), Months = coveredMonths, Receipt = receiptNo, Notes = notes });
        public void UpdatePayment(PaymentItem payment, decimal amount, DateTime paymentDate, int coveredMonths, string receiptNo, string notes) => Call<bool>("payment.update", new PaymentUpdateArgs { Payment = payment, Amount = amount, Date = DateService.Iso(paymentDate), Months = coveredMonths, Receipt = receiptNo, Notes = notes });
        public void DeletePayment(PaymentItem payment) => Call<bool>("payment.delete", new PaymentUpdateArgs { Payment = payment });
        public IReadOnlyList<ArchivedItem> GetArchived(string entityType = "همه") => Call<List<ArchivedItem>>("archive.get", new TypeArgs { Type = entityType });
        public long CountArchived(string entityType) => Call<long>("archive.count", new TypeArgs { Type = entityType });
        public void RestoreArchived(ArchivedItem item) => Call<bool>("archive.restore", new ArchiveArgs { Item = item });
        public void MoveArchivedToTrash(ArchivedItem item) => Call<bool>("archive.trash", new ArchiveArgs { Item = item });
        public IReadOnlyList<NoteItem> GetNotes(string status = "همه") => Call<List<NoteItem>>("notes", new TypeArgs { Type = status });
        public string SaveNote(NoteItem note) => Call<string>("note.save", new NoteArgs { Note = note });
        public void DeleteNote(string id) => Call<bool>("note.delete", new IdArgs { Id = id });
        public IReadOnlyList<TrashItem> GetTrash() => Call<List<TrashItem>>("trash");
        public void RestoreTrash(TrashItem item) => Call<bool>("trash.restore", new TrashArgs { Item = item });
        public void PermanentlyDeleteTrash(TrashItem item) => Call<bool>("trash.purge", new TrashArgs { Item = item });
        public AppSettingsRecord GetSettings() => Call<AppSettingsRecord>("settings");
        public void SaveSettings(AppSettingsRecord settings) => Call<bool>("settings.save", new SettingsArgs { Settings = settings });

        public DataTable BuildReport(string reportKey, string memberType = "همه")
        {
            var dto = Call<TableDto>("report", new ReportArgs { Key = reportKey, MemberType = memberType });
            var table = new DataTable(); foreach (var name in dto.Columns) table.Columns.Add(name, typeof(string));
            foreach (var row in dto.Rows) table.Rows.Add(row.Cast<object>().ToArray()); return table;
        }

        public IReadOnlyList<UserAccount> GetUsers() => Call<List<UserAccount>>("users");
        public UserAccount CreateUser(string username, string displayName, string role, string temporaryPassword) => Call<UserAccount>("user.create", new UserCreateArgs { Username = username, DisplayName = displayName, Role = role, Password = temporaryPassword });
        public void UpdateUser(string userId, string displayName, string role, bool isActive) => Call<bool>("user.update", new UserUpdateArgs { Id = userId, DisplayName = displayName, Role = role, Active = isActive });
        public void ResetPassword(string userId, string temporaryPassword) => Call<bool>("user.reset", new ResetArgs { Id = userId, Password = temporaryPassword });
        public IReadOnlyDictionary<string, bool> GetEffectivePermissions(string userId) => Call<Dictionary<string, bool>>("permissions", new IdArgs { Id = userId });
        public void SetPermissionOverride(string userId, string permission, bool allowed) => Call<bool>("permission.set", new PermissionArgs { Id = userId, Permission = permission, Allowed = allowed });
        public void ClearPermissionOverrides(string userId) => Call<bool>("permission.clear", new IdArgs { Id = userId });

        private T Call<T>(string operation, object? args = null, bool authenticated = true)
        {
            var request = new RpcRequest { Operation = operation, Token = authenticated ? _token : "", Machine = Environment.MachineName, Payload = args == null ? "" : _json.Serialize(args) };
            try
            {
                using (var client = new TcpClient())
                {
                    var connect = client.BeginConnect(_config.Host, _config.Port, null, null);
                    if (!connect.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4))) throw new TimeoutException();
                    client.EndConnect(connect); client.ReceiveTimeout = 30000; client.SendTimeout = 30000;
                    SecureWire.Write(client.GetStream(), _config.Secret, _json.Serialize(request));
                    var raw = SecureWire.Read(client.GetStream(), _config.Secret);
                    var response = _json.Deserialize<RpcResponse>(raw) ?? new RpcResponse { Ok = false, Error = "پاسخ شبکه معتبر نیست." };
                    if (!response.Ok) throw new InvalidOperationException(response.Error);
                    return _json.Deserialize<T>(response.Payload)!;
                }
            }
            catch (SocketException) { throw new InvalidOperationException("ارتباط با کامپیوتر اصلی برقرار نشد. شبکه دفتر و روشن بودن سرور را بررسی کنید."); }
            catch (TimeoutException) { throw new InvalidOperationException("کامپیوتر اصلی پاسخ نمی‌دهد. آدرس شبکه یا فایروال را بررسی کنید."); }
            catch (CryptographicException) { throw new InvalidOperationException("کلید اتصال این کامپیوتر با سرور یکسان نیست."); }
        }
    }

    internal static class SecureWire
    {
        public static void Write(NetworkStream stream, string secret, string text)
        {
            var plain = Encoding.UTF8.GetBytes(text ?? "");
            byte[] iv; byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = Key(secret, "enc"); aes.GenerateIV(); iv = aes.IV; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var encryptor = aes.CreateEncryptor()) cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }
            var signed = new byte[iv.Length + cipher.Length]; Buffer.BlockCopy(iv, 0, signed, 0, iv.Length); Buffer.BlockCopy(cipher, 0, signed, iv.Length, cipher.Length);
            byte[] mac; using (var hmac = new HMACSHA256(Key(secret, "mac"))) mac = hmac.ComputeHash(signed);
            var payload = new byte[signed.Length + mac.Length]; Buffer.BlockCopy(signed, 0, payload, 0, signed.Length); Buffer.BlockCopy(mac, 0, payload, signed.Length, mac.Length);
            if (payload.Length > 40 * 1024 * 1024) throw new InvalidOperationException("حجم درخواست شبکه بیش از حد مجاز است.");
            var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length)); stream.Write(length, 0, 4); stream.Write(payload, 0, payload.Length); stream.Flush();
        }

        public static string Read(NetworkStream stream, string secret)
        {
            var lengthBytes = ReadExact(stream, 4); var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));
            if (length < 49 || length > 40 * 1024 * 1024) throw new InvalidOperationException("بسته شبکه نامعتبر است.");
            var payload = ReadExact(stream, length); var signedLength = length - 32;
            var signed = new byte[signedLength]; var mac = new byte[32]; Buffer.BlockCopy(payload, 0, signed, 0, signedLength); Buffer.BlockCopy(payload, signedLength, mac, 0, 32);
            byte[] actual; using (var hmac = new HMACSHA256(Key(secret, "mac"))) actual = hmac.ComputeHash(signed);
            if (!ConstantEquals(mac, actual)) throw new CryptographicException("Network authentication failed.");
            var iv = new byte[16]; Buffer.BlockCopy(signed, 0, iv, 0, 16); var cipher = new byte[signedLength - 16]; Buffer.BlockCopy(signed, 16, cipher, 0, cipher.Length);
            byte[] plain; using (var aes = Aes.Create()) { aes.Key = Key(secret, "enc"); aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; using (var decryptor = aes.CreateDecryptor()) plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length); }
            return Encoding.UTF8.GetString(plain);
        }

        private static byte[] Key(string secret, string purpose) { using (var sha = SHA256.Create()) return sha.ComputeHash(Encoding.UTF8.GetBytes("SnapAfghanistan|1.4|" + purpose + "|" + secret)); }
        private static byte[] ReadExact(Stream stream, int count) { var result = new byte[count]; var offset = 0; while (offset < count) { var read = stream.Read(result, offset, count - offset); if (read <= 0) throw new EndOfStreamException(); offset += read; } return result; }
        private static bool ConstantEquals(byte[] a, byte[] b) { if (a.Length != b.Length) return false; var diff = 0; for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0; }
    }
}
