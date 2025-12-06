A fork of IsoMounter plugin made by Aynshe.

WinCDEmu does not support Audio CDs, so I replaced it with ImDrive (a free and lightweight virtual drive).


# 💿 IsoMounter for RetroBat

**Allows you to run original Windows CD-ROM games in RetroBat without relying on no-CD patches.**

While no-CD patches exist (often of unclear legality or shady origin), the goal of this plugin is to avoid using them and still be able to play your original games — especially if the CD is lost, damaged, or your system no longer has a CD-ROM drive.  
This solution assumes you still have a valid image file (like an `.iso`), and the game remains compatible with Windows 10 or 11.

---

### 📁 Installation

1. Extract the contents of the archive into the `plugins` and `emulationstation` folders at the root of **RetroBat**.
2. Place your ISO files in:  
   `*\RetroBat\plugins\IsoMounter\iso`
3. Name the ISO files exactly like your ROMs:  
   For example, `MyGame.iso` for `MyGame.pc` or a `.lnk`, `.bat`, or `.url` file if added to Steam.

---

### ⚠️ Some games detect the CD-ROM too quickly

Some games check for the CD immediately upon launch, before the image has time to mount.  
In such cases, a `.bat` launcher with a short delay is required to ensure the ISO is mounted in time.

#### Example:

```bat
@echo off
c:
cd /d "c:\path\to\game"
timeout /t 3 /nobreak >nul
"game.exe"
```

---

### ⚙️ Requirements

To run this program, the **.NET 9 runtime** must be installed on your system.  
You can download it here:  
👉 [Download .NET 9 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime)

🗂️ Supported formats
Supports various disk image formats.

If ImgDrive is installed, it will be used to mount all image formats, including .iso, .cue/.bin, and others.
(If you rename a .cue and .bin to match your game, be sure to update the content inside the .cue accordingly.)

Otherwise, only .iso files will be mounted using Windows' native system.

✅ ImgDrive is recommended:
It is generally faster, and CD mounting errors should occur less frequently in games.

🔗 https://www.yubsoft.com/imgdrive/
