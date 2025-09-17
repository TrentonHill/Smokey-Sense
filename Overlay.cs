// This is the overlay form that draws ESP and handles aim assist.
// It's a transparent topmost window using GDI for drawing.
// I have a custom keyboard hook for toggle detection and used mouse_event for aim movement.
// Note: ESP is super laggy in online matches and kills FPS but fine in private matches, will optimize asap.. im working on it!

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.COM.Surogate;
using Microsoft.COM.Surogate.Data;
using Microsoft.COM.Surogate.Modules;

public class Overlay : Form
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);  // Keyboard hook delegate
    private readonly Memory memory;  // Memory reader
    private readonly EntityManager entityManager;  // Entity manager
    private readonly System.Windows.Forms.Timer renderTimer;  // Timer for rendering
    private Pen espPen;  // Pen for drawing ESP
    private IntPtr keyboardHookId = IntPtr.Zero;  // Hook ID
    private bool toggleKeyPressed;  // Toggle state
    private LowLevelKeyboardProc keyboardProcDelegate;  // Delegate instance
    private static Vector2 previousDelta = Vector2.Zero;  // For aim smoothing
    private static Vector2 currentVelocity = Vector2.Zero;  // Velocity for aim
    private static readonly Random rand = new Random();  // Random for humanization
    private static DateTime lastShotTime = DateTime.Now;  // Track shots (not used yet)
    private static Entity lastTarget = null;  // Last aim target
    private static DateTime targetLockStart = DateTime.Now;  // Target lock time
    private IContainer components;  // Designer stuff

    // Constants for window styles and hooks
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 524288;
    private const int WS_EX_TRANSPARENT = 32;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 256;
    private const int WM_KEYUP = 257;
    private const int VK_RSHIFT = 161;
    private const int VK_LSHIFT = 160;
    private const uint MOUSEEVENTF_MOVE = 1u;

    // DLL imports
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public Overlay(Memory memory, EntityManager entityManager)  // Constructor
    {
        InitializeComponent();
        this.memory = memory;
        this.entityManager = entityManager;
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
        renderTimer.Tick += (s, e) => Invalidate();

        if (Functions.BoxESPEnabled || Functions.BoneESPEnabled || Functions.AimAssistEnabled)
            renderTimer.Start();

        // Subscribe to setting changes
        Functions.BoxESPEnabledChanged += OnFeatureEnabledChanges;
        Functions.BoneESPEnabledChanged += OnFeatureEnabledChanges;
        Functions.AimAssistEnabledChanged += OnFeatureEnabledChanges;

        // Set up keyboard hook
        Console.WriteLine("[i]: Initializing keyboard hook for CS2...");
        keyboardProcDelegate = KeyboardProc;
        Process process = Process.GetCurrentProcess();
        ProcessModule mainModule = process.MainModule;
        keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProcDelegate, GetModuleHandle(mainModule.ModuleName), 0u);
        if (keyboardHookId == IntPtr.Zero)
            Console.WriteLine($"[i]: Keyboard hook failed, error: {Marshal.GetLastWin32Error()}");
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)  // Hook callback
    {
        if (nCode >= 0 && Functions.AimAssistEnabled)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int toggleKey = GetToggleKeyCode();
            bool isDown = wParam == (IntPtr)WM_KEYDOWN;
            if (vkCode == toggleKey)
                toggleKeyPressed = isDown;
        }
        return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
    }

    private void OnFeatureEnabledChanges(object sender, EventArgs e)  // Handle feature toggles
    {
        if (Functions.BoxESPEnabled || Functions.BoneESPEnabled || Functions.AimAssistEnabled)
        {
            renderTimer.Start();
            Invalidate();
        }
        else
        {
            renderTimer.Stop();
            Invalidate();
        }
    }

    private void Overlay_Load(object sender, EventArgs e)  // Form load - set up overlay
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        BackColor = Color.White;
        TransparencyKey = Color.White;
        Bounds = Screen.PrimaryScreen.Bounds;
        int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    private int GetToggleKeyCode()  // Get key code based on setting
    {
        string key = Functions.AimAssistToggleKey;

        // Gestion spéciale pour certains alias courants
        if (string.Equals(key, "Right_Shift", StringComparison.OrdinalIgnoreCase)) return VK_RSHIFT;
        if (string.Equals(key, "Left_Shift", StringComparison.OrdinalIgnoreCase)) return VK_LSHIFT;
        if (string.Equals(key, "Caps_Lock", StringComparison.OrdinalIgnoreCase)) return (int)Keys.CapsLock;

        // Essaye de parser le nom de la touche via l'énum Keys
        try
        {
            Keys parsedKey = (Keys)Enum.Parse(typeof(Keys), key, true);
            return (int)parsedKey;
        }
        catch
        {
            // Si la conversion échoue, fallback sur Left Shift
            return VK_LSHIFT;
        }
    }

    private bool IsToggleKeyPressedFallback()  // Fallback key check
    {
        int toggleKey = GetToggleKeyCode();
        short asyncState = GetAsyncKeyState(toggleKey);
        short keyState = GetKeyState(toggleKey);
        bool capsLock = Control.IsKeyLocked(Keys.Capital) && Functions.AimAssistToggleKey == "Caps_Lock";
        return ((asyncState & 0x8000) != 0) || ((keyState & 0x8000) != 0) || capsLock;
    }

    protected override void OnPaint(PaintEventArgs e)  // Drawing logic
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = (SmoothingMode)Functions.SmoothingMode;

        // Update pen if needed
        if (espPen == null || espPen.Color != Functions.SelectedColor || espPen.Width != Functions.ESPThickness)
            espPen = new Pen(Functions.SelectedColor, Functions.ESPThickness);

        Process proc = memory.GetProcess();
        bool isForeground = false;
        IntPtr fgWindow = GetForegroundWindow();
        if (fgWindow != IntPtr.Zero)
        {
            GetWindowThreadProcessId(fgWindow, out uint pid);
            isForeground = pid == (uint)proc.Id;
        }

        if (!isForeground) return;

        Entity local = entityManager.LocalPlayer;
        List<Entity> ents = entityManager.Entities;
        if (local.PawnAddress == IntPtr.Zero) return;

        Vector2 screenCenter = new Vector2(Width / 2f, Height / 2f);
        bool aimToggle = toggleKeyPressed || IsToggleKeyPressedFallback();
        Entity closestTarget = null;
        float minDist = float.MaxValue;

        foreach (Entity ent in ents)
        {
            if (ent.PawnAddress == IntPtr.Zero || ent.health <= 0 || ent.team == local.team) continue;

            if (Functions.BoxESPEnabled)
            {
                float height = Math.Abs(ent.position2D.Y - ent.head2D.Y);
                height = Math.Max(height, 30f);
                float width = height * 1.2f;
                float halfWidth = width * 0.5f;
                float x = ent.head2D.X - halfWidth / 2f;
                float y = ent.head2D.Y - (width - height);
                if (x < 0 || y < 0 || x + halfWidth > Width || y + width > Height || float.IsNaN(x) || float.IsNaN(y)) continue;
                g.DrawRectangle(espPen, x, y, halfWidth, width);
            }

            if (Functions.BoneESPEnabled && ent.bones2D != null && ent.bones2D.Count >= 18) // Not Perfect
            {
                (int, int)[] boneConnections = new (int, int)[17]
                {
                    (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (4, 6), (6, 7), (7, 8), (8, 9),
                    (4, 10), (10, 11), (11, 12), (12, 13), (0, 14), (14, 15), (0, 16), (16, 17)
                };
                for (int i = 0; i < boneConnections.Length; i++)
                {
                    var (b1, b2) = boneConnections[i];
                    if (b1 < ent.bones2D.Count && b2 < ent.bones2D.Count)
                    {
                        Vector2 p1 = ent.bones2D[b1];
                        Vector2 p2 = ent.bones2D[b2];
                        if (p1.X != -99f && p1.Y != -99f && p2.X != -99f && p2.Y != -99f &&
                            !float.IsNaN(p1.X) && !float.IsNaN(p1.Y) && !float.IsNaN(p2.X) && !float.IsNaN(p2.Y))
                        {
                            g.DrawLine(espPen, p1.X, p1.Y, p2.X, p2.Y);
                        }
                    }
                }
            }

            if (!Functions.AimAssistEnabled || !aimToggle || ent.head2D.X == -99f || ent.head2D.Y == -99f ||
                float.IsNaN(ent.head2D.X) || float.IsNaN(ent.head2D.Y)) continue;

            float distToCenter = Vector2.Distance(screenCenter, ent.head2D);
            float fovRadius = Functions.AimAssistFOVSize * 20f;
            if (distToCenter <= fovRadius && distToCenter < minDist)
            {
                minDist = distToCenter;
                closestTarget = ent;
            }
        }

        if (Functions.AimAssistEnabled && aimToggle)
        {
            float fovRadius = Functions.AimAssistFOVSize * 20f;
            Pen fovPen = new Pen(Color.FromArgb(75, 0, 0, 0), 1f);
            g.DrawEllipse(fovPen, screenCenter.X - fovRadius, screenCenter.Y - fovRadius, fovRadius * 2f, fovRadius * 2f);
        }

        if (Functions.AimAssistEnabled && aimToggle && closestTarget != null) // Way too humanized lmfao
        {
            Vector2 targetPoint;

            // Utilisation du bone configuré si possible, sinon fallback sur head2D
            if (closestTarget.bones2D != null
                && Functions.AimBoneId >= 0
                && Functions.AimBoneId < closestTarget.bones2D.Count)
            {
                targetPoint = closestTarget.bones2D[Functions.AimBoneId];
            }
            else
            {
                targetPoint = closestTarget.head2D;
            }

            // Humanize aim point
            targetPoint.Y -= 4f + (float)rand.NextDouble() * 2f;

            Vector2 delta = targetPoint - screenCenter;
            float deltaLen = delta.Length();
            float angle = (float)Math.Atan2(delta.Y, delta.X);

            double lockTimeMs = (DateTime.Now - targetLockStart).TotalMilliseconds;
            float lockFactor = (lockTimeMs < 150.0) ? Memory.Clamp((float)(1.5 - lockTimeMs / 100.0), 0.3f, 1f) : 1f;
            delta *= lockFactor;

            Vector2 aimDelta = new Vector2((float)(Math.Cos(angle) * deltaLen), (float)(Math.Sin(angle) * deltaLen));
            float smooth = Functions.AimAssistSmoothing / 100f;
            smooth = Memory.Clamp(smooth, 0.05f, 0.95f);
            float accel = 1f - (float)Math.Pow(smooth, 2.3);

            currentVelocity = currentVelocity * 0.85f + aimDelta * accel * 0.15f;
            Vector2 move = currentVelocity;
            float maxSpeed = 13f;
            move.X = Memory.Clamp(move.X, -maxSpeed, maxSpeed);
            move.Y = Memory.Clamp(move.Y, -maxSpeed, maxSpeed);

            mouse_event(MOUSEEVENTF_MOVE, (uint)move.X, (uint)move.Y, 0u, 0);
            Thread.Sleep(rand.Next(1, 4));  // Small delay for human feel
        }
    }

    protected override void Dispose(bool disposing)  // Cleanup
    {
        if (disposing)
        {
            if (keyboardHookId != IntPtr.Zero) UnhookWindowsHookEx(keyboardHookId);
            renderTimer.Stop();
            renderTimer.Dispose();
            espPen?.Dispose();
            Functions.BoxESPEnabledChanged -= OnFeatureEnabledChanges;
            Functions.BoneESPEnabledChanged -= OnFeatureEnabledChanges;
            Functions.AimAssistEnabledChanged -= OnFeatureEnabledChanges;
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()  // Designer generated
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1920, 1080);
        this.FormBorderStyle = FormBorderStyle.None;
        this.Name = "Overlay";
        this.Text = "Microsoft.COM.Surogate";
        this.TopMost = true;
        this.Load += new EventHandler(this.Overlay_Load);
        this.ResumeLayout(false);
    }
}