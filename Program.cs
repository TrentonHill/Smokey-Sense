using Microsoft.COM.Surogate;
using Microsoft.COM.Surogate.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [STAThread]
    private static async Task Main()
    {
        Console.Title = "Microsoft.COM.Surogate";
        Console.WriteLine("\r\n     __                 _              __                     \r\n    / _\\_ __ ___   ___ | | _____ _   _/ _\\ ___ _ __   ___ ___ \r\n    \\ \\| '_ ` _ \\ / _ \\| |/ / _ \\ | | \\ \\ / _ \\ '_ \\ / __/ _ \\\r\n    _\\ \\ | | | | | (_) |   <  __/ |_| |\\ \\  __/ | | | (_|  __/\r\n    \\__/_| |_| |_|\\___/|_|\\_\\___|\\__, \\__/\\___|_| |_|\\___\\___| v1.2 BETA\r\n                                 |___/                        ");
        Console.Write("[i]: Looking for CS2.exe!");
        if (Process.GetProcessesByName("cs2").Length == 0)
        {
            Console.WriteLine("[i]: CS2 is not running. Please start the game first!");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }
        else
        {
            Console.Write(" (✓)\n");
            try
            {
                Console.Write("[i]: Initializing Memory!");
                Memory memory = new Memory();
                Console.Write(" (✓)\n");

                Console.Write("[i]: Updating Client Base!");
                Offsets.ClientBaseAddr = memory.GetModuleBase();
                Console.Write(" (✓)\n");

                Console.Write("[i]: Updating Offsets!");
                await Offsets.UpdateOffsets();
                Console.Write(" (✓)\n");

                Console.Write("[i]: Starting Entity Manager!");
                EntityManager entityManager = new EntityManager(memory);
                // Entity Manager Background Thread
                Thread updateThread = new Thread(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    while (true)
                    {
                        Process proc = memory.GetProcess();
                        bool isForeground = false;
                        IntPtr fgWindow = GetForegroundWindow();
                        if (fgWindow != IntPtr.Zero)
                        {
                            GetWindowThreadProcessId(fgWindow, out uint pid);
                            isForeground = pid == (uint)proc.Id;
                        }
                        if ((Functions.BoxESPEnabled || Functions.BoneESPEnabled || Functions.AimAssistEnabled) && isForeground)
                        {
                            long startMs = sw.ElapsedTicks * 1000 / Stopwatch.Frequency;
                            List<Entity> entities = entityManager.GetEntities();
                            Entity local = entityManager.GetLocalPlayer();
                            entityManager.UpdateLocalPlayer(local);
                            entityManager.UpdateEntities(entities);
                            long elapsedMs = (sw.ElapsedTicks * 1000 / Stopwatch.Frequency) - startMs;
                            // ~7 ms per frame = 144 FPS cap
                            int targetMs = 7;
                            if (elapsedMs < targetMs)
                                Thread.Sleep((int)(targetMs - elapsedMs));
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    }
                });
                updateThread.IsBackground = true;
                updateThread.Priority = ThreadPriority.Normal;
                updateThread.Start();
                Console.Write(" (✓)\n");

                Console.Write("[i]: Initializing Overlay!");
                using (Overlay overlay = new Overlay(memory, entityManager))
                {
                    overlay.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: {ex.Message}");
                Thread.Sleep(5000);
                Environment.Exit(1);
            }
        }
    }
}