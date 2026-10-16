$ErrorActionPreference = 'Stop'
$cursorRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $cursorRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /optimize+ /target:winexe /platform:anycpu "/win32manifest:$cursorRoot\app.manifest" "/out:$output\FlameCursor.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll /r:Microsoft.CSharp.dll "$cursorRoot\src\CursorCore.cs" "$cursorRoot\src\ColorControls.cs" "$cursorRoot\src\ReactiveFlame.cs" "$cursorRoot\src\SettingsWindow.cs" "$cursorRoot\src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed.' }
Copy-Item -LiteralPath (Join-Path $cursorRoot 'READ ME.txt') -Destination $output -Force
& $compiler /nologo /optimize+ /target:winexe /platform:anycpu "/win32manifest:$cursorRoot\app.manifest" "/out:$output\FlameCursor-Setup.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "/resource:$output\FlameCursor.exe,CursorApp" "/resource:$cursorRoot\READ ME.txt,Guide" "$cursorRoot\src\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Get-Item (Join-Path $output 'FlameCursor.exe'), (Join-Path $output 'FlameCursor-Setup.exe') | Select-Object Name,Length
