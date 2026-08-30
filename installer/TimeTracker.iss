; TimeTracker Pro — instalador Inno Setup 6
; Pré-requisitos baixados em tempo de instalação (framework-dependent).

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

#define MyAppName "TimeTracker Pro"
#define MyAppPublisher "TimeTracker"
#define MyAppExeName "TimeTracker.exe"
#define MyAppId "{{A7C3E9F1-4B2D-4E8A-9C1F-6D5A8B0E2F34}"
; Deve coincidir com AppConstants.AppMutexName (sem prefixo Local/Global).
#define MyAppMutex "TimeTrackerPro-A7C3E9F1"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=TimeTrackerPro-{#MyAppVersion}-setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\assets\app.ico
; Nome sem Local/Global — Inno adiciona o prefixo conforme PrivilegesRequired.
AppMutex={#MyAppMutex}
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar ícone na área de trabalho"; GroupDescription: "Ícones adicionais:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Cache do WebView2 — irmão de data\ em LocalAppData\TimeTracker Pro\.
; Entrada data\WebView2 é limpeza do layout incorreto temporário.
[InstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}\WebView2"
Type: files; Name: "{localappdata}\{#MyAppName}\webview2-profile-version.txt"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}\data\WebView2"
Type: files; Name: "{localappdata}\{#MyAppName}\data\webview2-profile-version.txt"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DesktopRuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  AspNetRuntimeUrl = 'https://aka.ms/dotnet/8.0/aspnetcore-runtime-win-x64.exe';

function SharedFrameworkInstalled(const FrameworkName: string): Boolean;
var
  FindRec: TFindRec;
  SharedPath: string;
begin
  SharedPath := ExpandConstant('{commonpf64}\dotnet\shared\') + FrameworkName;
  Result := FindFirst(SharedPath + '\8.*', FindRec);
  if Result then
    FindClose(FindRec);
end;

function NeedsDesktopRuntime: Boolean;
begin
  Result := not SharedFrameworkInstalled('Microsoft.WindowsDesktop.App');
end;

function NeedsAspNetRuntime: Boolean;
begin
  Result := not SharedFrameworkInstalled('Microsoft.AspNetCore.App');
end;

function InstallRuntime(const Url, FileName, Title: string): Boolean;
var
  LocalFile: string;
  ResultCode: Integer;
begin
  Result := True;
  WizardForm.StatusLabel.Caption := 'Baixando ' + Title + '...';
  try
    DownloadTemporaryFile(Url, FileName, '', nil);
  except
    MsgBox('Falha ao baixar ' + Title + '.'#13#10 + GetExceptionMessage, mbError, MB_OK);
    Result := False;
    exit;
  end;

  LocalFile := ExpandConstant('{tmp}\') + FileName;
  WizardForm.StatusLabel.Caption := 'Instalando ' + Title + '...';
  if not Exec(LocalFile, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('Não foi possível executar o instalador de ' + Title + '.', mbError, MB_OK);
    Result := False;
    exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox(Title + ' falhou (código ' + IntToStr(ResultCode) + ').', mbError, MB_OK);
    Result := False;
  end;
end;

function KillRunningApp: Boolean;
var
  ResultCode: Integer;
begin
  { Rede de segurança: encerra o app antes de sobrescrever arquivos em {app}. }
  Exec('taskkill.exe', '/IM {#MyAppExeName} /F /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;

  KillRunningApp();

  if NeedsDesktopRuntime then
  begin
    if not InstallRuntime(DesktopRuntimeUrl, 'windowsdesktop-runtime-8-win-x64.exe', '.NET 8 Desktop Runtime') then
    begin
      Result := 'Instalação do .NET Desktop Runtime cancelada ou falhou.';
      exit;
    end;
  end;

  if NeedsAspNetRuntime then
  begin
    if not InstallRuntime(AspNetRuntimeUrl, 'aspnetcore-runtime-8-win-x64.exe', '.NET 8 ASP.NET Core Runtime') then
    begin
      Result := 'Instalação do ASP.NET Core Runtime cancelada ou falhou.';
      exit;
    end;
  end;
end;
