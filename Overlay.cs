using Microsoft.COM.Surogate;
using Microsoft.COM.Surogate.Data;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;
using SharpDXVector3 = SharpDX.Vector3;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

public class Overlay : IDisposable
{
    private readonly Memory memory;
    private readonly EntityManager entityManager;
    private static NumericsVector2 previousDelta = NumericsVector2.Zero;
    private static NumericsVector2 currentVelocity = NumericsVector2.Zero;
    private static readonly Random rand = new Random();
    private static Entity lastTarget = null;
    private static DateTime targetLockStart = DateTime.Now;
    private Device d3dDevice;
    private DeviceContext deviceContext;
    private SwapChain swapChain;
    private RenderTargetView renderTargetView;
    private IntPtr hWnd;
    private bool running;
    private Buffer vertexBuffer;
    private VertexShader vertexShader;
    private PixelShader pixelShader;
    private InputLayout inputLayout;
    private readonly WndProc wndProcDelegate;
    private RasterizerState rasterizerState;
    private BlendState blendState;
    private static Vector3 OldPunch = Vector3.Zero;

    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 256;
    private const int WM_KEYUP = 257;
    private const int VK_RSHIFT = 161;
    private const int VK_LSHIFT = 160;
    private const uint LWA_ALPHA = 0x2;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_CLOSE = 0x0010;
    private const int INPUT_KEYBOARD = 1;
    private const int INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_SPACE = 0x20;

