# SmokeySense v1.1 (BETA)

**SmokeySense** is my custom built external cheat for Counter Strike 2 (CS2). I crafted this from the ground up as a passion project, disguising the entire thing to run as stealthily as possible by using a fake Windows process named `Microsoft.COM.Surogate`. Yeah, you read that right, it blends right into your system like it's part of the OS. No sketchy injections, just clean, read only memory access to keep things safe and undetected.

This is version **1.1 BETA**, so expect some updates soon. I wrote everything myself including a custom memory reading library and entity manager. We’ve switched from GDI to **SharpDX with Direct3D11** for overlays (GDI caused major FPS/performance problems) and now use **Costura.Fody** to embed referenced assemblies into the main `.exe` so there is still only a single binary.

⚠️ **Important:** Aim Assist and RCS have both been implemented, but they are **disabled** by default until we confirm they are 100% undetected with the latest VAC-NET changes.

---

## Features

* **Stealth:** Runs disguised as `Microsoft.COM.Surogate`, a nod to classic surrogate processes. Shows as a legit looking process in Task Manager.
* **Custom Memory Handling:** My own read only memory library (`Memory.cs`) with caching for speed. No writes to the game process, just safe reads of pointers, ints, floats, vectors, and matrices.
* **Custom Entity Management:** Full entity system (`Entity.cs`, `EntityManager.cs`) to track players, bones, health, teams, etc. Includes world to screen math and distance calculations. Threaded and locked for smooth updates.
* **Visual Overlays (ESP):** Box ESP and Bone ESP drawn on a transparent topmost window (`Overlay.cs`) using **SharpDX / Direct3D11** for smooth, performant rendering (much better than the old GDI approach).
* **Aim Assist (Implemented — Disabled):** Humanized, smoothed aim behavior with an FOV circle. **Disabled by default** until we confirm it's safe with VAC-NET.
* **Recoil Control System (RCS) (Implemented — Disabled):** RCS added, but **disabled by default** until verified safe.
* **Embedded Dependencies:** All NuGet assemblies are embedded into the `.exe` via **Costura.Fody** — no extra DLLs required.
* **Interactive Console Menu:** Toggle features at runtime using the console menu, type the corresponding number to enable/disable a function. No rebuild required!
* **Cross-Platform Vibes:** Tested on Windows 10 & 11; can run on Steam Deck (build on Windows, transfer exe).

---

## Releases

Official releases are available on the repository's Releases page. Each release contains the compiled `Microsoft.COM.Surogate.exe` for that version:

* Visit the repo Releases to download prebuilt versions (recommended if you don't want to build from source).
* Releases are tagged by version (for example: `v1.0-beta`, `v1.1-beta`, etc.).
* Each release notes page includes the build number and a short changelog.

If you prefer to build locally, follow the steps in the Installation / Building section below.

---

## Screenshots

### UI Settings

![UI Screenshot](https://i.imgur.com/BEEUYkF.png)

### In Game ESP

![In Game Screenshot](https://i.imgur.com/K51xhY3.jpeg)

---

## Steam Deck Compatibility

It runs on Steam Deck — build on Windows, move the `.exe` to the device, and run. Overlays work and it runs without spiking temps in my tests. Demo video:
[![SmokeySense on Steam Deck](https://i.imgur.com/9Gi54j5.png)](https://streamable.com/efdqtv)

---

## Installation / Building

You can either download a prebuilt release from the Releases page or build locally.

### Prerequisites

* Visual Studio 2022
* .NET Framework 4.8
* CS2 installed and running

### Clone the Repo

```bash
git clone https://github.com/TrentonHill/Smokey-Sense
```

### Build (Local)

1. Open the solution (`.sln`) in Visual Studio.
2. Set the project configuration to **Release**.
3. Build: `Build -> Build Solution` (or Ctrl+Shift+B).
4. Built binary: `bin/Release/Microsoft.COM.Surogate.exe` (this exe contains embedded references via Costura.Fody).

> **Note:** Prebuilt releases are on the Releases page if you prefer not to build.

---

## Running

1. Start CS2 first (menu or in-game).
2. Run `Microsoft.COM.Surogate.exe` as Administrator (required for memory access).
3. Console will show initialization messages and offset updates.
4. Overlay will appear if ESP is enabled.

---

## Usage & Controls

* **Interactive Console Menu:** When the program is running it exposes a console based menu. Type the number for the feature you want to toggle and press Enter. The menu controls enabling/disabling for ESP, Bones, Aim Assist (if enabled), RCS (if enabled), and other features. No need to re-edit `Functions.cs` for day to day toggles.
* **Default Keys:** There are a few default keybinds for quick toggles (see `Functions.cs`) — but the console menu is the recommended way to toggle at runtime.
* **Aim Assist & RCS:** Both features exist in the codebase but are **disabled by default**. They will remain disabled until explicit verification that they are safe from detection signals introduced by VAC-NET.
* **Advanced Customization:** If you want to change defaults, `Functions.cs` contains the config and default keybinds, advanced users can edit and rebuild. For most users, use the runtime console menu.
