using System;
using UnityEngine;

namespace LegoPuzzle.Data
{
    /// <summary>
    /// Provides standard and scientifically calibrated Colorblind-Safe (Okabe-Ito) color palettes
    /// for LEGO blocks and Exit Gates, ensuring high-contrast visibility for players with
    /// Protanopia, Deuteranopia, and Tritanopia.
    /// </summary>
    public static class ColorblindPalette
    {
        public const string PREFS_COLORBLIND_ENABLED = "LEGO_ColorblindEnabled";

        /// <summary>
        /// Returns true if Colorblind Mode is currently enabled.
        /// </summary>
        public static bool IsColorblindModeActive
        {
            get
            {
                if (Runtime.GameSettingsManager.HasInstance)
                {
                    return Runtime.GameSettingsManager.Instance.ColorblindModeEnabled;
                }
                return PlayerPrefs.GetInt(PREFS_COLORBLIND_ENABLED, 0) == 1;
            }
        }

        /// <summary>
        /// Gets the color for a LEGO piece based on its BlockColorType and active colorblind setting.
        /// </summary>
        public static Color GetPieceColor(BlockColorType colorType, Color customColor, bool? forceColorblind = null)
        {
            bool useColorblind = forceColorblind ?? IsColorblindModeActive;

            if (useColorblind)
            {
                // Scientifically validated Okabe-Ito / high luminance contrast palette
                return colorType switch
                {
                    BlockColorType.Red => new Color(0.85f, 0.37f, 0.00f),       // Vermilion / Red-Orange (#D55E00)
                    BlockColorType.Yellow => new Color(0.95f, 0.90f, 0.25f),    // High-Luminance Sun Yellow (#F0E442)
                    BlockColorType.Blue => new Color(0.00f, 0.45f, 0.70f),      // Cobalt / Royal Blue (#0072B2)
                    BlockColorType.Green => new Color(0.00f, 0.62f, 0.45f),     // Bluish Green / Mint (#009E73)
                    BlockColorType.Purple => new Color(0.80f, 0.47f, 0.65f),    // Reddish Purple / Plum (#CC79A7)
                    BlockColorType.Pink => new Color(0.95f, 0.40f, 0.65f),      // Bright Coral Pink
                    BlockColorType.Cyan => new Color(0.35f, 0.70f, 0.90f),      // Light Sky Blue (#56B4E9)
                    BlockColorType.Orange => new Color(0.90f, 0.60f, 0.00f),    // Amber Gold (#E69F00)
                    BlockColorType.Universal => new Color(0.98f, 0.98f, 0.98f), // Pure Pearl White
                    _ => customColor
                };
            }
            else
            {
                // Classic LEGO vibrant primary palette
                return colorType switch
                {
                    BlockColorType.Red => new Color(0.92f, 0.22f, 0.22f),
                    BlockColorType.Yellow => new Color(0.98f, 0.85f, 0.15f),
                    BlockColorType.Blue => new Color(0.18f, 0.48f, 0.95f),
                    BlockColorType.Green => new Color(0.28f, 0.85f, 0.28f),
                    BlockColorType.Purple => new Color(0.68f, 0.18f, 0.88f),
                    BlockColorType.Pink => new Color(0.98f, 0.45f, 0.75f),
                    BlockColorType.Cyan => new Color(0.22f, 0.85f, 0.95f),
                    BlockColorType.Orange => new Color(0.98f, 0.55f, 0.15f),
                    BlockColorType.Universal => new Color(0.95f, 0.95f, 0.95f),
                    _ => customColor
                };
            }
        }

        /// <summary>
        /// Gets the color for an Exit Gate based on its BlockColorType and active colorblind setting.
        /// </summary>
        public static Color GetGateColor(BlockColorType colorType, bool? forceColorblind = null)
        {
            return GetPieceColor(colorType, Color.white, forceColorblind);
        }
    }
}
