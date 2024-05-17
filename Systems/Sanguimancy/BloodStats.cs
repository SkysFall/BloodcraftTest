﻿namespace Cobalt.Systems.Sanguimancy
{
    public class BloodStats
    {
        public class PlayerBloodUtilities
        {
            public static bool ChooseStat(ulong steamId, string statType)
            {
                if (!Core.DataStructures.PlayerBloodChoices.ContainsKey(steamId))
                    Core.DataStructures.PlayerBloodChoices[steamId] = [];

                if (Core.DataStructures.PlayerBloodChoices[steamId].Count >= 2)
                {
                    return false; // Only allow 3 stats to be chosen
                }

                Core.DataStructures.PlayerBloodChoices[steamId].Add(statType);
                Core.DataStructures.SavePlayerBloodChoices();
                return true;
            }

            public static void ResetChosenStats(ulong steamId)
            {
                if (Core.DataStructures.PlayerBloodChoices.TryGetValue(steamId, out var bloodChoices))
                {
                    bloodChoices.Clear();
                    Core.DataStructures.SavePlayerBloodChoices();
                }
            }
        }

        public class BloodStatManager
        {
            public enum BloodStatType
            {
                SunResistance,
                FireResistance,
                HolyResistance,
                SilverResistance,
                PassiveHealthRegen
            }

            public static readonly Dictionary<int, BloodStatType> BloodStatMap = new()
            {
                { 0, BloodStatType.SunResistance },
                { 1, BloodStatType.FireResistance },
                { 2, BloodStatType.HolyResistance },
                { 3, BloodStatType.SilverResistance },
                { 4, BloodStatType.PassiveHealthRegen }
            };

            private static readonly Dictionary<BloodStatType, float> baseCaps = new()
            {
                {BloodStatType.SunResistance, 75f},
                {BloodStatType.FireResistance, 75f},
                {BloodStatType.HolyResistance, 75f},
                {BloodStatType.SilverResistance, 75f},
                {BloodStatType.PassiveHealthRegen, 0.19f}
            };

            public static Dictionary<BloodStatType, float> BaseCaps
            {
                get => baseCaps;
            }
        }
    }
}