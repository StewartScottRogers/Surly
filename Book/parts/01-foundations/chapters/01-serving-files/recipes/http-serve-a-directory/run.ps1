# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Recipe http-serve-a-directory, Windows form of run.sh.
Set-Location $PSScriptRoot
$server = Start-Process surl -ArgumentList '--directory','site','http://127.0.0.1:8080/' -PassThru -NoNewWindow
try { curl.exe http://127.0.0.1:8080/hello.txt } finally { Stop-Process $server }
