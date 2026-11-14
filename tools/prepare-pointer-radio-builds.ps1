param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe',
    [switch]$InstallAndLaunch
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'MarsPathUnity'
if (!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity editor not found: $UnityEditor" }
if (Test-Path -LiteralPath (Join-Path $projectPath 'Temp\UnityLockfile')) {
    throw 'Close the MarsPath Unity editor before preparing these builds.'
}

$adbPath = Join-Path (Split-Path -Parent $UnityEditor) 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
if ($InstallAndLaunch) {
    if (!(Test-Path -LiteralPath $adbPath)) { throw 'Quest USB tools are missing from this Unity installation.' }
    Write-Host 'Checking the connected Quest. Keep the USB cable connected.'
    $deviceLines = & $adbPath devices
    if ($LASTEXITCODE -ne 0) { throw 'Could not start Quest USB tools.' }
    if ($deviceLines -match '\sunauthorized\s*$') {
        Write-Host 'Put on the Quest and accept ALLOW USB DEBUGGING, then press Enter here.' -ForegroundColor Yellow
        Read-Host | Out-Null
        $deviceLines = & $adbPath devices
    }
    $devices = @($deviceLines | Where-Object { $_ -match '^([^\s]+)\s+device\s*$' } | ForEach-Object { ($_ -split '\s+')[0] })
    if ($devices.Count -ne 1) { throw 'Connect exactly one Quest by USB, accept USB debugging inside it, then run this file again.' }
    $questSerial = $devices[0]
    $questModel = (& $adbPath -s $questSerial shell getprop ro.product.model | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $questModel -ne 'Quest 3') { throw "Expected Quest 3; found: $questModel" }
}

function Invoke-MarsUnity([string]$method, [string]$target, [string]$logName) {
    $logPath = Join-Path $repoRoot "tmp\$logName"
    New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
    $result = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-buildTarget', $target,
        '-projectPath', ('"{0}"' -f $projectPath), '-executeMethod', $method,
        '-logFile', ('"{0}"' -f $logPath)
    )
    if ($result.ExitCode -ne 0) { throw "Unity failed ($($result.ExitCode)). See $logPath" }
}

Write-Host 'Checking encrypted radio playback in both directions...'
Invoke-MarsUnity 'MarsPath.Editor.MarsRadioRegressionChecks.Run' 'Win64' 'pointer-radio-check.log'
if (!(Select-String -LiteralPath (Join-Path $repoRoot 'tmp\pointer-radio-check.log') -SimpleMatch 'MARSPATH_RADIO_REGRESSION_PASSED' -Quiet)) {
    throw 'The radio regression check did not complete.'
}
Write-Host 'Building the laptop version...'
Invoke-MarsUnity 'MarsPath.Editor.MarsPathProjectSetup.BuildWindowsPrototype' 'Win64' 'pointer-radio-windows-build.log'
Write-Host 'Building the Quest version...'
& (Join-Path $PSScriptRoot 'build-quest.ps1') -UnityEditor $UnityEditor
Write-Host 'READY: Laptop and Quest builds completed.' -ForegroundColor Green
Get-Item -LiteralPath (Join-Path $projectPath 'Builds\Windows\MarsPath.exe'), (Join-Path $projectPath 'Builds\Quest\MarsPath.apk') |
    Select-Object FullName, Length, LastWriteTime

if ($InstallAndLaunch) {
    Write-Host 'Installing the updated Quest app. Please wait...'
    $installResult = & $adbPath -s $questSerial install -r (Join-Path $projectPath 'Builds\Quest\MarsPath.apk') 2>&1
    $installResult | Write-Host
    if ($LASTEXITCODE -ne 0 -or !($installResult -match '^Success\s*$')) { throw 'Quest installation failed. The laptop app has not been launched.' }
    & $adbPath -s $questSerial forward tcp:42739 tcp:42741
    if ($LASTEXITCODE -ne 0) { throw 'Could not establish the USB radio connection.' }
    & $adbPath -s $questSerial shell monkey -p com.lagrangev.marspath -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0) { throw 'The updated app is installed, but could not be opened on Quest.' }
    Start-Process -FilePath (Join-Path $projectPath 'Builds\Windows\MarsPath.exe') -WorkingDirectory (Join-Path $projectPath 'Builds\Windows') -WindowStyle Normal
    Write-Host 'READY TO TEST: updated Quest app installed and both apps opened. Laptop: hold Y to talk. Quest: hold LEFT TRIGGER to talk.' -ForegroundColor Green
}
