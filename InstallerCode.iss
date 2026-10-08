[CustomMessages]
english.DesktopIconTask=Create a desktop shortcut
english.TasksGroup=Additional tasks:
english.RunApp=Launch {#MyAppName}
english.UninstallEntry=Uninstall {#MyAppName}
russian.DesktopIconTask=Создать ярлык на рабочем столе
russian.TasksGroup=Дополнительные задачи:
russian.RunApp=Запустить {#MyAppName}
russian.UninstallEntry=Удалить {#MyAppName}

[Code]
function GetUiLanguage: String;
begin
  if ActiveLanguage = 'english' then
    Result := 'en'
  else
    Result := 'ru';
end;

procedure WriteDefaultConfig;
var
  ConfigPath: String;
  Json: String;
begin
  ConfigPath := ExpandConstant('{app}\config.json');
  if FileExists(ConfigPath) then
    Exit;

  Json :=
    '{' + #13#10 +
    '  "UiLanguage": "' + GetUiLanguage + '",' + #13#10 +
    '  "ClientId": "",' + #13#10 +
    '  "ActivityName": "",' + #13#10 +
    '  "ActivityType": 0,' + #13#10 +
    '  "ImageSet": "kiara",' + #13#10 +
    '  "TimestampMode": "elapsed",' + #13#10 +
    '  "UpdateInterval": 10000,' + #13#10 +
    '  "AutoConnect": false' + #13#10 +
    '}';

  SaveStringToFile(ConfigPath, Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteDefaultConfig;
end;
