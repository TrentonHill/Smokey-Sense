# SmokeySense v1.1 (BETA)

**SmokeySense** is my custom built external cheat for Counter Strike 2 (CS2). I crafted this from the ground up as a passion project, disguising the entire thing to run as stealthily as possible by using a fake Windows process named `Microsoft.COM.Surogate`. Yeah, you read that right, it blends right into your system like it's part of the OS. No sketchy injections, just clean, read only memory access to keep things safe and undetectable (as much as possible, anyway, use at your own risk!)

This is version **1.1 BETA**, so expect some updates soon. I built everything myself, custom memory reader, entity manager, offset grabber from public A2X sources. No third party libraries, no dependencies, just pure C# .net 4.8 and WinAPI calls. It's lightweight, performant, and even runs on a Steam Deck (more on that below)...

**Disclaimer:** This is for educational purposes only. Cheating in games can get you banned. I don't condone using this in online matches, test it offline or on bots. Always play fair!!

## Features

- **Stealth:** Runs disguised as `Microsoft.COM.Surogate` a nod to those old school surrogate processes. It looks legit in Task Manager, helping you fly under the radar.
  
- **Custom Memory Handling:** My own read only memory library (`Memory.cs`) with caching for speed. No writing to the game process, just peeking at what's there. Handles pointers, ints, floats, vectors, and matrices!

- **Custom Entity Management:** Built a full entity system (`Entity.cs` and `EntityManager.cs`) to track players, positions, bones, health, teams, you name it. World to screen conversion, bone reading for skeleton ESP, and distance calcs. All threaded and locked for smoothness.

- **Auto Updating Offsets:** Grabs the latest offsets from the public A2X GitHub repo (`OffsetGrabber.cs`) on startup. No manual updates needed, it parses and applies them dynamically.

- **Visual Overlays (ESP):** Box ESP and Bone ESP drawn on a transparent topmost window (`Overlay.cs`). Customizable colors, thickness, and toggles. Yeah, it's a bit FPS heavy in full matches right now (noted for optimization), but it looks sick for only using GDI..

- **Aim Assist:** Smooth, beyond humanized aim with FOV circle. Locks on heads with a touch of randomness, feels natural, not robotic! (The plan is to bypass the "future AI anti cheat")

- **No Dependencies:** Zero NuGet packages, no external DLLs. Just C# .NET 4.8 Framework and what's in the box. Compile and run, that's it! Only 48KB/s fully compiled as a .exe!!

- **Cross Platform Vibes:** Tested on Windows 10 & 11, and guess what? It even works on Steam Deck..

## Screenshots

### UI Settings
![UI Screenshot](https://i.imgur.com/BEEUYkF.png)

### In-Game ESP and Aim Assist
![In Game Screenshot](https://i.imgur.com/K51xhY3.jpeg)

## Steam Deck Compatibility

Who says cheats can't be portable? I got this running on my Steam Deck – just build it on Windows, transfer the exe, and run. ESP overlays work, aim assist snaps, and it doesn't overheat the thing. Here's a quick demo video:
[![SmokeySense on Steam Deck](https://i.imgur.com/9Gi54j5.png)](https://streamable.com/efdqtv)

## Installation / Building

No releases yet? No problem, build it yourself in Visual Studio. It's straight forward since there are no deps!

1. **Prerequisites:**
   - Visual Studio 2022.
   - .NET Framework 4.8.
   - CS2 installed and running.

2. **Clone the Repo:**
   ```
   git clone https://github.com/yourusername/SmokeySense.git
   ```

3. **Open in Visual Studio:**
   - Open VS and open the `.sln` file in the repo.
   - Set the project to **Release** mode.

4. **Build the Project:**
   - Hit **Build > Build Solution** (or Ctrl+Shift+B).
   - Grab the exe from `bin/Release/Microsoft.COM.Surogate.exe` (yep, that's the disguise).

5. **Run It:**
   - Launch CS2 first.
   - Run the exe as admin (for memory access).
   - Console will show init messages, offsets updating, etc.
   - Overlay appears if features are enabled (check `Functions.cs` for defaults).

## Usage

- **Starting Up:** Run the exe while CS2 is open (Dose not matter if you are in a match or at the menu). It'll hook in, update offsets, and start the overlay.
- **Toggles:** Use keys like Left Shift for aim assist (configurable in `Functions.cs`).
- **Customization:** Edit `Functions.cs` for Aim Assist Enable or Disable plus ESP and colors, FOV size, smoothing, etc. Rebuild to apply!
- **In-Game:** Enable ESP/bones for visuals, aim assist for... assistance. Make sure to hold the toggle key down for aim assist.
- **Exit:** Close the console window.
