# Build firmware GPT9800_ControlBoard -> .hex, dùng AVR toolchain có sẵn trong Arduino15
# (không cần Arduino IDE / arduino-cli)
#
#   .\build-firmware.ps1
#
param(
    [string]$Sketch = "$PSScriptRoot\..\firmware\GPT9800_ControlBoard\GPT9800_ControlBoard.ino",
    [string]$OutDir = "$PSScriptRoot\..\firmware\build"
)

$ErrorActionPreference = 'Stop'
$Arduino15 = Join-Path $env:LOCALAPPDATA 'Arduino15'
$Core      = Join-Path $Arduino15 'packages\arduino\hardware\avr\1.8.8'
$GccBin    = Join-Path $Arduino15 'packages\arduino\tools\avr-gcc\7.3.0-atmel3.6.1-arduino7\bin'
$AvrGpp    = Join-Path $GccBin 'avr-g++.exe'
$AvrGcc    = Join-Path $GccBin 'avr-gcc.exe'
$AvrObjcpy = Join-Path $GccBin 'avr-objcopy.exe'
$AvrSize   = Join-Path $GccBin 'avr-size.exe'

if (!(Test-Path $AvrGpp)) { throw "Không thấy avr-g++ tại $AvrGpp" }
if (!(Test-Path $Sketch)) { throw "Không thấy sketch tại $Sketch" }

$MCU        = 'atmega328p'
$F_CPU      = '16000000L'
$VariantDir = Join-Path $Core 'variants\standard'
$CoreDir    = Join-Path $Core 'cores\arduino'
$BuildDir   = Join-Path $OutDir 'obj'

# Rebuild the known objects in place without recursively deleting a computed path.
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutDir  -Force | Out-Null

$Defines  = @("-DARDUINO=10819", "-DARDUINO_AVR_UNO", "-DARDUINO_ARCH_AVR",
              "-DF_CPU=$F_CPU", "-mmcu=$MCU")
$Includes = @("-I$CoreDir", "-I$VariantDir")

$Common   = @('-c', '-g', '-Os', '-Wall', '-ffunction-sections', '-fdata-sections', '-flto')
$CppFlags = @('-std=gnu++11', '-fno-exceptions', '-fno-rtti', '-fpermissive')
$CFlags   = @('-std=gnu11', '-funsigned-char', '-funsigned-bitfields', '-fpack-struct', '-fshort-enums')

function Invoke-Gcc($exe, $argList) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & $exe @argList 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) {
        $out | ForEach-Object { Write-Host $_ }
        throw "Lỗi build với $(Split-Path $exe -Leaf) (exit $code)"
    }
    return $out
}

# ---- 1. Biên dịch core Arduino ----
$coreFiles = Get-ChildItem $CoreDir -Recurse -File | Where-Object { $_.Extension -in '.c', '.cpp', '.S' }
Write-Host "Core files: $($coreFiles.Count)"
foreach ($f in $coreFiles) {
    $name = ($f.Name -replace '[^A-Za-z0-9_]', '_') + '.o'
    $obj  = Join-Path $BuildDir $name
    if ($f.Extension -eq '.cpp') {
        Invoke-Gcc $AvrGpp ($Defines + $Includes + $Common + $CppFlags + @('-o', $obj, $f.FullName)) | Out-Null
    } else {
        Invoke-Gcc $AvrGcc ($Defines + $Includes + $Common + $CFlags + @('-o', $obj, $f.FullName)) | Out-Null
    }
}

# ---- 2. Biên dịch sketch (.ino -> .cpp) ----
$sketchCpp = Join-Path $BuildDir 'sketch.cpp'
$proto  = "void setup();`r`nvoid loop();`r`n"
$hdr    = "#include <Arduino.h>`r`n#include <stdint.h>`r`n#include <stdlib.h>`r`n#include <string.h>`r`n"
$body   = (Get-Content $Sketch -Raw) -replace '(?m)^\s*#include\s*<Arduino\.h>\s*$', ''
[System.IO.File]::WriteAllText($sketchCpp, "$hdr$proto$body", (New-Object System.Text.UTF8Encoding $false))
Invoke-Gcc $AvrGpp ($Defines + $Includes + $Common + $CppFlags + @('-o', (Join-Path $BuildDir 'sketch.o'), $sketchCpp)) | Out-Null

# ---- 3. Link ----
$elf  = Join-Path $OutDir 'GPT9800_ControlBoard.elf'
$hex  = Join-Path $OutDir 'GPT9800_ControlBoard.hex'
$objs = (Get-ChildItem $BuildDir -Filter *.o | ForEach-Object { $_.FullName })
$linkArgs = @('-Os', '-mmcu=atmega328p', '-Wl,--gc-sections', '-flto', '-o', $elf) + $objs + @('-lm')
Invoke-Gcc $AvrGpp $linkArgs | Out-Null

Invoke-Gcc $AvrObjcpy @('-O', 'ihex', '-R', '.eeprom', $elf, $hex) | Out-Null
& $AvrSize -C --mcu=$MCU $elf

Write-Host ""
Write-Host "OK -> $hex"
