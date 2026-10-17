# Runs the RATS checks from the terminal.
#   .\scripts\test.ps1          sim tests only (dotnet, Unity not needed, ~2 s)
#   .\scripts\test.ps1 -Unity   also play-mode smoke + headless freeze capture (Unity batch mode)
# Close the Unity editor first when using -Unity (one editor per project).
param([switch]$Unity)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'unity'
$captures = Join-Path $root 'captures'
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
$failed = $false

function Invoke-Timed([string]$name, [scriptblock]$block) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $code = & $block
    $result = if ($code -eq 0) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0,-28} {1}  {2:N1}s" -f $name, $result, $sw.Elapsed.TotalSeconds)
    if ($code -ne 0) { $script:failed = $true }
}

function Invoke-UnityBatch([string]$method, [string[]]$extra) {
    New-Item -ItemType Directory -Force $captures | Out-Null
    $log = Join-Path $captures "$method.log"
    $argList = @('-batchmode', '-projectPath', "`"$project`"", '-executeMethod', $method, '-logFile', "`"$log`"") + $extra
    $p = Start-Process -FilePath $unityExe -ArgumentList $argList -PassThru -NoNewWindow
    $null = $p.Handle  # keeps ExitCode readable after exit
    if (-not $p.WaitForExit(300000)) { Stop-Process -Id $p.Id -Force; return 99 }
    Select-String -Path $log -Pattern '^\[(PlaySmoke|Capture|CharacterView)\]|error CS' | ForEach-Object { Write-Host "    $($_.Line)" }
    return $p.ExitCode
}

Invoke-Timed 'sim tests (dotnet)' {
    dotnet test (Join-Path $project 'SimTests') --nologo -v q | Out-Host
    $LASTEXITCODE
}

if ($Unity) {
    Invoke-Timed 'play-mode smoke (Unity)' { Invoke-UnityBatch 'Rats.EditorTools.PlaySmoke.Run' @() }
    Invoke-Timed 'freeze capture (Unity)' { Invoke-UnityBatch 'Rats.EditorTools.Capture.Run' @('-captureDir', "`"$captures`"") }
}

if ($failed) { exit 1 }
