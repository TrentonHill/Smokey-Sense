// Uses events to notify when settings change, so UI or other parts can react.
// I made this static for global access without instances.

using System;
using System.Drawing;

public static class Functions
{
    private static Color _selectedColor = Color.Black;  // Default color for ESP
    private static int[] _selectedColorRGBA = new int[4] { 0, 0, 0, 255 };  // RGBA representation
    private static bool _boxESPEnabled = true;  // Box ESP toggle
    private static bool _boneESPEnabled = false;  // Bone ESP toggle
    private static bool _aimAssistEnabled = false;  // Aim assist toggle
    private static int _aimAssistFOVSize = 3;  // FOV size for aim assist
    private static int _aimAssistSmoothing = 50;  // Smoothing factor
    private static string _aimAssistToggleKey = "Left_Shift";  // Toggle key
    private static bool _recoilControlEnabled = false; // RCS Toggle

    public static bool RecoilControlEnabled
    {
        get => _recoilControlEnabled;
        set
        {
            if (_recoilControlEnabled != value)
            {
                _recoilControlEnabled = value;
                RecoilControlEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

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

    public static bool BoxESPEnabled
    {
        get => _boxESPEnabled;
        set
        {
            if (_boxESPEnabled != value)
            {
                _boxESPEnabled = value;
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

    // Events for setting changes
    public static event EventHandler SelectedColorChanged;
    public static event EventHandler SelectedColorRGBAChanged;
    public static event EventHandler BoxESPEnabledChanged;
    public static event EventHandler BoneESPEnabledChanged;
    public static event EventHandler AimAssistEnabledChanged;
    public static event EventHandler AimAssistFOVSizeChanged;
    public static event EventHandler AimAssistSmoothingChanged;
    public static event EventHandler AimAssistToggleKeyChanged;
    public static event EventHandler RecoilControlEnabledChanged;
}