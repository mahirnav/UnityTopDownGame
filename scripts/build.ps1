# Builds the windowed Windows player to unity/Builds/RATS/RATS.exe (close the Unity editor first).
#   .\scripts\build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
$log = Join-Path $root 'captures\build.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

$argList = @('-batchmode', '-projectPath', "`"$root\unity`"", '-executeMethod', 'Rats.EditorTools.PlayerBuild.Windows', '-logFile', "`"$log`"")
$p = Start-Process -FilePath $unityExe -ArgumentList $argList -PassThru -NoNewWindow
$null = $p.Handle
$p.WaitForExit()
Select-String -Path $log -Pattern '\[PlayerBuild\]|error CS' | ForEach-Object { Write-Host $_.Line }
exit $p.ExitCode
