param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'MarsPathUnity'
$settingsPath = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
$logPath = Join-Path $project 'Builds\Quest\build.log'
$inputLine = [regex]'(?m)^(  activeInputHandler: )([012])(?=\r?$)'

if (!(Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity editor not found at $UnityEditor. Supply -UnityEditor with an Android-capable Unity installation."
}
if (Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')) {
    throw 'Close this Unity project before starting the Quest build.'
}

$settings = [IO.File]::ReadAllText($settingsPath)
$inputSetting = $inputLine.Match($settings)
if (!$inputSetting.Success) { throw 'Active Input Handling was not found in ProjectSettings.asset.' }
$previousInput = $inputSetting.Groups[2].Value

try {
    [IO.File]::WriteAllText($settingsPath, $inputLine.Replace($settings, '${1}1', 1))
    New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
    $build = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-buildTarget', 'Android',
        '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'MarsPath.Editor.MarsPathQuestBuild.BuildApk',
        '-logFile', ('"{0}"' -f $logPath)
    )
    if ($build.ExitCode -ne 0) { throw "Unity Quest build failed with exit code $($build.ExitCode). See $logPath." }
    Get-Item -LiteralPath (Join-Path $project 'Builds\Quest\MarsPath.apk')
}
finally {
    $updatedSettings = [IO.File]::ReadAllText($settingsPath)
    if (!$inputLine.IsMatch($updatedSettings)) { throw "Could not restore the desktop input setting in $settingsPath." }
    [IO.File]::WriteAllText($settingsPath, $inputLine.Replace($updatedSettings, ('${1}' + $previousInput), 1))
}
