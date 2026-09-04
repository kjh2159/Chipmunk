#define MyAppName "Chipmunk"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "Chipmunk contributors"
#define MyAppExeName "Chipmunk.exe"
#define PawnIoVersion "2.2.0"
#define PawnIoSha256 "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032"
#define PawnIoInstaller AddBackslash(SourcePath) + "dependencies\PawnIO_setup.exe"
#ifndef AppPayloadDir
  #define AppPayloadDir AddBackslash(SourcePath) + "..\artifacts\portable"
#endif

; Refuse to create a distributable installer if the bundled kernel-driver
; installer is missing or differs from the reviewed official binary.
#if VER < EncodeVer(6,4,0)
  #error Inno Setup 6.4 or newer is required for compile-time SHA-256 verification.
#endif
#ifnexist PawnIoInstaller
  #error PawnIO installer is missing. Run scripts\fetch-pawnio.ps1 first.
#endif
#if LowerCase(GetSHA256OfFile(PawnIoInstaller)) != LowerCase(PawnIoSha256)
  #error PawnIO installer SHA-256 does not match the pinned official value.
#endif

[Setup]
AppId={{D5A9D761-99B6-4AB6-8E57-EBC13867D9A9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Chipmunk
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\Chipmunk\Assets\chipmunk.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Chipmunk-Setup-x64
LicenseFile=..\LICENSE

[Tasks]
Name: "autostart"; Description: "Start automatically when signing in to Windows"; GroupDescription: "Additional tasks:"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked
Name: "pawnio"; Description: "Install the signed PawnIO {#PawnIoVersion} kernel driver for CPU temperature sensors (optional; requires a separate UAC approval)"; GroupDescription: "Optional hardware access:"; Flags: unchecked; Check: PawnIoNotInstalled

[Files]
Source: "{#AppPayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Chipmunk"; ValueData: """{app}\{#MyAppExeName}"" --startup"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  PawnIoInstallAttempted: Boolean;
  PawnIoDefaultSelectionApplied: Boolean;

function IsIntelCpu(): Boolean;
var
  VendorIdentifier: String;
begin
  { Windows exposes the processor vendor without requiring WMI or elevation. }
  Result :=
    RegQueryStringValue(
      HKLM64,
      'HARDWARE\DESCRIPTION\System\CentralProcessor\0',
      'VendorIdentifier',
      VendorIdentifier) and
    (CompareText(Trim(VendorIdentifier), 'GenuineIntel') = 0);

  if Result then
    Log('Intel CPU detected; PawnIO will be selected by default.')
  else
    Log('Intel CPU was not detected; PawnIO remains optional and unchecked.');
end;

function PawnIoNotInstalled(): Boolean;
begin
  Result :=
    (not RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO')) and
    (not RegKeyExists(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'));
end;

function VerifyPawnIoInstaller(): Boolean;
var
  InstallerPath: String;
  ActualHash: String;
begin
  InstallerPath := ExpandConstant('{app}\Dependencies\PawnIO_setup.exe');
  if not FileExists(InstallerPath) then
  begin
    MsgBox(
      'PawnIO was not started because its installer file is missing.',
      mbCriticalError,
      MB_OK);
    Result := False;
    Exit;
  end;

  ActualHash := GetSHA256OfFile(InstallerPath);
  Result := CompareText(ActualHash, '{#PawnIoSha256}') = 0;
  if not Result then
    MsgBox(
      'PawnIO was not started because its SHA-256 does not match the pinned official value.',
      mbCriticalError,
      MB_OK);
end;

procedure InstallOptionalPawnIo();
var
  ResultCode: Integer;
  Started: Boolean;
begin
  if PawnIoInstallAttempted or
     (not WizardIsTaskSelected('pawnio')) or
     (not PawnIoNotInstalled()) then
    Exit;

  PawnIoInstallAttempted := True;
  if not VerifyPawnIoInstaller() then
    Exit;

  WizardForm.StatusLabel.Caption :=
    'Waiting for approval to install the optional signed PawnIO driver...';
  Started := ShellExec(
    'runas',
    ExpandConstant('{app}\Dependencies\PawnIO_setup.exe'),
    '-install -silent',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  if not Started then
  begin
    MsgBox(
      'PawnIO was not installed. CPU temperature may remain N/A. ' +
      'The Chipmunk installation will continue.',
      mbInformation,
      MB_OK);
    Exit;
  end;

  { ShellExec reports launch errors through ResultCode, but unlike Exec it does
    not expose the elevated child process exit code. Verify the documented
    machine-wide installation record after the child process finishes. }
  if PawnIoNotInstalled() then
    MsgBox(
      'PawnIO did not report a completed installation. ' +
      'Chipmunk will continue without low-level CPU sensors.',
      mbInformation,
      MB_OK)
  else
    Log('PawnIO {#PawnIoVersion} installed successfully.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallOptionalPawnIo();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { Apply the default only once. Returning to the task page must not override a
    user's explicit decision to clear the optional PawnIO checkbox. }
  if (CurPageID = wpSelectTasks) and (not PawnIoDefaultSelectionApplied) then
  begin
    PawnIoDefaultSelectionApplied := True;
    if PawnIoNotInstalled() and IsIntelCpu() then
      WizardSelectTasks('pawnio');
  end;
end;
