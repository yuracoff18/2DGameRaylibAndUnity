param()
$ErrorActionPreference = 'Stop'
$gardenProjectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $gardenProjectRoot
try {
    dotnet run --project 'Tests/ModelTests.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del modelo.' }
    $gardenPlayer = Join-Path $gardenProjectRoot 'Build\Windows\Jardin.exe'
    if (-not (Test-Path -LiteralPath $gardenPlayer)) { throw 'Compila primero con Tools\Build.ps1.' }
    $gardenOutput = Join-Path $gardenProjectRoot 'Validation\Player'
    New-Item -ItemType Directory -Path $gardenOutput -Force | Out-Null
    $gardenPlayerLog = Join-Path $gardenOutput 'Player.log'
    $gardenArguments = @('-gardenSmoke', '-gardenSmokeOutput', ('"' + $gardenOutput + '"'),
        '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0',
        '-logFile', ('"' + $gardenPlayerLog + '"'))
    $gardenProcess = Start-Process -FilePath $gardenPlayer -ArgumentList $gardenArguments -WindowStyle Hidden -Wait -PassThru
    if ($gardenProcess.ExitCode -ne 0) { throw "Fallo del ejecutable. Revisa $gardenPlayerLog" }
    $gardenReport = Get-Content -LiteralPath (Join-Path $gardenOutput 'player-validation.json') -Raw | ConvertFrom-Json
    if (-not $gardenReport.passed) { throw 'La comprobacion del ejecutable no paso.' }
    Write-Host "Modelo y ejecutable correctos. Informes: $(Join-Path $gardenProjectRoot 'Validation')"
} finally {
    Pop-Location
}
