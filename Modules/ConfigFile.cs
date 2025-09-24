using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public class FunctionsConfig
{
    public int[] SelectedColorRGBA = new int[4] { 255, 0, 0, 255 };
    public int ESPThickness = 1;
    public bool BoxESPEnabled = false;
    public bool BoneESPEnabled = false;
    public bool AimAssistEnabled = true;
    public int AimAssistFOVSize = 3;
    public int AimAssistSmoothing = 50;
    public string AimAssistToggleKey = "Left_Shift";
    public int SmoothingMode = 4; // 4 antialias (see SmoothingMode enum System.Drawing.Drawing2D) 
    public int AimBoneId = 9; // Par défaut: 9 (Head)
}

public static class ConfigFile
{
    public static FunctionsConfig Load(string path)
    {
        var config = new FunctionsConfig();
        if (!File.Exists(path))
            return config;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            var parts = trimmed.Split(new[] { '=' }, 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "SmoothingMode":
                    int.TryParse(value, out config.SmoothingMode);
                    break;
                case "SelectedColorRGBA":
                    var vals = value.Split(',');
                    if (vals.Length == 4)
                    {
                        for (int i = 0; i < 4; i++)
                            int.TryParse(vals[i], out config.SelectedColorRGBA[i]);
                    }
                    break;
                case "ESPThickness":
                    int.TryParse(value, out config.ESPThickness);
                    break;
                case "BoxESPEnabled":
                    bool.TryParse(value, out config.BoxESPEnabled);
                    break;
                case "BoneESPEnabled":
                    bool.TryParse(value, out config.BoneESPEnabled);
                    break;
                case "AimAssistEnabled":
                    bool.TryParse(value, out config.AimAssistEnabled);
                    break;
                case "AimAssistFOVSize":
                    int.TryParse(value, out config.AimAssistFOVSize);
                    break;
                case "AimAssistSmoothing":
                    int.TryParse(value, out config.AimAssistSmoothing);
                    break;
                case "AimAssistToggleKey":
                    config.AimAssistToggleKey = value;
                    break;
                case "AimBoneId":
                    int.TryParse(value, out config.AimBoneId);
                    break;
            }
        }
        return config;
    }

    public static void Save(string path, FunctionsConfig config)
    {
        var lines = new List<string>
        {
            "# Smokey-Sense configuration",
            "SelectedColorRGBA=" + string.Join(",", config.SelectedColorRGBA),
            "ESPThickness=" + config.ESPThickness,
            "BoxESPEnabled=" + config.BoxESPEnabled.ToString().ToLower(),
            "BoneESPEnabled=" + config.BoneESPEnabled.ToString().ToLower(),
            "AimAssistEnabled=" + config.AimAssistEnabled.ToString().ToLower(),
            "AimAssistFOVSize=" + config.AimAssistFOVSize,
            "AimAssistSmoothing=" + config.AimAssistSmoothing,
            "AimAssistToggleKey=" + config.AimAssistToggleKey,
            "SmoothingMode=" + config.SmoothingMode,
            "AimBoneId=" + config.AimBoneId
        };
        File.WriteAllLines(path, lines.ToArray());
    }
}