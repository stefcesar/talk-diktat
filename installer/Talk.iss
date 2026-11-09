; Installer für Talk (Inno Setup 6 oder neuer). Gebaut wird über installer\baue-setup.ps1, das PublishDir, OutputDir und AppVersion übergibt.
; Installation pro Benutzer, ohne Administratorrechte, Programm als Ordner (Playbook Abschnitt 4 Punkt 11).
; Die Daten des Nutzers unter %LOCALAPPDATA%\Talk (Einstellungen, Verlauf, Wörterlisten, Protokoll, Modelle) fasst der Installer nie an,
; auch nicht beim Entfernen. Einzige Ausnahme ist die Variante "Komplett" (Schalter /DKomplett, für die Weitergabe auf einem USB Stick):
; Sie legt die beiden Modelle dort ab, aber nur, wenn sie noch fehlen, und lässt sie beim Entfernen liegen. Außerdem installiert sie die
; Microsoft Visual C++ Laufzeit, wenn sie fehlt oder zu alt ist; nur dafür fragt Windows nach Administratorrechten (Playbook Entscheid 70).
; Sie legt außerdem die Kurzanleitung Liesmich.txt in den Programmordner.

#ifndef PublishDir
  #error PublishDir fehlt. Bitte installer\baue-setup.ps1 benutzen.
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef AppVersion
  #define AppVersion "0.4.1"
#endif
#ifdef Komplett
  #ifndef ModelsDir
    #error ModelsDir fehlt. Bitte installer\baue-setup.ps1 -Komplett benutzen.
  #endif
  #ifndef VcRedist
    #error VcRedist fehlt. Bitte installer\baue-setup.ps1 -Komplett benutzen.
  #endif
#endif

