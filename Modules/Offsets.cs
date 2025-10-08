using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.COM.Surogate.Data
{
    public static class Offsets
    {
        public static IntPtr ClientBaseAddr;
        public static async Task UpdateOffsets()
        {
            // Delete output folder if it exist
            if (System.IO.Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")))
            {
                System.IO.Directory.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output"), true);
            }

            // Delete cs2-dumper.log if it exist
            if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cs2-dumper.log")))
            {
                System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cs2-dumper.log"));
            }

            // Delete dumper.exe if it exist
            if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe")))
            {
                System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe"));
            }

            // grab dumper.exe bytes from resources and create it as a file.exe in the same base directory to be ran
            byte[] dumperBytes = Properties.Resources.dumper;
            if (dumperBytes != null && dumperBytes.Length > 0)
            {
                using (var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe"), FileMode.Create, FileAccess.Write))
                {
                    fileStream.Write(dumperBytes, 0, dumperBytes.Length);
                }
            }
            else
            {
                Console.WriteLine($"[i]: dumper.exe error: not found or empty!");
                Thread.Sleep(5000);
                Environment.Exit(1);
            }

            // Start dumper.exe
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe"),
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,   // required to hide the window
                CreateNoWindow = true,     // hides console window
                WindowStyle = ProcessWindowStyle.Hidden // extra safety
            };
            try
            {
                using (Process proc = Process.Start(startInfo))
                {
                    proc.WaitForExit(); // wait for it to finish
                }

                // Grab the offsets we need from the freshly dumped classes in the output folder
                var offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                // Check if the output folder exist
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")))
                {
                    Console.WriteLine($"[i]: dumper.exe error: output folder not found!");
                    Thread.Sleep(5000);
                    Environment.Exit(1);
                }

                // specific offset class files that we need, add more if needed!
                string[] files = {
                        System.IO.Path.Combine(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output"), "client_dll.cs"),
                        System.IO.Path.Combine(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output"), "offsets.cs")
                    };

                // custom regex to target offsets by there class and nint so make sure to use the .cs files
                string currentClass = null;
                var classRegex = new System.Text.RegularExpressions.Regex(@"public\s+static\s+class\s+([A-Za-z0-9_]+)");
                var constRegex = new System.Text.RegularExpressions.Regex(@"public\s+const\s+nint\s+([A-Za-z0-9_]+)\s*=\s*(0x[0-9A-Fa-f]+|\d+);");

                // Read all the offsets in the classes/files we are targeting
                foreach (var file in files)
                {
                    if (!System.IO.File.Exists(file))
                    {
                        Console.WriteLine($"[i]: dumper.exe error: {file}");
                        continue;
                    }

                    foreach (string line in System.IO.File.ReadAllLines(file))
                    {
                        var classMatch = classRegex.Match(line);
                        if (classMatch.Success)
                        {
                            currentClass = classMatch.Groups[1].Value.Trim();
                            continue;
                        }

                        var constMatch = constRegex.Match(line);
                        if (constMatch.Success && currentClass != null)
                        {
                            string name = constMatch.Groups[1].Value.Trim();
                            string value = constMatch.Groups[2].Value.Trim();

                            long offset = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                                ? Convert.ToInt64(value.Substring(2), 16)
                                : Convert.ToInt64(value);

                            string key = $"{currentClass}.{name}";
                            if (!offsets.ContainsKey(key))
                                offsets[key] = offset;
                        }
                    }
                }

                // Delete the output folder as we dont need it anymore
                if (System.IO.Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")))
                {
                    System.IO.Directory.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output"), true);
                }

                // Delete cs2-dumper.log if it exist
                if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cs2-dumper.log")))
                {
                    System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cs2-dumper.log"));
                }

                // Also delete dumper.exe as we dont need it anymore
                if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe")))
                {
                    System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dumper.exe"));
                }

                // Lowkey had no idea you could even do something like this in c#
                long Get(string key)
                {
                    if (offsets.TryGetValue(key, out long val))
                        return val;

                    string[] parts = key.Split('.');
                    string bare = parts[parts.Length - 1];
                    foreach (var kv in offsets)
                        if (kv.Key.EndsWith("." + bare, StringComparison.OrdinalIgnoreCase))
                            return kv.Value;

                    Console.WriteLine($"[i]: dumper.exe error: Offset not found: {key}");
                    return 0;
                }

                // assign the offsets
                m_iHealth = (int)Get("C_BaseEntity.m_iHealth");
                dwViewMatrix = (int)Get("ClientDll.dwViewMatrix");
                m_vecViewOffset = (int)Get("C_BaseModelEntity.m_vecViewOffset");
                m_lifeState = (int)Get("C_BaseEntity.m_lifeState");
                m_vOldOrigin = (int)Get("C_BasePlayerPawn.m_vOldOrigin");
                m_iTeamNum = (int)Get("C_BaseEntity.m_iTeamNum");
                m_hPlayerPawn = (int)Get("CBasePlayerController.m_hPawn");
                dwLocalPlayerPawn = (int)Get("ClientDll.dwLocalPlayerPawn");
                dwEntityList = (int)Get("ClientDll.dwEntityList");
                m_modelState = (int)Get("CSkeletonInstance.m_modelState");
                m_pGameSceneNode = (int)Get("C_BaseEntity.m_pGameSceneNode");
                shotsFired = (int)Get("C_CSPlayerPawn.m_iShotsFired");
                dwSensitivity = (int)Get("ClientDll.dwSensitivity");
                dwSensitivity_sensitivity = (int)Get("ClientDll.dwSensitivity_sensitivity");
                m_flFOVSensitivityAdjust = (int)Get("C_BasePlayerPawn.m_flFOVSensitivityAdjust");
                m_aimPunchCache = (int)Get("C_CSPlayerPawn.m_aimPunchCache");
                m_aimPunchAngle = (int)Get("C_CSPlayerPawn.m_aimPunchAngle");
                dwViewAngles = (int)Get("ClientDll.dwViewAngles");
                dwGlobalVars = (int)Get("ClientDll.dwGlobalVars");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[i]: dumper.exe error: " + ex.Message);
                Thread.Sleep(5000);
                Environment.Exit(1);
            }
        }

        public static int m_iHealth = 0x00;  // Player's current health
        public static int dwViewMatrix = 0x00;  // View matrix for world to screen calculations\
        public static int m_vecViewOffset = 0x00;  // View offset vector
        public static int m_lifeState = 0x00;  // Life state (alive/dead)
        public static int m_vOldOrigin = 0x00;  // Old origin position
        public static int m_iTeamNum = 0x00;  // Team number
        public static int m_hPlayerPawn = 0x00;  // Handle to player pawn
        public static int dwLocalPlayerPawn = 0x00;  // Local player pawn address
        public static int dwEntityList = 0x00;  // Entity list pointer
        public static int m_modelState = 0x00;  // Model state
        public static int m_pGameSceneNode = 0x00;  // Game scene node pointer
        public static int shotsFired = 0x00;
        public static int dwSensitivity = 0x00;
        public static int dwSensitivity_sensitivity = 0x00;
        public static int m_flFOVSensitivityAdjust = 0x00;
        public static int m_aimPunchCache = 0x00;
        public static int m_aimPunchAngle = 0x00;
        public static int dwViewAngles = 0x00;
        public static int dwGlobalVars = 0x00;
    }
}