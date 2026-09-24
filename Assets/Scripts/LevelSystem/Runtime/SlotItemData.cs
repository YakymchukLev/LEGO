using System;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    public enum SlotRewardType
    {
        Coins,
        Gems,
        Hearts,
        HammerBooster,
        FreezeBooster,
        HintBooster,
        JackpotSpecial
    }

    /// <summary>
    /// Configuration data for an item that can appear on the slot machine reels.
    /// Easily configurable in the Unity Inspector by dropping in sprites, setting rewards and drop weights.
    /// </summary>
    [Serializable]
    public class SlotItemData
    {
        [Tooltip("Unique identifier for this item (e.g., 'Coins', 'Hammer', 'Diamond')")]
        public string id = "Coins";

        [Tooltip("Human-readable name displayed when this item wins")]
        public string displayName = "100 Coins";

        [Tooltip("The sprite icon rendered on the reel cells. Drop your custom sprites here!")]
        public Sprite icon;

        [Tooltip("Type of reward granted when this item forms a winning payline")]
        public SlotRewardType rewardType = SlotRewardType.Coins;

        [Tooltip("Quantity of the reward (e.g., 100 coins, 20 gems, 1 booster)")]
        public int rewardAmount = 100;

        [Tooltip("Rarity / selection weight in RNG (higher = more frequent, lower = rarer)")]
        [Range(1, 100)]
        public int weight = 20;

        [Tooltip("Accent glow color used for particles and celebratory banners on win")]
        public Color themeColor = new Color(1f, 0.84f, 0f, 1f); // Vibrant Gold
    }
}