[Setup]
AppId={{7B0D5C6E-3A1F-4E52-9C1B-5F2E8D6A4B01}
AppName=Talk
AppVersion={#AppVersion}
AppPublisher=Stefano Cesaretti
AppCopyright=© 2026 Stefano Cesaretti
AppPublisherURL=https://github.com/stefcesar/talk-diktat
AppSupportURL=https://github.com/stefcesar/talk-diktat/issues
VersionInfoCopyright=© 2026 Stefano Cesaretti
DefaultDirName={localappdata}\Programs\Talk
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir={#OutputDir}
#ifdef Komplett
OutputBaseFilename=TalkSetup-{#AppVersion}-komplett
; Vor der Installation die Voraussetzungen, danach die Kurzanleitung. Beide Dateien sind UTF-8 mit BOM, sonst liest Inno Setup sie falsch.
InfoBeforeFile=Voraussetzungen.txt
InfoAfterFile=Liesmich.txt
#else
OutputBaseFilename=TalkSetup-{#AppVersion}
#endif
SetupIconFile=..\assets\symbol\fertig\talk.ico
UninstallDisplayIcon={app}\Talk.exe
UninstallDisplayName=Talk
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Läuft Talk beim Installieren, bietet der Installer an, es zu schließen. Sonst wären Dateien gesperrt.
CloseApplications=yes
RestartApplications=no
; Talk hält diesen Mutex, solange es im Tray läuft (App.xaml.cs, SingleInstanceName). Setup und Entfernen sagen dann klar:
; Talk läuft, bitte erst beenden. Ohne das blieben beim Entfernen gesperrte Dateien liegen und der Hook liefe weiter.
AppMutex=Local\Talk.SingleInstance
; Die Wahl beim Autostart wird nicht aus der letzten Installation übernommen: Wer den Haken später in Talk entfernt hat,
; bekommt ihn bei einer Aktualisierung nicht stillschweigend zurück.
UsePreviousTasks=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "autostart"; Description: "Talk mit Windows starten"; GroupDescription: "Start:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Talk.Core.xml,createdump.exe"; Flags: recursesubdirs createallsubdirs ignoreversion
; Lizenztexte der mitgelieferten Bestandteile (MIT, SIL OFL, .NET).
Source: "Lizenzen\*"; DestDir: "{app}\Lizenzen"; Flags: ignoreversion
#ifdef Komplett
; Die Modelle nur, wenn sie fehlen: Vorhandene (auch andere, größere) bleiben unberührt. Beim Entfernen bleiben sie liegen wie alle Daten.
Source: "{#ModelsDir}\ggml-silero-v6.2.0.bin"; DestDir: "{localappdata}\Talk\models"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#ModelsDir}\ggml-large-v3-turbo-q5_0.bin"; DestDir: "{localappdata}\Talk\models"; Flags: onlyifdoesntexist uninsneveruninstall
; Nach {tmp}, das Inno Setup beim Beenden selbst leert. Ausgeführt wird sie in [Code] (InstallVcRedist), damit der Rückgabewert zählt.
Source: "{#VcRedist}"; DestDir: "{tmp}"; DestName: "vc_redist.x64.exe"; Check: VcRedistNeeded
; Die Kurzanleitung bleibt auch nach dem Setup im Programmordner.
Source: "Liesmich.txt"; DestDir: "{app}"; Flags: ignoreversion
#endif

[Icons]
Name: "{autoprograms}\Talk"; Filename: "{app}\Talk.exe"

[Registry]
; Derselbe Eintrag, den der Haken "Talk mit Windows starten" in den Einstellungen setzt. Beim Entfernen von Talk verschwindet er.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Talk"; ValueData: """{app}\Talk.exe"""; Tasks: autostart; Flags: uninsdeletevalue
; Auch ein Eintrag, den der Nutzer erst später in Talk gesetzt hat, verschwindet beim Entfernen. Sonst zeigte er auf eine gelöschte Datei.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Talk"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\Talk.exe"; Description: "Talk jetzt starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Nur der Programmordner. %LOCALAPPDATA%\Talk bleibt stehen.
Type: dirifempty; Name: "{app}"

#ifdef Komplett
[Code]
var
  RestartNeeded: Boolean;

// True, wenn die Microsoft Visual C++ Laufzeit (x64) fehlt oder älter als 14.44 ist. Die Bibliotheken von whisper.cpp sind mit dem
// Toolset 14.44 gebaut, und Microsoft verlangt eine Laufzeit, die mindestens so neu ist.
function VcRedistNeeded: Boolean;
var
  Key: String;
  Installed, Major, Minor: Cardinal;
begin
  Result := True;
  Key := 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
  if not RegQueryDWordValue(HKLM64, Key, 'Installed', Installed) then Exit;
  if Installed <> 1 then Exit;
  if not RegQueryDWordValue(HKLM64, Key, 'Major', Major) then Exit;
  if not RegQueryDWordValue(HKLM64, Key, 'Minor', Minor) then Exit;
  Result := (Major < 14) or ((Major = 14) and (Minor < 44));
end;

// Führt die Laufzeit nach dem Kopieren aus, vor "Talk jetzt starten". Windows fragt dafür nach Administratorrechten. Lehnt der Nutzer
// ab oder scheitert die Installation, sagt das Setup es; Talk bleibt installiert. 3010 heißt: erst nach einem Neustart wirksam.
procedure InstallVcRedist;
var
  ResultCode: Integer;
  Path: String;
begin
  Path := ExpandConstant('{tmp}\vc_redist.x64.exe');
  if not FileExists(Path) then Exit;
  WizardForm.StatusLabel.Caption := 'Die Microsoft Visual C++ Laufzeit wird installiert ...';
  if not ShellExec('runas', Path, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    SuppressibleMsgBox('Die Microsoft Visual C++ Laufzeit wurde nicht installiert. Ohne sie erkennt Talk keinen Text. Wird das Setup noch einmal ausgeführt, fragt es erneut.', mbError, MB_OK, IDOK);
    Exit;
  end;
  case ResultCode of
    0, 1638: ;
    3010: RestartNeeded := True;
  else
    SuppressibleMsgBox('Die Installation der Microsoft Visual C++ Laufzeit endete mit Code ' + IntToStr(ResultCode) + '. Ohne sie erkennt Talk keinen Text.', mbError, MB_OK, IDOK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then InstallVcRedist;
end;

function NeedRestart(): Boolean;
begin
  Result := RestartNeeded;
end;
#endif
