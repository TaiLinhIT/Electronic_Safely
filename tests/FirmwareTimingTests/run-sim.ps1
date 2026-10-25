# =====================================================================================
#  run-sim.ps1  -  Build & chạy mô phỏng firmware trên PC
#
#  Không cần board, không cần Arduino IDE. Script biên dịch THẲNG file
#  GPT9800_ControlBoard.ino (qua shim Arduino.h) rồi chạy 11 kịch bản kiểm tra
#  thứ tự xi-lanh / interlock / relay / timeout / lệnh chẩn đoán.
#
#    .\run-sim.ps1            # build + chạy
#    .\run-sim.ps1 -NoBuild   # chỉ chạy lại exe đã build
#
#  Cần 1 trình biên dịch C++ cho Windows (chọn 1 trong 2):
#    - w64devkit (khuyến nghị, portable): giải nén rồi thêm thư mục bin vào PATH,
#      hoặc đặt biến môi trường W64DEVKIT trỏ tới thư mục w64devkit
#    - Visual Studio C++ (cl.exe + Windows SDK)
# =====================================================================================
param(
    [switch]$NoBuild,
    [string]$Compiler = ''
)

$ErrorActionPreference = 'Stop'
$SimDir = $PSScriptRoot
$Exe    = Join-Path $SimDir 'build\sim.exe'
$OutDir = Join-Path $SimDir 'build'

function Find-Gpp {
    if ($Compiler) {
        if (Test-Path $Compiler) { return (Resolve-Path $Compiler).Path }
        throw "Khong thay compiler: $Compiler"
    }
    $cmd = Get-Command g++ -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        $(if ($env:W64DEVKIT) { Join-Path $env:W64DEVKIT 'bin\g++.exe' } else { $null }),
        "$env:LOCALAPPDATA\Temp\opencode\w64devkit\w64devkit\bin\g++.exe",
        'C:\w64devkit\bin\g++.exe',
        'C:\msys64\ucrt64\bin\g++.exe',
        'C:\msys64\mingw64\bin\g++.exe',
        'C:\MinGW\bin\g++.exe'
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    return $null
}

if (-not $NoBuild) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

    $gpp = Find-Gpp
    if ($gpp) {
        Write-Host ">>> Build bang $gpp" -ForegroundColor Cyan
        & $gpp -std=c++17 -O1 -I"$SimDir" -o $Exe "$SimDir\sim_main.cpp"
        if ($LASTEXITCODE -ne 0) { throw "g++ loi (exit $LASTEXITCODE)" }
    }
    else {
        $vcvars = @(
            'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat',
            'C:\Program Files\Microsoft Visual Studio\17\Community\VC\Auxiliary\Build\vcvars64.bat',
            'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat'
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1

        if (-not $vcvars) {
            throw ("Khong tim thay trinh bien dich C++ nao.`n" +
                   "Cai w64devkit (portable, ~67 MB): https://github.com/skeeto/w64devkit/releases`n" +
                   "roi dat bien moi truong W64DEVKIT tro toi thu muc w64devkit, hoac them bin vao PATH.")
        }

        Write-Host ">>> Build bang MSVC ($vcvars)" -ForegroundColor Cyan
        $bat = Join-Path $OutDir 'build.bat'
        @"
@echo off
call "$vcvars" >nul
cl /nologo /EHsc /utf-8 /W3 /I"$SimDir" /Fe:"$Exe" /Fo:"$OutDir\\" "$SimDir\sim_main.cpp"
"@ | Set-Content -Path $bat -Encoding ASCII
        cmd /c "`"$bat`""
        if ($LASTEXITCODE -ne 0) { throw "MSVC loi (exit $LASTEXITCODE)" }
    }
}

if (-not (Test-Path $Exe)) { throw "Khong thay $Exe - hay chay lai khong co -NoBuild." }

& $Exe
exit $LASTEXITCODE
