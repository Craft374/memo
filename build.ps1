# MEMO 윈도우 빌드 스크립트
#   .\build.ps1          : 빌드 후 시작 메뉴 바로가기 생성
#   .\build.ps1 -Run     : 빌드 후 바로 실행
#   .\build.ps1 -NoShortcut : 바로가기 만들지 않음
param(
    [switch]$Run,
    [switch]$NoShortcut
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Dotnet {
    $candidates = @()
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($onPath) { $candidates += $onPath.Source }
    $candidates += "$env:USERPROFILE\.dotnet\dotnet.exe"

    foreach ($exe in $candidates) {
        if (Test-Path $exe) {
            $sdks = & $exe --list-sdks 2>$null
            if ($sdks) { return $exe }
        }
    }

    Write-Host ".NET SDK가 없어 사용자 폴더에 설치합니다 (관리자 권한 불필요)..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $installer = Join-Path $env:TEMP "dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer -UseBasicParsing
    powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet"
    return "$env:USERPROFILE\.dotnet\dotnet.exe"
}

$dotnet = Find-Dotnet
Write-Host "dotnet: $dotnet"

& $dotnet publish "$root\MEMO.csproj" -c Release -o "$root\build" --nologo
if ($LASTEXITCODE -ne 0) { throw "빌드 실패" }

$exe = Join-Path $root "build\MEMO.exe"
Write-Host ""
Write-Host "빌드 완료: $exe"

if (-not $NoShortcut) {
    $shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\MEMO.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = (Join-Path $root "build")
    $shortcut.Description = "MEMO"
    $shortcut.Save()
    Write-Host "시작 메뉴 바로가기 생성: $shortcutPath"
    Write-Host "(시작 메뉴에서 MEMO 검색 후 작업 표시줄에 고정할 수 있습니다)"
}

if ($Run) {
    Start-Process $exe
}
