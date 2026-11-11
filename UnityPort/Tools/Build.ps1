param(
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe',
    [switch]$RebuildScene
)
$ErrorActionPreference = 'Stop'
$gardenProjectRoot = Split-Path -Parent $PSScriptRoot
$gardenLogDirectory = Join-Path $gardenProjectRoot 'Logs'
New-Item -ItemType Directory -Path $gardenLogDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "No se encuentra Unity: $UnityEditorPath"
}

function Invoke-GardenEditor([string]$Method, [string]$LogName) {
    $gardenLogPath = Join-Path $gardenLogDirectory $LogName
    $gardenArguments = @('-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $gardenProjectRoot + '"'),
        '-executeMethod', $Method, '-logFile', ('"' + $gardenLogPath + '"'))
    Write-Host "Unity: $Method"
    $gardenProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $gardenArguments -WindowStyle Hidden -Wait -PassThru
    if ($gardenProcess.ExitCode -ne 0) {
        throw "Unity termino con codigo $($gardenProcess.ExitCode). Revisa $gardenLogPath"
    }
}

if ($RebuildScene) {
    Invoke-GardenEditor 'Garden.EditorTools.UnityMigrationBuilder.BuildProject' 'GenerateProject.log'
}
Invoke-GardenEditor 'Garden.EditorTools.UnityMigrationBuilder.BuildWindows' 'BuildWindows.log'
Write-Host "Ejecutable: $(Join-Path $gardenProjectRoot 'Build\Windows\Jardin.exe')"
