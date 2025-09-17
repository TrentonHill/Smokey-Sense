// Main entry point. Sets up everything and runs the overlay.
// I have a loop for updating entities in a background thread.
// Checks if CS2 is running and handles foreground focus.

using Microsoft.COM.Surogate;
using Microsoft.COM.Surogate.Data;
using Microsoft.COM.Surogate.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        Console.WriteLine("\r\n     __                 _              __                     \r\n    / _\\_ __ ___   ___ | | _____ _   _/ _\\ ___ _ __   ___ ___ \r\n    \\ \\| '_ ` _ \\ / _ \\| |/ / _ \\ | | \\ \\ / _ \\ '_ \\ / __/ _ \\\r\n    _\\ \\ | | | | | (_) |   <  __/ |_| |\\ \\  __/ | | | (_|  __/\r\n    \\__/_| |_| |_|\\___/|_|\\_\\___|\\__, \\__/\\___|_| |_|\\___\\___| v1.0 BETA\r\n                                 |___/                        ");

        if (Process.GetProcessesByName("cs2").Length == 0)
        {
            Console.WriteLine("[i]: CS2 is not running. Please start the game first!");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }

        try
        {
            Functions.LoadConfig();
            Functions.StartConfigWatcher();
            Application.ApplicationExit += (s, e) => Functions.StopConfigWatcher();

            Console.WriteLine("[i]: Initializing memory for CS2...");
            Memory memory = new Memory();
            EntityManager entityManager = new EntityManager(memory);

            Console.WriteLine("[i]: Updating offsets...");
            await OffsetGrabber.UpdateOffsets();

            Console.WriteLine("[i]: Startup completed!");

            // Background thread for entity updates
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
                        long startMs = sw.ElapsedTicks / 10000;
                        List<Entity> entities = entityManager.GetEntities();
                        Entity local = entityManager.GetLocalPlayer();
                        entityManager.UpdateLocalPlayer(local);
                        entityManager.UpdateEntities(entities);
                        long elapsedMs = (sw.ElapsedTicks / 10000) - startMs;
                        int targetMs = 16;
                        if (elapsedMs < targetMs) Thread.Sleep(targetMs - (int)elapsedMs);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            });
            updateThread.IsBackground = true;
            updateThread.Priority = ThreadPriority.BelowNormal;
            updateThread.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Overlay(memory, entityManager));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[i]: {ex.Message}");
            Thread.Sleep(5000);
            Environment.Exit(1);
        }
    }
}