    [DllImport("user32.dll")]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool RegisterClass(ref WNDCLASS wndClass);
    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);
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
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")]
    private static extern bool PostQuitMessage(int nExitCode);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public SharpDXVector3 Position;
        public RawColorBGRA Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_DESTROY:
            case WM_CLOSE:
                running = false;
                Dispose();
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public Overlay(Memory memory, EntityManager entityManager)
    {
        this.memory = memory;
        this.entityManager = entityManager;
        running = true;

        wndProcDelegate = CustomWndProc;
        WNDCLASS wndClass = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
            hInstance = GetModuleHandle(null),
            lpszClassName = "CS2Overlay"
        };
        RegisterClass(ref wndClass);

        System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.PrimaryScreen;
        hWnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            "CS2Overlay",
            "Microsoft.COM.Surogate",
            WS_POPUP,
            screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height,
            IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero
        );
        MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
        ShowWindow(hWnd, 5);
        Console.Write(" (✓)\n");

        Console.Write("[i]: Initializing Drawing API!");
        GetClientRect(hWnd, out RECT clientRect);
        var swapChainDesc = new SwapChainDescription
        {
            BufferCount = 1,
            ModeDescription = new ModeDescription(clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            IsWindowed = true,
            OutputHandle = hWnd,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            Usage = Usage.RenderTargetOutput
        };

        Device.CreateWithSwapChain(
            DriverType.Hardware,
            DeviceCreationFlags.None,
            swapChainDesc,
            out d3dDevice,
            out swapChain
        );
        deviceContext = d3dDevice.ImmediateContext;

        using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
        {
            renderTargetView = new RenderTargetView(d3dDevice, backBuffer);
        }

        var rasterizerDesc = new RasterizerStateDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None
        };
        rasterizerState = new RasterizerState(d3dDevice, rasterizerDesc);

        var blendDesc = new BlendStateDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false
        };
        blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
        {
            IsBlendEnabled = true,
            SourceBlend = BlendOption.SourceAlpha,
            DestinationBlend = BlendOption.InverseSourceAlpha,
            BlendOperation = BlendOperation.Add,
            SourceAlphaBlend = BlendOption.One,
            DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
            AlphaBlendOperation = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteMaskFlags.All
        };
        blendState = new BlendState(d3dDevice, blendDesc);

        string hlslCode = @"
            struct VS_INPUT {
                float4 Position : POSITION;
                float4 Color : COLOR;
            };
            struct PS_INPUT {
                float4 Position : SV_POSITION;
                float4 Color : COLOR;
            };
            PS_INPUT VS(VS_INPUT input) {
                PS_INPUT output;
                output.Position = input.Position;
                output.Color = input.Color;
                return output;
            }
            float4 PS(PS_INPUT input) : SV_TARGET {
                return input.Color;
            }";

        using (var vsBytecode = ShaderBytecode.Compile(hlslCode, "VS", "vs_4_0"))
        {
            vertexShader = new VertexShader(d3dDevice, vsBytecode);
            var inputElements = new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 12, 0)
            };
            inputLayout = new InputLayout(d3dDevice, vsBytecode.Bytecode, inputElements);
        }
        using (var psBytecode = ShaderBytecode.Compile(hlslCode, "PS", "ps_4_0"))
        {
            pixelShader = new PixelShader(d3dDevice, psBytecode);
        }

        Thread renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        renderThread.Start();
        Console.Write(" (✓)\n");

        Console.Write("[i]: Initializing Map Handler!");
        Thread mapHandler = new Thread(BackgroundMapHandler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        mapHandler.Start();
        Console.Write(" (✓)\n");

        Console.WriteLine("[i]: Initialization Complete. You may now play the game!");
        Thread.Sleep(3000);

        Thread menuThread = new Thread(() =>
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("\r\n     __                 _              __                     \r\n    / _\\_ __ ___   ___ | | _____ _   _/ _\\ ___ _ __   ___ ___ \r\n    \\ \\| '_ ` _ \\ / _ \\| |/ / _ \\ | | \\ \\ / _ \\ '_ \\ / __/ _ \\\r\n    _\\ \\ | | | | | (_) |   <  __/ |_| |\\ \\  __/ | | | (_|  __/\r\n    \\__/_| |_| |_|\\___/|_|\\_\\___|\\__, \\__/\\___|_| |_|\\___\\___| v1.2 BETA\r\n                                 |___/                        ");
                if (Functions.BoxESPEnabled) { Console.WriteLine($"\n[1]: BoxESP (ON)"); } else { Console.WriteLine($"\n[1]: BoxESP (OFF)"); }
                if (Functions.BoneESPEnabled) { Console.WriteLine($"[2]: BoneESP (ON)"); } else { Console.WriteLine($"[2]: BoneESP (OFF)"); }
                if (Functions.AimAssistEnabled) { Console.WriteLine($"[3]: AimAssist (ON)"); } else { Console.WriteLine($"[3]: AimAssist (OFF)"); }
                if (Functions.RecoilControlEnabled) { Console.WriteLine($"[4]: RCS (ON)"); } else { Console.WriteLine($"[4]: RCS (OFF)"); }
                Console.Write($"-> ");
                string input = Console.ReadLine();
                if (input == "1")
                {
                    if (Functions.BoxESPEnabled) { Functions.BoxESPEnabled = false; } else { Functions.BoxESPEnabled = true; }
                }
                if (input == "2")
                {
                    if (Functions.BoneESPEnabled) { Functions.BoneESPEnabled = false; } else { Functions.BoneESPEnabled = true; }
                }
                if (input == "3")
                {
                    if (Functions.AimAssistEnabled) { Functions.AimAssistEnabled = false; } else { Functions.AimAssistEnabled = true; }
                }
                if (input == "4")
                {
                    if (Functions.RecoilControlEnabled) { Functions.RecoilControlEnabled = false; } else { Functions.RecoilControlEnabled = true; }
                }
            }
        });
        menuThread.IsBackground = true;
        menuThread.Priority = ThreadPriority.Normal;
        menuThread.Start();
    }

    private void RenderLoop()
    {
        while (running)
        {
            Stopwatch sw = Stopwatch.StartNew();
            RenderFrame();
            long elapsedMs = sw.ElapsedTicks * 1000 / Stopwatch.Frequency;
            // ~7 ms per frame = 144 FPS cap
            int targetMs = 7;
            if (elapsedMs < targetMs)
                Thread.Sleep((int)(targetMs - elapsedMs));
        }
    }

    private void RenderFrame()
    {
        if (d3dDevice == null || swapChain == null) return;

        try
        {
            GetClientRect(hWnd, out RECT clientRect);
            float width = clientRect.Right - clientRect.Left;
            float height = clientRect.Bottom - clientRect.Top;

            deviceContext.Rasterizer.SetViewport(new SharpDX.Viewport(0, 0, (int)width, (int)height, 0f, 1f));
            deviceContext.Rasterizer.State = rasterizerState;
            deviceContext.OutputMerger.SetBlendState(blendState, new RawColor4(0, 0, 0, 0));

            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            deviceContext.ClearRenderTargetView(renderTargetView, new RawColor4(0f, 0f, 0f, 0f));

            deviceContext.InputAssembler.InputLayout = inputLayout;
            deviceContext.VertexShader.Set(vertexShader);
            deviceContext.PixelShader.Set(pixelShader);
            deviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.LineList;

            Process proc = memory.GetProcess();
            bool isForeground = false;
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fgWindow, out uint pid);
                isForeground = pid == (uint)proc.Id;
            }

            if (!isForeground)
            {
                swapChain.Present(0, PresentFlags.None);
                return;
            }

            Entity local = entityManager.LocalPlayer;
            List<Entity> ents = entityManager.Entities;
            if (local.PawnAddress == IntPtr.Zero)
            {
                swapChain.Present(0, PresentFlags.None);
                return;
            }

            if (local.health == 0)
            {
                swapChain.Present(0, PresentFlags.None);
                return;
            }

            NumericsVector2 screenCenter = new NumericsVector2(width / 2f, height / 2f);
            Entity closestTarget = null;
            float minDist = float.MaxValue;

            int[] rgba = Functions.SelectedColorRGBA;
            RawColorBGRA matchColor = new RawColorBGRA((byte)rgba[0], (byte)rgba[1], (byte)rgba[2], (byte)rgba[3]);

            List<Vertex> vertices = new List<Vertex>();

            foreach (Entity ent in ents)
            {
                if (ent.PawnAddress == IntPtr.Zero || ent.health <= 0 || ent.team == local.team) continue;

                if (Functions.BoxESPEnabled && ent.bones2D != null && ent.bones2D.Count > 0) // Perfect, dont touch!
                {
                    // Calculate raw min/max
                    float headY = ent.head2D.Y;
                    float minX = float.MaxValue;
                    float maxX = float.MinValue;
                    float minY = float.MaxValue;
                    float maxY = float.MinValue;

                    foreach (var bone in ent.bones2D)
                    {
                        if (float.IsNaN(bone.X) || float.IsNaN(bone.Y) || bone.X == -99f || bone.Y == -99f)
                            continue;

                        if (bone.X < minX) minX = bone.X;
                        if (bone.X > maxX) maxX = bone.X;
                        if (bone.Y < minY) minY = bone.Y;
                        if (bone.Y > maxY) maxY = bone.Y;
                    }

                    if (minX == float.MaxValue || maxX == float.MinValue ||
                        minY == float.MaxValue || maxY == float.MinValue)
                        continue;

                    // Raw box from head to feet
                    float rawHeight = maxY - headY;
                    if (rawHeight < 10f) continue;

                    // Add a little padding
                    float paddingTop = rawHeight * 0.12f;   // 12% above head pos
                    float paddingBottom = rawHeight * 0.09f; // 9% below feet pos

                    float boxY = headY - paddingTop;
                    float boxHeight = rawHeight + paddingTop + paddingBottom;

                    float boxWidth = (maxX - minX) * 1.16f; // widen box slightly (16%)
                    float boxX = (minX + maxX) / 2f - (boxWidth / 2f);

                    // Clip check
                    if (boxX < 0 || boxY < 0 || boxX + boxWidth > width || boxY + boxHeight > height)
                        continue;

                    // Convert to NDC
                    float nx1 = (boxX / width) * 2f - 1f;
                    float ny1 = 1f - (boxY / height) * 2f;
                    float nx2 = ((boxX + boxWidth) / width) * 2f - 1f;
                    float ny2 = 1f - ((boxY + boxHeight) / height) * 2f;

                    vertices.AddRange(new[]
                    {
                        new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx2, ny1, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx2, ny1, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx1, ny2, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx1, ny2, 0), Color = matchColor },
                        new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = matchColor }
                    });
                }

                if (Functions.BoneESPEnabled && ent.bones2D != null && ent.bones2D.Count > 0) // Not Perfect yet, still has a weird zig zag in the back area!
                {
                    (int, int)[] boneConnections = new (int, int)[17]
                    {
                        (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (4, 6), (6, 7), (7, 8), (8, 9),
                        (4, 10), (10, 11), (11, 12), (12, 13), (0, 14), (14, 15), (0, 16), (16, 17)
                    };
                    for (int i = 0; i < boneConnections.Length; i++)
                    {
                        var (b1, b2) = boneConnections[i];
                        if (b1 >= ent.bones2D.Count || b2 >= ent.bones2D.Count)
                        {
                            continue;
                        }
                        NumericsVector2 p1 = ent.bones2D[b1];
                        NumericsVector2 p2 = ent.bones2D[b2];
                        if (p1.X == -99f || p1.Y == -99f || p2.X == -99f || p2.Y == -99f ||
                            float.IsNaN(p1.X) || float.IsNaN(p1.Y) || float.IsNaN(p2.X) || float.IsNaN(p2.Y))
                        {
                            continue;
                        }
                        float nx1 = (p1.X / width) * 2f - 1f;
                        float ny1 = 1f - (p1.Y / height) * 2f;
                        float nx2 = (p2.X / width) * 2f - 1f;
                        float ny2 = 1f - (p2.Y / height) * 2f;

                        vertices.AddRange(new[]
                        {
                            new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = matchColor },
                            new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = matchColor }
                        });
                    }
                }

                if (Functions.AimAssistEnabled && ent.head2D.X != -99f && ent.head2D.Y != -99f &&
                    !float.IsNaN(ent.head2D.X) && !float.IsNaN(ent.head2D.Y))
                {
                    float distToCenter = NumericsVector2.Distance(screenCenter, ent.head2D);
                    float fovRadius = Functions.AimAssistFOVSize * 20f;
                    if (distToCenter <= fovRadius && distToCenter < minDist)
                    {
                        minDist = distToCenter;
                        closestTarget = ent;
                    }
                }
            }

            if (Functions.AimAssistEnabled) // (AimAssist FOV Circle): Works Perfectly, dont touch! (Long term only show if toggle key is also pressed)
            {
                float fovRadius = Functions.AimAssistFOVSize * 20f;
                RawColorBGRA fovColor = matchColor; // Match selected color

                const int segments = 32;
                for (int i = 0; i < segments; i++)
                {
                    float angle1 = (float)(i * Math.PI * 2 / segments);
                    float angle2 = (float)((i + 1) * Math.PI * 2 / segments);
                    float x1 = screenCenter.X + fovRadius * (float)Math.Cos(angle1);
                    float y1 = screenCenter.Y + fovRadius * (float)Math.Sin(angle1);
                    float x2 = screenCenter.X + fovRadius * (float)Math.Cos(angle2);
                    float y2 = screenCenter.Y + fovRadius * (float)Math.Sin(angle2);

                    if (float.IsNaN(x1) || float.IsNaN(y1) || float.IsNaN(x2) || float.IsNaN(y2))
                    {
                        continue;
                    }

                    float nx1 = (x1 / width) * 2f - 1f;
                    float ny1 = 1f - (y1 / height) * 2f;
                    float nx2 = (x2 / width) * 2f - 1f;
                    float ny2 = 1f - (y2 / height) * 2f;

                    vertices.AddRange(new[]
                    {
                        new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = fovColor },
                        new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = fovColor }
                    });
                }
            }

            if (vertices.Count > 0)
            {
                using (var vb = Buffer.Create(d3dDevice, BindFlags.VertexBuffer, vertices.ToArray()))
                {
                    deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vb, Utilities.SizeOf<Vertex>(), 0));
                    deviceContext.Draw(vertices.Count, 0);
                }
            }

            if (Functions.AimAssistEnabled) 
            {
                // Add logic for vischeck from Vischeck.IsVisable() in the VisCheck.cs class
            }

            if (Functions.RecoilControlEnabled) 
            {
                //float Strength = 100f; // percent (1 == 100%, 0.5 == 50%)
                //float Smoothing = 5f;

                //// read punch angle
                //Vector3 rawPunch = memory.ReadVec(local.PawnAddress + Offsets.m_aimPunchAngle);
                //Console.WriteLine($"[RCS DEBUG] Raw m_aimPunchAngle: {rawPunch}");
                //Vector3 punch = rawPunch * Strength / 100f * 2f;

                //int shotsFired = memory.ReadInt(local.PawnAddress, Offsets.shotsFired);
                //Console.WriteLine($"[RCS DEBUG] ShotsFired: {shotsFired}");

                //if (shotsFired > 1)
                //{
                //    Vector3 currentAngles = memory.ReadVec(memory.GetModuleBase(), Offsets.dwViewAngles);

                //    // delta between previous punch and current punch
                //    Vector3 deltaPunch = punch - OldPunch;

                //    // calculate new angles
                //    Vector3 newAngles = currentAngles - deltaPunch;
                //    newAngles.X = Normalize(newAngles.X);
                //    newAngles.Y = Normalize(newAngles.Y);

                //    // compute float dx/dy before rounding
                //    float dxFloat = (newAngles.X - currentAngles.X); // no / Smoothing
                //    float dyFloat = (newAngles.Y - currentAngles.Y);

                //    int dx = (int)(dxFloat * 50f); // scale up for testing
                //    int dy = (int)(dyFloat * 50f);

                //    // debug logging
                //    Console.WriteLine($"[RCS DEBUG] CurrentAngles: {currentAngles}");
                //    Console.WriteLine($"[RCS DEBUG] Punch: {punch}, OldPunch: {OldPunch}, DeltaPunch: {deltaPunch}");
                //    Console.WriteLine($"[RCS DEBUG] NewAngles (after normalize): {newAngles}");
                //    Console.WriteLine($"[RCS DEBUG] dxFloat: {dxFloat:F4}, dyFloat: {dyFloat:F4}, dx: {dx}, dy: {dy}");

                //    // apply mouse move if non-zero
                //    if (dx != 0 || dy != 0)
                //    {
                //        MoveMouse(dx, dy);
                //        Console.WriteLine("[RCS DEBUG] MoveMouse called");
                //    }
                //    else
                //    {
                //        Console.WriteLine("[RCS DEBUG] dx/dy == 0 (no movement this tick)");
                //    }
                //}

                //// store last punch for next frame
                //OldPunch = punch;
                //Console.WriteLine($"[RCS DEBUG] OldPunch updated: {OldPunch}");
            }

            swapChain.Present(0, PresentFlags.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR]: " + ex.Message);
            Thread.Sleep(5000);
            Environment.Exit(1);
        }
    }

    public void BackgroundMapHandler()
    {
        while (true)
        {
            Thread.Sleep(3000);
            IntPtr globalVars = memory.ReadPointer(memory.GetModuleBase() + Offsets.dwGlobalVars);
            IntPtr currentMapName = memory.ReadPointer(globalVars + 0x188);
            string currentMap = memory.ReadString(currentMapName);
            Console.WriteLine($"[i]: Current Map Name: {currentMap}"); // NOT WORKING ;(

            // If we can get the correct map name we can then check if its one of the maps that we already have its GLB data too and if so set it as the current map for vischeck's IsVisable to be setup and work correctly
        }
    }

    private static void MoveMouse(int dx, int dy)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            dwFlags = MOUSEEVENTF_MOVE
        };
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static float Normalize(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    public void Run()
    {
        while (running)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    public void Dispose()
    {
        running = false;
        if (vertexBuffer != null)
        {
            vertexBuffer.Dispose();
            vertexBuffer = null;
        }
        if (inputLayout != null)
        {
            inputLayout.Dispose();
            inputLayout = null;
        }
        if (vertexShader != null)
        {
            vertexShader.Dispose();
            vertexShader = null;
        }
        if (pixelShader != null)
        {
            pixelShader.Dispose();
            pixelShader = null;
        }
        if (renderTargetView != null)
        {
            renderTargetView.Dispose();
            renderTargetView = null;
        }
        if (swapChain != null)
        {
            swapChain.Dispose();
            swapChain = null;
        }
        if (d3dDevice != null)
        {
            d3dDevice.Dispose();
            d3dDevice = null;
        }
        if (rasterizerState != null)
        {
            rasterizerState.Dispose();
            rasterizerState = null;
        }
        if (blendState != null)
        {
            blendState.Dispose();
            blendState = null;
        }
        if (hWnd != IntPtr.Zero)
        {
            DestroyWindow(hWnd);
            hWnd = IntPtr.Zero;
        }
    }
}