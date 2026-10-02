param(
    [ValidateSet('Open', 'Configure', 'Validate', 'Build')]
    [string]$Action = 'Open'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$editorPath = 'E:\Unity\Editor\6000.4.7f1\Editor\Unity.exe'
if (!(Test-Path -LiteralPath $editorPath)) { throw "Unity editor not found: $editorPath" }
$logFolder = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logFolder -Force | Out-Null
$arguments = @('-projectPath', ('"' + $projectRoot + '"'))
if ($Action -ne 'Open') {
    $methods = @{ Configure = 'Configure'; Validate = 'Validate'; Build = 'BuildWindows' }
    $arguments += @('-batchmode', '-quit', '-buildTarget', 'Win64', '-executeMethod', ('Emerge.Editor.EmergeProjectSetup.' + $methods[$Action]))
}
$arguments += @('-logFile', ('"' + (Join-Path $logFolder ($Action.ToLowerInvariant() + '.log')) + '"'))
$process = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
if ($Action -ne 'Open') {
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode); see Logs." }
}
