# Flame Cursor

An animated Windows cursor with a movement-reactive flame, independent arrow and flame controls, and customizable colors.

## Download and install

**[Download the latest installer](https://github.com/MyGuitarMethod/Flame-Cursor/releases/latest/download/FlameCursor-Setup.exe)**

Or open the **[latest release](https://github.com/MyGuitarMethod/Flame-Cursor/releases/latest)** for the ZIP package and quick guide.

1. Run `FlameCursor-Setup.exe` and choose **Install**.
2. In Cursor Settings, click **Apply & enable**.
3. Open settings again from the desktop right-click menu or the Start menu. On Windows 11, the desktop entry may be under **Show more options**.

Installs for your Windows account without administrator access. Requires .NET Framework 4.5 or newer. Developed and tested on Windows 10; Windows 11 has not been tested on a physical device. This personal build is unsigned.

## Controls

- Adjust overall size, arrow size, flame size, and independent pulse settings.
- Enable **Flame physics** to make the fire bend with movement.
- Adjust **Flame sensitivity** from 25% to 400%; try 250% for a stronger reaction.
- Change the arrow outline, arrow fill, and flame with hue, brightness, and saturation controls.
- **Reset defaults** restores all appearance settings. **Disable** restores your Windows pointer.

The arrow and reactive flame share one native cursor image, keeping them attached to the click point. Text, resize, and other special pointers stay unchanged. Enabled settings return after signing in to Windows.

See [the quick guide](READ%20ME.txt) for usage, updating, and uninstalling.

## Build from source

On Windows with the .NET Framework C# compiler installed, run:

```powershell
.\build.ps1
```

Outputs are written to `dist`: the application, self-contained installer, and quick guide. Generated files and local verification output are excluded from source control; downloadable packages are attached to GitHub releases.

Run the isolated checks without changing your installed cursor or settings:

```powershell
Start-Process .\dist\FlameCursor.exe -ArgumentList '--self-test', 'verification' -Wait
Get-Content .\verification\results.txt
```

The source uses WinForms and Windows cursor APIs, with no external packages or game assets.
