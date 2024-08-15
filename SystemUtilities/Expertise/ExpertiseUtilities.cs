﻿using Bloodcraft.Patches;
using Bloodcraft.SystemUtilities.Leveling;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using static Bloodcraft.Services.LocalizationService;

namespace Bloodcraft.SystemUtilities.Expertise;
public static class ExpertiseUtilities
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly float UnitMultiplier = Plugin.UnitExpertiseMultiplier.Value; // Expertise points multiplier from normal units
    static readonly int MaxExpertiseLevel = Plugin.MaxExpertiseLevel.Value; // maximum level
    static readonly float VBloodMultiplier = Plugin.VBloodExpertiseMultiplier.Value; // Expertise points multiplier from VBlood units
    const float ExpertiseConstant = 0.1f; // constant for calculating level from xp
    const int ExpertisePower = 2; // power for calculating level from xp
    static readonly float PrestigeRatesMultiplier = Plugin.PrestigeRatesMultiplier.Value; // Prestige multiplier
    static readonly float PrestigeRatesReducer = Plugin.PrestigeRatesReducer.Value; // Prestige reducer
    public enum WeaponType
    {
        Sword,
        Axe,
        Mace,
        Spear,
        Crossbow,
        GreatSword,
        Slashers,
        Pistols,
        Reaper,
        Longbow,
        Whip,
        Unarmed,
        FishingPole
    }

    public static readonly Dictionary<WeaponType, PrestigeUtilities.PrestigeType> WeaponPrestigeMap = new()
    {
        { WeaponType.Sword, PrestigeUtilities.PrestigeType.SwordExpertise },
        { WeaponType.Axe, PrestigeUtilities.PrestigeType.AxeExpertise },
        { WeaponType.Mace, PrestigeUtilities.PrestigeType.MaceExpertise },
        { WeaponType.Spear, PrestigeUtilities.PrestigeType.SpearExpertise },
        { WeaponType.Crossbow, PrestigeUtilities.PrestigeType.CrossbowExpertise },
        { WeaponType.GreatSword, PrestigeUtilities.PrestigeType.GreatSwordExpertise },
        { WeaponType.Slashers, PrestigeUtilities.PrestigeType.SlashersExpertise },
        { WeaponType.Pistols, PrestigeUtilities.PrestigeType.PistolsExpertise },
        { WeaponType.Reaper, PrestigeUtilities.PrestigeType.ReaperExpertise },
        { WeaponType.Longbow, PrestigeUtilities.PrestigeType.LongbowExpertise },
        { WeaponType.Whip, PrestigeUtilities.PrestigeType.WhipExpertise },
        { WeaponType.Unarmed, PrestigeUtilities.PrestigeType.UnarmedExpertise }, 
        { WeaponType.FishingPole, PrestigeUtilities.PrestigeType.FishingPoleExpertise }
    };
    public static void UpdateExpertise(Entity Killer, Entity Victim)
    {
        if (Killer == Victim || Victim.Has<Minion>()) return;

        Entity userEntity = Killer.Read<PlayerCharacter>().UserEntity;
        User user = userEntity.Read<User>();
        ulong steamID = user.PlatformId;
        ExpertiseUtilities.WeaponType weaponType = ModifyUnitStatBuffUtils.GetCurrentWeaponType(Killer);

        if (Victim.Has<UnitStats>())
        {
            var VictimStats = Victim.Read<UnitStats>();
            float ExpertiseValue = CalculateExpertiseValue(VictimStats, Victim.Has<VBloodConsumeSource>());
            float changeFactor = 1f;

            if (Core.DataStructures.PlayerPrestiges.TryGetValue(steamID, out var prestiges))
            {
                if (prestiges.TryGetValue(WeaponPrestigeMap[weaponType], out var expertisePrestige) && expertisePrestige > 0)
                {
                    changeFactor -= (PrestigeRatesReducer * expertisePrestige);
                }

                if (prestiges.TryGetValue(PrestigeUtilities.PrestigeType.Experience, out var xpPrestige) && xpPrestige > 0)
                {
                    changeFactor += (PrestigeRatesMultiplier * xpPrestige);
                }
            }

            ExpertiseValue *= changeFactor;
            //IPrestigeHandler prestigeHandler = PrestigeHandlerFactory.GetPrestigeHandler(WeaponPrestigeMap[weaponType]);

            IExpertiseHandler handler = ExpertiseHandlerFactory.GetExpertiseHandler(weaponType);
            if (handler != null)
            {
                // Check if the player leveled up
                var xpData = handler.GetExpertiseData(steamID);

                if (xpData.Key >= MaxExpertiseLevel) return;

                float newExperience = xpData.Value + ExpertiseValue;
                int newLevel = ConvertXpToLevel(newExperience);
                bool leveledUp = false;

                if (newLevel > xpData.Key)
                {
                    leveledUp = true;
                    if (newLevel > MaxExpertiseLevel)
                    {
                        newLevel = MaxExpertiseLevel;
                        newExperience = ConvertLevelToXp(MaxExpertiseLevel);
                    }
                }

                var updatedXPData = new KeyValuePair<int, float>(newLevel, newExperience);
                handler.UpdateExpertiseData(steamID, updatedXPData);
                handler.SaveChanges();
                NotifyPlayer(user, weaponType, ExpertiseValue, leveledUp, newLevel, handler);
            }
        }
    }
    static float CalculateExpertiseValue(UnitStats VictimStats, bool isVBlood)
    {
        float ExpertiseValue = VictimStats.SpellPower + VictimStats.PhysicalPower;
        if (isVBlood) return ExpertiseValue * VBloodMultiplier;
        return ExpertiseValue * UnitMultiplier;
    }
    public static void NotifyPlayer(User user, ExpertiseUtilities.WeaponType weaponType, float gainedXP, bool leveledUp, int newLevel, IExpertiseHandler handler)
    {
        ulong steamID = user.PlatformId;
        gainedXP = (int)gainedXP;
        int levelProgress = GetLevelProgress(steamID, handler);

        if (leveledUp)
        {
            if (newLevel <= MaxExpertiseLevel) HandleServerReply(EntityManager, user, $"<color=#c0c0c0>{weaponType}</color> improved to [<color=white>{newLevel}</color>]");
        }

        if (Core.DataStructures.PlayerBools.TryGetValue(steamID, out var bools) && bools["ExpertiseLogging"])
        {
            HandleServerReply(EntityManager, user, $"+<color=yellow>{gainedXP}</color> <color=#c0c0c0>{weaponType.ToString().ToLower()}</color> <color=#FFC0CB>expertise</color> (<color=white>{levelProgress}%</color>)");
        }
    }
    public static int GetLevelProgress(ulong steamID, IExpertiseHandler handler)
    {
        float currentXP = GetXp(steamID, handler);
        int currentLevelXP = ConvertLevelToXp(GetLevel(steamID, handler));
        int nextLevelXP = ConvertLevelToXp(GetLevel(steamID, handler) + 1);

        double neededXP = nextLevelXP - currentLevelXP;
        double earnedXP = nextLevelXP - currentXP;
        return 100 - (int)Math.Ceiling(earnedXP / neededXP * 100);
    }
    public static int ConvertXpToLevel(float xp)
    {
        // Assuming a basic square root scaling for experience to level conversion
        return (int)(ExpertiseConstant * Math.Sqrt(xp));
    }
    public static int ConvertLevelToXp(int level)
    {
        // Reversing the formula used in ConvertXpToLevel for consistency
        return (int)Math.Pow(level / ExpertiseConstant, ExpertisePower);
    }
    static float GetXp(ulong steamID, IExpertiseHandler handler)
    {
        var xpData = handler.GetExpertiseData(steamID);
        return xpData.Value;
    }
    static int GetLevel(ulong steamID, IExpertiseHandler handler)
    {
        return ConvertXpToLevel(GetXp(steamID, handler));
    }
    public static WeaponType GetWeaponTypeFromSlotEntity(Entity weaponEntity)
    {
        if (weaponEntity == Entity.Null) return WeaponType.Unarmed;

        string weaponCheck = weaponEntity.Read<PrefabGUID>().LookupName();

        return Enum.GetValues(typeof(WeaponType))
                   .Cast<WeaponType>()
                   .FirstOrDefault(type =>
                    weaponCheck.Contains(type.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    !(type == WeaponType.Sword && weaponCheck.Contains("GreatSword", StringComparison.OrdinalIgnoreCase))
                   );
    }
}