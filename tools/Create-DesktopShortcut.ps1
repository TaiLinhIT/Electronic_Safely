param(
    [string]$ExePath = '',
    [string]$DesktopPath = [Environment]::GetFolderPath('DesktopDirectory'),
    [string]$ShortcutName = 'MiT TECH - Kiem tra an toan dien'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $PSScriptRoot 'GPT9000_WinForms_TestSystem.exe'
    if (-not (Test-Path -LiteralPath $ExePath)) {
        $ExePath = Join-Path $PSScriptRoot '..\bin\Release\net8.0-windows\GPT9000_WinForms_TestSystem.exe'
    }
}
$ExePath = [IO.Path]::GetFullPath($ExePath)
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Không thấy chương trình: $ExePath" }
if ($ShortcutName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Tên shortcut không hợp lệ.' }
$iconSource = Join-Path ([IO.Path]::GetDirectoryName($ExePath)) 'Assets\MitTechApp.ico'
if (-not (Test-Path -LiteralPath $iconSource -PathType Leaf)) { throw "Không thấy icon MiT TECH: $iconSource" }

# Use a content-specific path so an old icon cached for the EXE cannot replace
# the current brand. The copy is per-user and does not require editing Program Files.
$iconHash = (Get-FileHash -LiteralPath $iconSource -Algorithm SHA256).Hash.Substring(0, 16)
$iconCache = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MiTTechSafetyTest\Icons'
New-Item -ItemType Directory -Path $iconCache -Force | Out-Null
$iconPath = Join-Path $iconCache ("MitTech-$iconHash.ico")
if (-not (Test-Path -LiteralPath $iconPath) -or
    (Get-FileHash -LiteralPath $iconPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $iconSource -Algorithm SHA256).Hash) {
    Copy-Item -LiteralPath $iconSource -Destination $iconPath -Force
}
New-Item -ItemType Directory -Path $DesktopPath -Force | Out-Null
$shortcutPath = Join-Path ([IO.Path]::GetFullPath($DesktopPath)) ($ShortcutName + '.lnk')
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcutPath)
$link.TargetPath = $ExePath
$link.WorkingDirectory = [IO.Path]::GetDirectoryName($ExePath)
$link.IconLocation = $iconPath + ',0'
$link.Description = 'MiT TECH - Hệ thống kiểm tra an toàn điện GPT-9000'
$link.Save()

if (-not ('MitTechShortcutNotify' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class MitTechShortcutNotify { [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern void SHChangeNotify(uint eventId, uint flags, string path, IntPtr other); [DllImport("shell32.dll", EntryPoint="SHChangeNotify")] public static extern void RefreshAssociations(uint eventId, uint flags, IntPtr first, IntPtr second); }'
}
[MitTechShortcutNotify]::SHChangeNotify(0x2000, 0x1005, $ExePath, [IntPtr]::Zero)
[MitTechShortcutNotify]::SHChangeNotify(0x2000, 0x1005, $shortcutPath, [IntPtr]::Zero)
[MitTechShortcutNotify]::RefreshAssociations(0x08000000, 0x1000, [IntPtr]::Zero, [IntPtr]::Zero)
[pscustomobject]@{ Shortcut=$shortcutPath; Target=$ExePath; Icon=$iconPath }
