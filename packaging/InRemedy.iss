#define MyAppName "In-Remedy"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef SourceDir
  #error SourceDir must be defined on the command line.
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourceDir) + "..\installer"
#endif
#ifndef PostgresInstaller
  #error PostgresInstaller must be defined on the command line.
#endif
#ifndef WebViewInstaller
  #error WebViewInstaller must be defined on the command line.
#endif

[Setup]
AppId={{6D32E08A-08C9-46C0-905A-2D99F0A5CB67}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Bojan Crvenkovic
AppPublisherURL=mailto:crvenkovicbojan@gmail.com
DefaultDirName={localappdata}\Programs\In-Remedy
DefaultGroupName=In-Remedy
OutputDir={#OutputDir}
OutputBaseFilename=InRemedy-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#SourceDir}\InRemedy.ico
UninstallDisplayIcon={app}\InRemedy.ico
PrivilegesRequired=admin

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PostgresInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#WebViewInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\In-Remedy"; Filename: "{app}\InRemedy.Desktop.exe"; WorkingDir: "{app}"; IconFilename: "{app}\InRemedy.ico"
Name: "{autodesktop}\In-Remedy"; Filename: "{app}\InRemedy.Desktop.exe"; WorkingDir: "{app}"; IconFilename: "{app}\InRemedy.ico"

[Run]
Filename: "{tmp}\{#emit ExtractFileName(WebViewInstaller)}"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft WebView2 runtime..."; Flags: waituntilterminated; Check: not IsWebView2Installed
Filename: "{tmp}\{#emit ExtractFileName(PostgresInstaller)}"; Parameters: "{code:GetPostgresInstallerParams}"; StatusMsg: "Installing bundled PostgreSQL..."; Flags: waituntilterminated; Check: not IsInRemedyPostgresInstalled and not ShouldUseExistingLocalPostgres
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Initialize-InRemedyDatabase.ps1"" -InstallRoot ""{app}"" -PgBinDir ""{code:GetPostgresBinDir}"" -AppUrl ""http://127.0.0.1:5180"" -DbHost ""{code:GetPostgresHost}"" -Port {code:GetPostgresPort} -SuperUser ""{code:GetPostgresSuperUser}"" -SuperPassword ""{code:GetPostgresSuperPassword}"" -ServiceName ""{code:GetPostgresServiceName}"" -AppUser ""inremedy_app"" -AppPassword ""InRemedy!2026Local"" -DatabaseName ""inremedy"""; StatusMsg: "Configuring the In-Remedy database..."; Flags: waituntilterminated runhidden
Filename: "{app}\InRemedy.Desktop.exe"; Description: "Launch In-Remedy"; Flags: nowait postinstall skipifsilent

[Code]
var
  ExistingPgDetected: Boolean;
  ExistingPgConfigPage: TWizardPage;
  UseExistingRadio: TRadioButton;
  UseBundledRadio: TRadioButton;
  PgHostLabel: TNewStaticText;
  PgPortLabel: TNewStaticText;
  PgUserLabel: TNewStaticText;
  PgPasswordLabel: TNewStaticText;
  PgHostEdit: TNewEdit;
  PgPortEdit: TNewEdit;
  PgUserEdit: TNewEdit;
  PgPasswordEdit: TPasswordEdit;
  ExistingPgBaseDir: string;
  ExistingPgServiceId: string;
  ExistingPgHost: string;
  ExistingPgPort: string;
  ExistingPgUser: string;
  ExistingPgPassword: string;

function IsWebView2Installed: Boolean;
begin
  Result :=
    RegValueExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv') or
    RegValueExists(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv');
end;

function TryLoadExistingPostgresInstallation: Boolean;
var
  SubKeys: TArrayOfString;
  Index: Integer;
  KeyName: string;
begin
  Result := False;
  ExistingPgBaseDir := '';
  ExistingPgServiceId := '';
  ExistingPgHost := '127.0.0.1';
  ExistingPgPort := '5432';
  ExistingPgUser := 'postgres';
  ExistingPgPassword := 'postgres';

  if not RegGetSubkeyNames(HKLM64, 'SOFTWARE\PostgreSQL\Installations', SubKeys) then
  begin
    Exit;
  end;

  for Index := 0 to GetArrayLength(SubKeys) - 1 do
  begin
    KeyName := SubKeys[Index];
    if CompareText(KeyName, 'postgresql-x64-17-inremedy') = 0 then
    begin
      Continue;
    end;

    if not RegQueryStringValue(HKLM64, 'SOFTWARE\PostgreSQL\Installations\' + KeyName, 'Base Directory', ExistingPgBaseDir) then
    begin
      Continue;
    end;

    if not RegQueryStringValue(HKLM64, 'SOFTWARE\PostgreSQL\Installations\' + KeyName, 'Service ID', ExistingPgServiceId) then
    begin
      ExistingPgServiceId := '';
    end;

    Result := True;
    Exit;
  end;
end;

function IsInRemedyPostgresInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SOFTWARE\PostgreSQL\Installations\postgresql-x64-17-inremedy');
end;

procedure UpdateExistingPgInputsEnabled();
begin
  if not ExistingPgDetected then
  begin
    Exit;
  end;

  PgHostLabel.Enabled := UseExistingRadio.Checked;
  PgPortLabel.Enabled := UseExistingRadio.Checked;
  PgUserLabel.Enabled := UseExistingRadio.Checked;
  PgPasswordLabel.Enabled := UseExistingRadio.Checked;
  PgHostEdit.Enabled := UseExistingRadio.Checked;
  PgPortEdit.Enabled := UseExistingRadio.Checked;
  PgUserEdit.Enabled := UseExistingRadio.Checked;
  PgPasswordEdit.Enabled := UseExistingRadio.Checked;
end;

procedure ExistingPgOptionClicked(Sender: TObject);
begin
  UpdateExistingPgInputsEnabled();
end;

procedure InitializeWizard();
begin
  ExistingPgDetected := TryLoadExistingPostgresInstallation() and not IsInRemedyPostgresInstalled();
  if not ExistingPgDetected then
  begin
    Exit;
  end;

  ExistingPgConfigPage :=
    CreateCustomPage(
      wpSelectDir,
      'Database setup',
      'Choose whether to use an existing local PostgreSQL installation or install a dedicated In-Remedy instance.');

  UseExistingRadio := TRadioButton.Create(ExistingPgConfigPage);
  UseExistingRadio.Parent := ExistingPgConfigPage.Surface;
  UseExistingRadio.Caption := 'Use existing local PostgreSQL';
  UseExistingRadio.Checked := True;
  UseExistingRadio.Left := ScaleX(0);
  UseExistingRadio.Top := ScaleY(8);
  UseExistingRadio.Width := ExistingPgConfigPage.SurfaceWidth;
  UseExistingRadio.OnClick := @ExistingPgOptionClicked;

  UseBundledRadio := TRadioButton.Create(ExistingPgConfigPage);
  UseBundledRadio.Parent := ExistingPgConfigPage.Surface;
  UseBundledRadio.Caption := 'Install dedicated PostgreSQL for In-Remedy';
  UseBundledRadio.Left := ScaleX(0);
  UseBundledRadio.Top := UseExistingRadio.Top + ScaleY(24);
  UseBundledRadio.Width := ExistingPgConfigPage.SurfaceWidth;
  UseBundledRadio.OnClick := @ExistingPgOptionClicked;

  PgHostLabel := TNewStaticText.Create(ExistingPgConfigPage);
  PgHostLabel.Parent := ExistingPgConfigPage.Surface;
  PgHostLabel.Caption := 'Host';
  PgHostLabel.Left := ScaleX(0);
  PgHostLabel.Top := UseBundledRadio.Top + ScaleY(36);

  PgHostEdit := TNewEdit.Create(ExistingPgConfigPage);
  PgHostEdit.Parent := ExistingPgConfigPage.Surface;
  PgHostEdit.Left := ScaleX(0);
  PgHostEdit.Top := PgHostLabel.Top + ScaleY(16);
  PgHostEdit.Width := ExistingPgConfigPage.SurfaceWidth div 2 - ScaleX(8);
  PgHostEdit.Text := ExistingPgHost;

  PgPortLabel := TNewStaticText.Create(ExistingPgConfigPage);
  PgPortLabel.Parent := ExistingPgConfigPage.Surface;
  PgPortLabel.Caption := 'Port';
  PgPortLabel.Left := PgHostEdit.Left + PgHostEdit.Width + ScaleX(16);
  PgPortLabel.Top := PgHostLabel.Top;

  PgPortEdit := TNewEdit.Create(ExistingPgConfigPage);
  PgPortEdit.Parent := ExistingPgConfigPage.Surface;
  PgPortEdit.Left := PgPortLabel.Left;
  PgPortEdit.Top := PgPortLabel.Top + ScaleY(16);
  PgPortEdit.Width := ExistingPgConfigPage.SurfaceWidth div 2 - ScaleX(8);
  PgPortEdit.Text := ExistingPgPort;

  PgUserLabel := TNewStaticText.Create(ExistingPgConfigPage);
  PgUserLabel.Parent := ExistingPgConfigPage.Surface;
  PgUserLabel.Caption := 'Admin user';
  PgUserLabel.Left := ScaleX(0);
  PgUserLabel.Top := PgHostEdit.Top + ScaleY(34);

  PgUserEdit := TNewEdit.Create(ExistingPgConfigPage);
  PgUserEdit.Parent := ExistingPgConfigPage.Surface;
  PgUserEdit.Left := ScaleX(0);
  PgUserEdit.Top := PgUserLabel.Top + ScaleY(16);
  PgUserEdit.Width := ExistingPgConfigPage.SurfaceWidth div 2 - ScaleX(8);
  PgUserEdit.Text := ExistingPgUser;

  PgPasswordLabel := TNewStaticText.Create(ExistingPgConfigPage);
  PgPasswordLabel.Parent := ExistingPgConfigPage.Surface;
  PgPasswordLabel.Caption := 'Admin password';
  PgPasswordLabel.Left := PgUserEdit.Left + PgUserEdit.Width + ScaleX(16);
  PgPasswordLabel.Top := PgUserLabel.Top;

  PgPasswordEdit := TPasswordEdit.Create(ExistingPgConfigPage);
  PgPasswordEdit.Parent := ExistingPgConfigPage.Surface;
  PgPasswordEdit.Left := PgPasswordLabel.Left;
  PgPasswordEdit.Top := PgPasswordLabel.Top + ScaleY(16);
  PgPasswordEdit.Width := ExistingPgConfigPage.SurfaceWidth div 2 - ScaleX(8);
  PgPasswordEdit.Text := ExistingPgPassword;

  UpdateExistingPgInputsEnabled();
end;

function ShouldUseExistingLocalPostgres: Boolean;
begin
  Result := ExistingPgDetected and UseExistingRadio.Checked;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if ExistingPgDetected and (CurPageID = ExistingPgConfigPage.ID) and UseExistingRadio.Checked then
  begin
    if Trim(PgHostEdit.Text) = '' then
    begin
      MsgBox('Enter the PostgreSQL host.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if Trim(PgPortEdit.Text) = '' then
    begin
      MsgBox('Enter the PostgreSQL port.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if Trim(PgUserEdit.Text) = '' then
    begin
      MsgBox('Enter the PostgreSQL admin user.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if Trim(PgPasswordEdit.Text) = '' then
    begin
      MsgBox('Enter the PostgreSQL admin password.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

function GetPostgresBinDir(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres and (Trim(ExistingPgBaseDir) <> '') then
  begin
    Result := AddBackslash(ExistingPgBaseDir) + 'bin';
  end
  else
  begin
    Result := ExpandConstant('{commonpf}\PostgreSQL\17-inremedy\bin');
  end;
end;

function GetPostgresHost(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres then
  begin
    Result := Trim(PgHostEdit.Text);
  end
  else
  begin
    Result := '127.0.0.1';
  end;
end;

function GetPostgresPort(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres then
  begin
    Result := Trim(PgPortEdit.Text);
  end
  else
  begin
    Result := '5433';
  end;
end;

function GetPostgresSuperUser(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres then
  begin
    Result := Trim(PgUserEdit.Text);
  end
  else
  begin
    Result := 'postgres';
  end;
end;

function GetPostgresSuperPassword(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres then
  begin
    Result := PgPasswordEdit.Text;
  end
  else
  begin
    Result := 'InRemedyPG!2026';
  end;
end;

function GetPostgresServiceName(Value: string): string;
begin
  if ShouldUseExistingLocalPostgres and (Trim(ExistingPgServiceId) <> '') then
  begin
    Result := ExistingPgServiceId;
  end
  else
  begin
    Result := 'postgresql-x64-17-inremedy';
  end;
end;

function GetPostgresInstallerParams(Value: string): string;
begin
  Result :=
    '--mode unattended ' +
    '--unattendedmodeui none ' +
    '--superaccount postgres ' +
    '--superpassword "InRemedyPG!2026" ' +
    '--servicepassword "InRemedyPG!2026" ' +
    '--serverport 5433 ' +
    '--servicename postgresql-x64-17-inremedy ' +
    '--prefix "' + ExpandConstant('{commonpf}\PostgreSQL\17-inremedy') + '" ' +
    '--datadir "' + ExpandConstant('{commonappdata}\InRemedy\PostgreSQL\data') + '" ' +
    '--create_shortcuts 0 ' +
    '--enable-components server,commandlinetools ' +
    '--disable-components pgAdmin,stackbuilder';
end;
