// Uses events to notify when settings change, so UI or other parts can react.
// I made this static for global access without instances.

using System;
using System.Drawing;
using Microsoft.COM.Surogate.Modules;
using System.IO;

public static class Functions
{
    private static readonly string ConfigFilePath = "config.txt";
    private static FileSystemWatcher _configWatcher;

    private static Color _selectedColor = Color.Red;  // Default color for ESP
    private static int[] _selectedColorRGBA = new int[4] { 255, 0, 0, 255 };  // RGBA representation
    private static int _espThickness = 1;  // Line thickness for ESP
    private static bool _espEnabled = true;  // Box ESP toggle (disabled by default due to performance.. mabye using GDI was a bad idea after all)
    private static bool _boneESPEnabled = false;  // Bone ESP toggle
    private static bool _aimAssistEnabled = true;  // Aim assist toggle
    private static int _aimAssistFOVSize = 3;  // FOV size for aim assist
    private static int _aimAssistSmoothing = 50;  // Smoothing factor
    private static string _aimAssistToggleKey = "Left_Shift";  // Toggle key
    private static int _smoothingMode = 4; // Default smoothing mode (anti alias)
    private static int _aimBoneId = 9; // Par défaut: 9 (ex: Head pour CS2)

    public static Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                SelectedColorChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int[] SelectedColorRGBA
    {
        get => _selectedColorRGBA;
        set
        {
            if (value != null && value.Length == 4 &&
                (_selectedColorRGBA[0] != value[0] || _selectedColorRGBA[1] != value[1] ||
                 _selectedColorRGBA[2] != value[2] || _selectedColorRGBA[3] != value[3]))
            {
                _selectedColorRGBA = value;
                SelectedColorRGBAChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int ESPThickness
    {
        get => _espThickness;
        set
        {
            if (_espThickness != value)
            {
                _espThickness = value;
                ESPThicknessChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static bool BoxESPEnabled
    {
        get => _espEnabled;
        set
        {
            if (_espEnabled != value)
            {
                _espEnabled = value;
                BoxESPEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static bool BoneESPEnabled
    {
        get => _boneESPEnabled;
        set
        {
            if (_boneESPEnabled != value)
            {
                _boneESPEnabled = value;
                BoneESPEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static bool AimAssistEnabled
    {
        get => _aimAssistEnabled;
        set
        {
            if (_aimAssistEnabled != value)
            {
                _aimAssistEnabled = value;
                AimAssistEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int AimAssistFOVSize
    {
        get => _aimAssistFOVSize;
        set
        {
            if (_aimAssistFOVSize != value)
            {
                _aimAssistFOVSize = value;
                AimAssistFOVSizeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int AimAssistSmoothing
    {
        get => _aimAssistSmoothing;
        set
        {
            if (_aimAssistSmoothing != value)
            {
                _aimAssistSmoothing = value;
                AimAssistSmoothingChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static string AimAssistToggleKey
    {
        get => _aimAssistToggleKey;
        set
        {
            if (_aimAssistToggleKey != value)
            {
                _aimAssistToggleKey = value;
                AimAssistToggleKeyChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int SmoothingMode
    {
        get => _smoothingMode;
        set
        {
            if (_smoothingMode != value)
            {
                _smoothingMode = value;
                SmoothingModeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static int AimBoneId
    {
        get => _aimBoneId;
        set
        {
            if (_aimBoneId != value)
            {
                _aimBoneId = value;
                AimBoneIdChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static void LoadConfig()
    {
        var config = ConfigFile.Load(ConfigFilePath);
        SelectedColorRGBA = config.SelectedColorRGBA;
        ESPThickness = config.ESPThickness;
        BoxESPEnabled = config.BoxESPEnabled;
        BoneESPEnabled = config.BoneESPEnabled;
        AimAssistEnabled = config.AimAssistEnabled;
        AimAssistFOVSize = config.AimAssistFOVSize;
        AimAssistSmoothing = config.AimAssistSmoothing;
        AimAssistToggleKey = config.AimAssistToggleKey;
        AimBoneId = config.AimBoneId;
        SmoothingMode = config.SmoothingMode;
        SelectedColor = Color.FromArgb(
            config.SelectedColorRGBA[3], // Alpha channel
            config.SelectedColorRGBA[0], // Red channel
            config.SelectedColorRGBA[1], // Green channel
            config.SelectedColorRGBA[2] // Blue channel
        );
    }

    public static void SaveConfig()
    {
        var config = new FunctionsConfig
        {
            SelectedColorRGBA = SelectedColorRGBA,
            ESPThickness = ESPThickness,
            BoxESPEnabled = BoxESPEnabled,
            BoneESPEnabled = BoneESPEnabled,
            AimAssistEnabled = AimAssistEnabled,
            AimAssistFOVSize = AimAssistFOVSize,
            AimAssistSmoothing = AimAssistSmoothing,
            AimAssistToggleKey = AimAssistToggleKey,
            SmoothingMode = SmoothingMode,
            AimBoneId = AimBoneId
        };
        ConfigFile.Save(ConfigFilePath, config);

        Console.WriteLine("Config file saved");
    }

    public static void StartConfigWatcher()
    {
        if (_configWatcher != null) return; // allready started démarré

        _configWatcher = new FileSystemWatcher
        {
            Path = ".",
            Filter = Path.GetFileName(ConfigFilePath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _configWatcher.Changed += (s, e) =>
        {
            try
            {
                // Small break to avoid concurent accès during writing
                System.Threading.Thread.Sleep(100);
                LoadConfig();
                Console.WriteLine("Config file reloaded (auto).");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Config reload error: " + ex.Message);
            }
        };
        _configWatcher.EnableRaisingEvents = true;
    }

    public static void StopConfigWatcher()
    {
        if (_configWatcher != null)
        {
            _configWatcher.EnableRaisingEvents = false;
            _configWatcher.Dispose();
            _configWatcher = null;
        }
    }

    // Events for setting changes
    public static event EventHandler SelectedColorChanged;
    public static event EventHandler SelectedColorRGBAChanged;
    public static event EventHandler ESPThicknessChanged;
    public static event EventHandler BoxESPEnabledChanged;
    public static event EventHandler BoneESPEnabledChanged;
    public static event EventHandler AimAssistEnabledChanged;
    public static event EventHandler AimAssistFOVSizeChanged;
    public static event EventHandler AimAssistSmoothingChanged;
    public static event EventHandler AimAssistToggleKeyChanged;
    public static event EventHandler SmoothingModeChanged;
    public static event EventHandler AimBoneIdChanged;
}