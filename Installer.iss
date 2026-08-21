#define AppName "Snap Afghanistan"
#define AppVersion "1.4.0"
[Setup]
AppId={{47F26725-C9DA-42DD-A160-BFBA681D67E5}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Snap Afghanistan
DefaultGroupName=Snap Afghanistan
OutputDir=installer-output
OutputBaseFilename=SnapAfghanistan-Setup-1.4.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=Assets\snap.ico
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\SnapAfghanistan.exe
VersionInfoVersion={#AppVersion}
VersionInfoCompany=Snap Afghanistan
VersionInfoDescription=Snap Afghanistan Desktop Multi-User Management System
AppPublisher=Snap Afghanistan
AppPublisherURL=https://github.com/ehsanrahoufi234-droid/Snap-Afghanistan
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Snap Afghanistan"; Filename: "{app}\SnapAfghanistan.exe"
Name: "{autodesktop}\Snap Afghanistan"; Filename: "{app}\SnapAfghanistan.exe"; Tasks: desktopicon
[Tasks]
Name: desktopicon; Description: "ایجاد میان‌بر اسنپ افغانستان روی Desktop"; Flags: checkedonce
[Run]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=\"Snap Afghanistan LAN\""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=\"Snap Afghanistan LAN\" dir=in action=allow protocol=TCP localport=47821 profile=private"; Flags: runhidden
Filename: "{app}\SnapAfghanistan.exe"; Description: "اجرای اسنپ افغانستان"; Flags: nowait postinstall skipifsilent runasoriginaluser
[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=\"Snap Afghanistan LAN\""; Flags: runhidden

[Code]
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNet48Installed;
  if not Result then
    MsgBox('برای اجرای اسنپ افغانستان، Microsoft .NET Framework 4.8 باید روی ویندوز نصب باشد.', mbError, MB_OK);
end;
