using Microsoft.COM.Surogate;
using Microsoft.COM.Surogate.Data;
using Microsoft.COM.Surogate.Modules;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;
using SharpDXVector3 = SharpDX.Vector3;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using System.Collections.Concurrent;

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

    // === Ajouts: VB dynamique persistant + staging + cache visibilité ===
    private Buffer dynamicVertexBuffer;
    private int maxVertices = 65536; // capacité initiale (augmentée à la volée si nécessaire)
    private Vertex[] vertexStaging;
    private readonly List<Vertex> frameVertices = new List<Vertex>(4096);
    // Modifié: on stocke la position entité + locale
    private struct VisCacheEntry { public NumericsVector3 EntPos; public NumericsVector3 LocalPos; public bool Visible; public int Stamp; }
    private readonly Dictionary<IntPtr, VisCacheEntry> visCache = new Dictionary<IntPtr, VisCacheEntry>(128);

    // Connections d’os statiques (évite l’allocation par frame)
    private static readonly (int, int)[] BoneConnections = new (int, int)[17]
    {
        (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (4, 6), (6, 7), (7, 8), (8, 9),
        (4, 10), (10, 11), (11, 12), (12, 13), (0, 14), (14, 15), (0, 16), (16, 17)
    };

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

        // === Init VB dynamique persistant ===
        var vbDesc = new BufferDescription
        {
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.VertexBuffer,
            CpuAccessFlags = CpuAccessFlags.Write,
            OptionFlags = ResourceOptionFlags.None,
            SizeInBytes = Utilities.SizeOf<Vertex>() * maxVertices
        };
        dynamicVertexBuffer = new Buffer(d3dDevice, vbDesc);
        vertexStaging = new Vertex[maxVertices];

        Thread renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        renderThread.Start();
        Console.Write(" (✓)\n");

        Console.Write("[i]: Initializing Map Handler!");
        Thread mapHandlerThread = new Thread(BackgroundMapHandler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        mapHandlerThread.Start();
        Console.Write(" (✓)\n");

        Console.WriteLine("[i]: Initialization Complete. You may now play the game!");
        Thread.Sleep(3000);

        Thread menuThread = new Thread(Menu)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        menuThread.Start();
    }

    private void Menu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("\r\n     __                 _              __                     \r\n    / _\\_ __ ___   ___ | | _____ _   _/ _\\ ___ _ __   ___ ___ \r\n    \\ \\| '_ ` _ \\ / _ \\| |/ / _ \\ | | \\ \\ / _ \\ '_ \\ / __/ _ \\r\n    _\\ \\ | | | | | (_) |   <  __/ |_| |\\ \\  __/ | | | (_|  __/\r\n    \\__/_| |_| |_|\\___/|_|\\_\\___|\\__, \\__/\\___|_| |_|\\___\\___| v1.2 BETA\r\n                                 |___/                        ");
            if (Functions.BoxESPEnabled) { Console.WriteLine($"\n[1]: BoxESP (ON)"); } else { Console.WriteLine($"\n[1]: BoxESP (OFF)"); }
            if (Functions.BoneESPEnabled) { Console.WriteLine($"[2]: BoneESP (ON)"); } else { Console.WriteLine($"[2]: BoneESP (OFF)"); }
            if (Functions.AimAssistEnabled) { Console.WriteLine($"[3]: AimAssist (ON)"); } else { Console.WriteLine($"[3]: AimAssist (OFF)"); }
            if (Functions.RecoilControlEnabled) { Console.WriteLine($"[4]: RCS (ON)"); } else { Console.WriteLine($"[4]: RCS (OFF)"); }
            Console.Write($"-> ");
            string input = Console.ReadLine();
            if (input == "1")
            {
                Functions.BoxESPEnabled = !Functions.BoxESPEnabled;
            }
            if (input == "2")
            {
                Functions.BoneESPEnabled = !Functions.BoneESPEnabled;
            }
            if (input == "3")
            {
                Functions.AimAssistEnabled = !Functions.AimAssistEnabled;
            }
            if (input == "4")
            {
                Functions.RecoilControlEnabled = !Functions.RecoilControlEnabled;
            }
        }
    }

    private void RenderLoop()
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (running)
        {
            sw.Restart();
            RenderFrame();
            long elapsedMs = sw.ElapsedTicks * 1000 / Stopwatch.Frequency;
            int targetMs = 7; // ~144 FPS
            if (elapsedMs < targetMs)
                Thread.Sleep((int)(targetMs - elapsedMs));
            Logger.LogDebug($"Render loop {1000 / sw.ElapsedMilliseconds} fps");
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

            Entity local = entityManager.LocalPlayer;
            List<Entity> ents = entityManager.Entities;
            if (local.PawnAddress == IntPtr.Zero)
            {
                swapChain.Present(0, PresentFlags.None);
                return;
            }

            NumericsVector2 screenCenter = new NumericsVector2(width / 2f, height / 2f);
            Entity closestTarget = null;
            float minDist = float.MaxValue;

            // Couleurs locales, éviter de muter Functions.SelectedColorRGBA dans la boucle
            RawColorBGRA colorSelected = new RawColorBGRA((byte)Functions.SelectedColorRGBA[0], (byte)Functions.SelectedColorRGBA[1], (byte)Functions.SelectedColorRGBA[2], (byte)Functions.SelectedColorRGBA[3]);
            RawColorBGRA colorVisible = new RawColorBGRA(0, 255, 0, 255);
            RawColorBGRA colorHidden = new RawColorBGRA(0, 0, 0, 255);

            frameVertices.Clear();

            long visCheckMs = -1;
            bool visMeasured = false;
            int nowTick = Environment.TickCount;

            // 1) Prépare les boîtes et décide qui a besoin d'un vischeck (cache TTL)
            var boxes = new List<(Entity ent, float nx1, float ny1, float nx2, float ny2, bool? cachedVisible)>(ents.Count);
            var toCompute = new List<Entity>(32);

            // Seuils (au carré) pour invalider sur mouvement
            const float entPosEpsSq = 0.01f;   // ~1 cm^2 si unités = mètres (à ajuster)
            const float localPosEpsSq = 0.01f; // idem pour le joueur local

            foreach (Entity ent in ents)
            {
                if (ent.PawnAddress == IntPtr.Zero || ent.health <= 0 || ent.team == local.team) continue;

                if (Functions.BoxESPEnabled && ent.bones2D != null && ent.bones2D.Count > 0)
                {
                    float headY = ent.head2D.Y;
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;

                    for (int i = 0; i < ent.bones2D.Count; i++)
                    {
                        var bone = ent.bones2D[i];
                        if (float.IsNaN(bone.X) || float.IsNaN(bone.Y) || bone.X == -99f || bone.Y == -99f) continue;
                        if (bone.X < minX) minX = bone.X;
                        if (bone.X > maxX) maxX = bone.X;
                        if (bone.Y < minY) minY = bone.Y;
                        if (bone.Y > maxY) maxY = bone.Y;
                    }

                    if (minX == float.MaxValue || maxX == float.MinValue || minY == float.MaxValue || maxY == float.MinValue) continue;

                    float rawHeight = maxY - headY;
                    if (rawHeight < 10f) continue;

                    float paddingTop = rawHeight * 0.12f;
                    float paddingBottom = rawHeight * 0.09f;

                    float boxY = headY - paddingTop;
                    float boxHeight = rawHeight + paddingTop + paddingBottom;
                    float boxWidth = (maxX - minX) * 1.16f;
                    float boxX = (minX + maxX) / 2f - (boxWidth / 2f);

                    // Clip simple
                    if (boxX < 0 || boxY < 0 || boxX + boxWidth > width || boxY + boxHeight > height)
                        continue;

                    // NDC immédiat
                    float nx1 = (boxX / width) * 2f - 1f;
                    float ny1 = 1f - (boxY / height) * 2f;
                    float nx2 = ((boxX + boxWidth) / width) * 2f - 1f;
                    float ny2 = 1f - ((boxY + boxHeight) / height) * 2f;

                    // Cache TTL 75ms + seuil mouvement (entité ET joueur local)
                    bool? cachedVisible = null;
                    VisCacheEntry cached;
                    if (visCache.TryGetValue(ent.PawnAddress, out cached)
                        && (nowTick - cached.Stamp) <= 75
                        && NumericsVector3.DistanceSquared(cached.EntPos, ent.position) <= entPosEpsSq
                        && NumericsVector3.DistanceSquared(cached.LocalPos, local.position) <= localPosEpsSq
                        )
                    {
                        cachedVisible = cached.Visible;
                    }
                    else
                    {
                        toCompute.Add(ent);
                    }

                    boxes.Add((ent, nx1, ny1, nx2, ny2, cachedVisible));
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

            // 2) VisCheck parallèle pour ceux qui ne sont pas en cache
            var visResults = new ConcurrentDictionary<IntPtr, bool>();
            if (toCompute.Count > 0)
            {
                var swv = Stopwatch.StartNew();
                Parallel.ForEach(
                    toCompute,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                    ent =>
                    {
                        bool v = VisCheck.IsVisible(local.position, ent.position);
                        visResults[ent.PawnAddress] = v;
                    });
                swv.Stop();
                visCheckMs = swv.ElapsedMilliseconds;
                visMeasured = true;
            }

            // 3) Ajoute les vertices en utilisant les résultats (cache + parallèles)
            for (int i = 0; i < boxes.Count; i++)
            {
                var item = boxes[i];
                bool visible = item.cachedVisible ?? visResults.GetOrAdd(item.ent.PawnAddress, _ => true);

                // Met à jour le cache (thread unique ici) avec entité ET local
                visCache[item.ent.PawnAddress] = new VisCacheEntry { EntPos = item.ent.position, LocalPos = local.position, Visible = visible, Stamp = nowTick };

                var drawColor = visible ? colorVisible : colorHidden;
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx1, item.ny1, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx2, item.ny1, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx2, item.ny1, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx2, item.ny2, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx2, item.ny2, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx1, item.ny2, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx1, item.ny2, 0), Color = drawColor });
                frameVertices.Add(new Vertex { Position = new SharpDXVector3(item.nx1, item.ny1, 0), Color = drawColor });
            }

            // Bones (séquentiel, faible coût et inchangé)
            if (Functions.BoneESPEnabled)
            {
                foreach (Entity ent in ents)
                {
                    if (ent.PawnAddress == IntPtr.Zero || ent.health <= 0 || ent.team == local.team) continue;
                    if (ent.bones2D == null || ent.bones2D.Count == 0) continue;

                    for (int i = 0; i < BoneConnections.Length; i++)
                    {
                        var (b1, b2) = BoneConnections[i];
                        if (b1 >= ent.bones2D.Count || b2 >= ent.bones2D.Count) continue;
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

                        frameVertices.Add(new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = colorSelected });
                        frameVertices.Add(new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = colorSelected });
                    }
                }
            }

            if (visCheckMs >= 0)
            {
                Console.Title = VisCheck.modelReady
                    ? $"Microsoft.COM.Surogate - Model Active: {visCheckMs}ms"
                    : $"Microsoft.COM.Surogate - Model Loading: {visCheckMs}ms";
            }

            if (Functions.AimAssistEnabled)
            {
                float fovRadius = Functions.AimAssistFOVSize * 20f;
                RawColorBGRA fovColor = colorSelected;
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
                        continue;

                    float nx1 = (x1 / width) * 2f - 1f;
                    float ny1 = 1f - (y1 / height) * 2f;
                    float nx2 = (x2 / width) * 2f - 1f;
                    float ny2 = 1f - (y2 / height) * 2f;

                    frameVertices.Add(new Vertex { Position = new SharpDXVector3(nx1, ny1, 0), Color = fovColor });
                    frameVertices.Add(new Vertex { Position = new SharpDXVector3(nx2, ny2, 0), Color = fovColor });
                }
            }

            int vertexCount = frameVertices.Count;
            if (vertexCount > 0)
            {
                // Resize si nécessaire
                if (vertexCount > maxVertices)
                {
                    dynamicVertexBuffer.Dispose();
                    maxVertices = NextPow2(vertexCount);
                    var vbDescGrow = new BufferDescription
                    {
                        Usage = ResourceUsage.Dynamic,
                        BindFlags = BindFlags.VertexBuffer,
                        CpuAccessFlags = CpuAccessFlags.Write,
                        OptionFlags = ResourceOptionFlags.None,
                        SizeInBytes = Utilities.SizeOf<Vertex>() * maxVertices
                    };
                    dynamicVertexBuffer = new Buffer(d3dDevice, vbDescGrow);
                    vertexStaging = new Vertex[maxVertices];
                }

                // Copier dans le staging réutilisable (évite ToArray chaque frame)
                frameVertices.CopyTo(0, vertexStaging, 0, vertexCount);

                var box = deviceContext.MapSubresource(dynamicVertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                Utilities.Write(box.DataPointer, vertexStaging, 0, vertexCount);
                deviceContext.UnmapSubresource(dynamicVertexBuffer, 0);

                deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(dynamicVertexBuffer, Utilities.SizeOf<Vertex>(), 0));
                deviceContext.Draw(vertexCount, 0);
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

    private static int NextPow2(int v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v++;
        return v;
    }

    private void BackgroundMapHandler()
    {
        while (true)
        {
            try
            {
                IntPtr globalVars = memory.ReadPointer(memory.GetModuleBase() + Offsets.dwGlobalVars);
                IntPtr currentMapNamePtr = memory.ReadPointer(globalVars + 0x180);
                VisCheck.currentMap = memory.ReadString(currentMapNamePtr);

                if (string.IsNullOrEmpty(VisCheck.currentMap) || VisCheck.currentMap == "<empty>")
                {
                    VisCheck.oldMap = VisCheck.currentMap;
                    VisCheck.modelReady = false;
                }
                else if (VisCheck.currentMap != VisCheck.oldMap)
                {
                    VisCheck.GetMapData();
                    VisCheck.LoadBVHForMap();
                    VisCheck.oldMap = VisCheck.currentMap;
                }
            }
            catch { /* éviter de planter le thread si le jeu charge */ }

            // Poll plus lent (diminue la charge CPU en continu)
            Thread.Sleep(250);
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
        if (dynamicVertexBuffer != null)
        {
            dynamicVertexBuffer.Dispose();
            dynamicVertexBuffer = null;
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