﻿using Bloodcraft.Services;
using Bloodcraft.SystemUtilities.Experience;
using Bloodcraft.SystemUtilities.Familiars;
using Bloodcraft.SystemUtilities.Leveling;
using HarmonyLib;
using ProjectM;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Stunlock.Network;
using Unity.Entities;
using User = ProjectM.Network.User;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class ServerBootstrapSystemPatch
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;

    static readonly PrefabGUID woodenCoffin = new(381160212);
    static readonly PrefabGUID stoneCoffin = new(569692162);

    static readonly Dictionary<string, bool> DefaultBools = new()
    {
        { "ExperienceLogging", false },
        { "QuestLogging", false },
        { "ProfessionLogging", false },
        { "ExpertiseLogging", false },
        { "BloodLogging", false },
        { "FamiliarLogging", false },
        { "SpellLock", false },
        { "ShiftLock", false },
        { "Grouping", false },
        { "Emotes", false },
        { "Binding", false },
        { "Kit", false },
        { "VBloodEmotes", true },
        { "FamiliarVisual", true},
        { "ShinyChoice", false }
    };

    static readonly bool Leveling = Plugin.LevelingSystem.Value;
    static readonly bool Familiars = Plugin.FamiliarSystem.Value;
    static readonly bool Classes = Plugin.SoftSynergies.Value || Plugin.HardSynergies.Value;
    static readonly bool Legacies = Plugin.BloodSystem.Value;
    static readonly bool Expertise = Plugin.ExpertiseSystem.Value;
    static readonly bool RestedXP = Plugin.RestedXP.Value;
    static readonly bool Prestige = Plugin.PrestigeSystem.Value;
    static readonly bool Professions = Plugin.ProfessionSystem.Value;

    static readonly float RestedXPRate = Plugin.RestedXPRate.Value;
    static readonly float RestedXPMaxMultiplier = Plugin.RestedXPMaxMultiplier.Value;
    static readonly float RestedXPTickRate = Plugin.RestedXPTickRate.Value;

    static readonly int StartingLevel = Plugin.StartingLevel.Value;

    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    [HarmonyPostfix]
    static void OnUserConnectedPostfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        int userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
        ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];
        Entity userEntity = serverClient.UserEntity;
        User user = __instance.EntityManager.GetComponentData<User>(userEntity);
        ulong steamId = user.PlatformId;

        Entity character = EntityManager.Exists(user.LocalCharacter._Entity) ? user.LocalCharacter._Entity : Entity.Null;

        if (!Core.DataStructures.PlayerBools.ContainsKey(steamId))
        {
            Core.DataStructures.PlayerBools.Add(steamId, DefaultBools);
            Core.DataStructures.SavePlayerBools();
        }
        else
        {
            Dictionary<string, bool> existingBools = Core.DataStructures.PlayerBools[steamId];

            foreach (string key in DefaultBools.Keys)
            {
                if (!existingBools.ContainsKey(key))
                {
                    existingBools[key] = DefaultBools[key];
                }
            }

            Core.DataStructures.PlayerBools[steamId] = existingBools;
            Core.DataStructures.SavePlayerBools();
        }

        if (Professions)
        {
            if (!Core.DataStructures.PlayerWoodcutting.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerWoodcutting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerWoodcutting();
            }

            if (!Core.DataStructures.PlayerMining.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerMining.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerMining();
            }

            if (!Core.DataStructures.PlayerFishing.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerFishing.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerFishing();
            }

            if (!Core.DataStructures.PlayerBlacksmithing.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerBlacksmithing.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerBlacksmithing();
            }

            if (!Core.DataStructures.PlayerTailoring.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerTailoring.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerTailoring();
            }

            if (!Core.DataStructures.PlayerAlchemy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerAlchemy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerAlchemy();
            }

            if (!Core.DataStructures.PlayerHarvesting.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerHarvesting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerHarvesting();
            }

            if (!Core.DataStructures.PlayerEnchanting.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerEnchanting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerEnchanting();
            }
        }

        if (Expertise)
        {
            if (!Core.DataStructures.PlayerUnarmedExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerUnarmedExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerUnarmedExpertise();
            }

            if (!Core.DataStructures.PlayerSpells.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerSpells.Add(steamId, (0, 0, 0));
                Core.DataStructures.SavePlayerSpells();
            }

            if (!Core.DataStructures.PlayerSwordExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerSwordExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerSwordExpertise();
            }

            if (!Core.DataStructures.PlayerAxeExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerAxeExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerAxeExpertise();
            }

            if (!Core.DataStructures.PlayerMaceExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerMaceExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerMaceExpertise();
            }

            if (!Core.DataStructures.PlayerSpearExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerSpearExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerSpearExpertise();
            }

            if (!Core.DataStructures.PlayerCrossbowExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerCrossbowExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerCrossbowExpertise();
            }

            if (!Core.DataStructures.PlayerGreatSwordExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerGreatSwordExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerGreatSwordExpertise();
            }

            if (!Core.DataStructures.PlayerSlashersExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerSlashersExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerSlashersExpertise();
            }

            if (!Core.DataStructures.PlayerPistolsExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerPistolsExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerPistolsExpertise();
            }

            if (!Core.DataStructures.PlayerReaperExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerReaperExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerReaperExpertise();
            }

            if (!Core.DataStructures.PlayerLongbowExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerLongbowExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerLongbowExpertise();
            }

            if (!Core.DataStructures.PlayerWhipExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerWhipExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerWhipExpertise();
            }

            if (!Core.DataStructures.PlayerFishingPoleExpertise.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerFishingPoleExpertise.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerFishingPoleExpertise();
            }

            if (!Core.DataStructures.PlayerWeaponStats.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerWeaponStats.Add(steamId, []);
                Core.DataStructures.SavePlayerWeaponStats();
            }
        }

        if (Legacies)
        {
            if (!Core.DataStructures.PlayerWorkerLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerWorkerLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerWorkerLegacy();
            }

            if (!Core.DataStructures.PlayerWarriorLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerWarriorLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerWarriorLegacy();
            }

            if (!Core.DataStructures.PlayerScholarLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerScholarLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerScholarLegacy();
            }

            if (!Core.DataStructures.PlayerRogueLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerRogueLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerRogueLegacy();
            }

            if (!Core.DataStructures.PlayerMutantLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerMutantLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerMutantLegacy();
            }

            if (!Core.DataStructures.PlayerVBloodLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerVBloodLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerVBloodLegacy();
            }

            if (!Core.DataStructures.PlayerDraculinLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerDraculinLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerDraculinLegacy();
            }

            if (!Core.DataStructures.PlayerImmortalLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerImmortalLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerImmortalLegacy();
            }

            if (!Core.DataStructures.PlayerCreatureLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerCreatureLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerCreatureLegacy();
            }

            if (!Core.DataStructures.PlayerBruteLegacy.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerBruteLegacy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                Core.DataStructures.SavePlayerBruteLegacy();
            }
        }

        if (Leveling)
        { 
            if (!Core.DataStructures.PlayerExperience.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerExperience.Add(steamId, new KeyValuePair<int, float>(StartingLevel, PlayerLevelingUtilities.ConvertLevelToXp(StartingLevel)));
                Core.DataStructures.SavePlayerExperience();
            }

            if (Prestige && !Core.DataStructures.PlayerPrestiges.ContainsKey(steamId))
            {
                var prestigeDict = new Dictionary<PrestigeUtilities.PrestigeType, int>();
                foreach (var prestigeType in Enum.GetValues<PrestigeUtilities.PrestigeType>())
                {
                    prestigeDict.Add(prestigeType, 0);
                }
                Core.DataStructures.PlayerPrestiges.Add(steamId, prestigeDict);
                Core.DataStructures.SavePlayerPrestiges();
            }
            else
            {
                // Ensure all keys are present in the existing dictionary
                var prestigeDict = Core.DataStructures.PlayerPrestiges[steamId];
                foreach (var prestigeType in Enum.GetValues<PrestigeUtilities.PrestigeType>())
                {
                    if (!prestigeDict.ContainsKey(prestigeType))
                    {
                        prestigeDict.Add(prestigeType, 0);
                    }
                }
            }

            if (character != Entity.Null)
            {
                GearOverride.SetLevel(character);
            }

            if (RestedXP)
            {
                if (!Core.DataStructures.PlayerRestedXP.ContainsKey(steamId))
                {
                    Core.DataStructures.PlayerRestedXP.Add(steamId, new KeyValuePair<DateTime, float>(DateTime.MinValue, 0f));
                    Core.DataStructures.SavePlayerRestedXP();
                }
                else if (character != Entity.Null && Core.DataStructures.PlayerRestedXP.ContainsKey(steamId))
                {
                    float restedMultiplier = 0;

                    if (ServerGameManager.HasBuff(character, woodenCoffin)) restedMultiplier = 0.5f;
                    else if (ServerGameManager.HasBuff(character, stoneCoffin)) restedMultiplier = 1f;

                    var restedData = Core.DataStructures.PlayerRestedXP[steamId];
                    DateTime lastLogout = restedData.Key;

                    TimeSpan timeOffline = DateTime.UtcNow - lastLogout;
                    if (timeOffline.TotalMinutes >= RestedXPTickRate && restedMultiplier != 0)
                    {
                        float currentRestedXP = restedData.Value;
                        float restedCap = Core.DataStructures.PlayerExperience[steamId].Value * RestedXPMaxMultiplier;
                        float earnedPerTick = RestedXPRate * restedCap;

                        float earnedRestedXP = (float)timeOffline.TotalMinutes / RestedXPTickRate * earnedPerTick * restedMultiplier;
                        currentRestedXP = Math.Min(currentRestedXP + earnedRestedXP, restedCap);
                        int roundedXP = (int)(Math.Round(currentRestedXP / 100.0) * 100);

                        Core.DataStructures.PlayerRestedXP[steamId] = new KeyValuePair<DateTime, float>(DateTime.UtcNow, currentRestedXP);
                        Core.DataStructures.SavePlayerRestedXP();

                        string message = $"+<color=#FFD700>{roundedXP}</color> <color=green>rested</color> <color=#FFC0CB>experience</color> earned from being logged out in your coffin!";
                        LocalizationService.HandleServerReply(EntityManager, user, message);
                    }
                }
            }
        }

        if (Familiars)
        {
            if (!Core.DataStructures.FamiliarActives.ContainsKey(steamId))
            {
                Core.DataStructures.FamiliarActives.Add(steamId, (Entity.Null, 0));
                Core.DataStructures.SavePlayerFamiliarActives();
            }

            if (!Core.DataStructures.FamiliarSet.ContainsKey(steamId))
            {
                Core.DataStructures.FamiliarSet.Add(steamId, "");
                Core.DataStructures.SavePlayerFamiliarSets();
            }

            Core.FamiliarExperienceManager.SaveFamiliarExperience(steamId, Core.FamiliarExperienceManager.LoadFamiliarExperience(steamId));
            Core.FamiliarUnlocksManager.SaveUnlockedFamiliars(steamId, Core.FamiliarUnlocksManager.LoadUnlockedFamiliars(steamId));

            if (character != Entity.Null && character.Has<FollowerBuffer>())
            {
                var buffer = character.ReadBuffer<FollowerBuffer>();
                foreach (var follower in buffer)
                {
                    if (EntityManager.Exists(follower.Entity._Entity))
                    {
                        DestroyUtility.Destroy(EntityManager, follower.Entity._Entity);
                    }
                }
                FamiliarSummonUtilities.FamiliarUtilities.ClearFamiliarActives(steamId);
            }
        }

        if (Classes)
        {
            if (!Core.DataStructures.PlayerClasses.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerClasses.Add(steamId, []);
                Core.DataStructures.SavePlayerClasses();
            }

            if (!Core.DataStructures.PlayerSpells.ContainsKey(steamId))
            {
                Core.DataStructures.PlayerSpells.Add(steamId, (0, 0, 0));
                Core.DataStructures.SavePlayerSpells();
            }
        }
    }

    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
    [HarmonyPrefix]
    static void OnUserDisconnectedPrefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        int userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
        ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];
        Entity userEntity = serverClient.UserEntity;
        User user = __instance.EntityManager.GetComponentData<User>(userEntity);
        Entity character = user.LocalCharacter._Entity;
        ulong steamId = user.PlatformId;

        if (Familiars && EntityManager.Exists(character) && character.Has<FollowerBuffer>())
        {
            var buffer = character.ReadBuffer<FollowerBuffer>();
            foreach (var follower in buffer)
            {
                if (EntityManager.Exists(follower.Entity._Entity))
                {
                    DestroyUtility.Destroy(EntityManager, follower.Entity._Entity);
                }
            }
            FamiliarSummonUtilities.FamiliarUtilities.ClearFamiliarActives(steamId);
        }

        if (Leveling)
        {
            if (RestedXP && Core.DataStructures.PlayerRestedXP.TryGetValue(steamId, out var restedData))
            {
                restedData = new KeyValuePair<DateTime, float>(DateTime.UtcNow, restedData.Value);
                Core.DataStructures.PlayerRestedXP[steamId] = restedData;
                Core.DataStructures.SavePlayerRestedXP();
            }

            if (character.Exists() && character.Has<ServantInteractPointLocalTransform>())
            {
                character.Remove<ServantInteractPointLocalTransform>();
            }
        }
    }
}