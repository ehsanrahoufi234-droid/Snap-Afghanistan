#define AppName "Snap Afghanistan"
#define AppVersion "1.3.0"
[Setup]
AppId={{47F26725-C9DA-42DD-A160-BFBA681D67E5}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Snap Afghanistan
DefaultGroupName=Snap Afghanistan
OutputDir=installer-output
OutputBaseFilename=SnapAfghanistan-Setup-1.3.0
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
VersionInfoDescription=Snap Afghanistan Desktop Management System
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
Filename: "{app}\SnapAfghanistan.exe"; Description: "اجرای اسنپ افغانستان"; Flags: nowait postinstall skipifsilent

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
