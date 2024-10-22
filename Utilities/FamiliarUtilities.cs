﻿using Bloodcraft.Services;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using static Bloodcraft.Patches.LinkMinionToOwnerOnSpawnSystemPatch;
using static Bloodcraft.Services.DataService.FamiliarPersistence;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarUnlocksManager;
using static Bloodcraft.Systems.Familiars.FamiliarSummonSystem;

namespace Bloodcraft.Utilities;
internal static class FamiliarUtilities
{
    static EntityManager EntityManager => Core.EntityManager;
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;

    public static readonly Dictionary<Entity, Entity> AutoCallMap = [];

    static readonly PrefabGUID SwitchTargetBuff = new(1489461671);
    public static void ClearFamiliarActives(ulong steamId)
    {
        if (steamId.TryGetFamiliarActives(out var actives))
        {
            actives = (Entity.Null, 0);
            steamId.SetFamiliarActives(actives);
        }
    }
    public static Entity FindPlayerFamiliar(Entity character)
    {
        if (!character.Has<FollowerBuffer>()) return Entity.Null;

        var followers = character.ReadBuffer<FollowerBuffer>();
        ulong steamId = character.GetSteamId();

        if (!followers.IsEmpty) // if buffer not empty check here first, only need the rest if familiar is disabled via call/dismiss since that removes from followerBuffer to enable waygate use and such
        {
            foreach (FollowerBuffer follower in followers)
            {
                Entity familiar = follower.Entity._Entity;
                if (familiar.Has<BlockFeedBuff>() && familiar.Exists()) return familiar;
            }
        }
        else if (HasDismissed(steamId, out Entity familiar)) return familiar;

        return Entity.Null;
    }
    public static void HandleFamiliarMinions(Entity familiar) //  need to see if game will handle familiar minions as player minions without extra effort, that would be neat
    {
        if (FamiliarMinions.ContainsKey(familiar))
        {
            foreach (Entity minion in FamiliarMinions[familiar])
            {
                DestroyUtility.Destroy(EntityManager, minion);
            }

            FamiliarMinions.Remove(familiar);
        }
    }
    public static bool HasDismissed(ulong steamId, out Entity familiar)
    {
        familiar = Entity.Null;

        if (steamId.TryGetFamiliarActives(out var actives) && actives.Familiar.Exists())
        {
            familiar = actives.Familiar;
            return true;
        }

        return false;
    }
    public static void ParseAddedFamiliar(ChatCommandContext ctx, ulong steamId, string unit, string activeSet = "")
    {
        UnlockedFamiliarData data = LoadUnlockedFamiliars(steamId);

        if (int.TryParse(unit, out int prefabHash) && PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(new(prefabHash), out Entity prefabEntity))
        {
            // Add to set if valid
            if (!prefabEntity.Read<PrefabGUID>().LookupName().StartsWith("CHAR"))
            {
                LocalizationService.HandleReply(ctx, "Invalid unit prefab (match found but does not start with CHAR/char).");
                return;
            }

            data.UnlockedFamiliars[activeSet].Add(prefabHash);
            SaveUnlockedFamiliars(steamId, data);

            LocalizationService.HandleReply(ctx, $"<color=green>{new PrefabGUID(prefabHash).GetPrefabName()}</color> added to <color=white>{activeSet}</color>.");
        }
        else if (unit.ToLower().StartsWith("char")) // search for full and/or partial name match
        {
            // Try using TryGetValue for an exact match (case-sensitive)
            if (!PrefabCollectionSystem.NameToPrefabGuidDictionary.TryGetValue(unit, out PrefabGUID match))
            {
                // If exact match is not found, do a case-insensitive search for full or partial matches
                foreach (var kvp in PrefabCollectionSystem.NameToPrefabGuidDictionary)
                {
                    // Check for a case-insensitive full match
                    if (kvp.Key.Equals(unit, StringComparison.OrdinalIgnoreCase))
                    {
                        match = kvp.Value; // Full match found
                        break;
                    }
                }
            }

            // verify prefab is a char unit
            if (!match.IsEmpty() && PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(match, out prefabEntity))
            {
                if (!prefabEntity.Read<PrefabGUID>().LookupName().StartsWith("CHAR"))
                {
                    LocalizationService.HandleReply(ctx, "Invalid unit name (match found but does not start with CHAR/char).");
                    return;
                }

                data.UnlockedFamiliars[activeSet].Add(match.GuidHash);
                SaveUnlockedFamiliars(steamId, data);

                LocalizationService.HandleReply(ctx, $"<color=green>{match.GetPrefabName()}</color> (<color=yellow>{match.GuidHash}</color>) added to <color=white>{activeSet}</color>.");
            }
            else
            {
                LocalizationService.HandleReply(ctx, "Invalid unit name (no full or partial matches).");
            }
        }
        else
        {
            LocalizationService.HandleReply(ctx, "Invalid prefab (not an integer) or name (does not start with CHAR/char).");
        }
    }
    public static void ReturnFamiliar(Entity player, Entity familiar)
    {
        Follower following = familiar.Read<Follower>();
        following.ModeModifiable._Value = 1;
        familiar.Write(following);

        float3 playerPos = player.Read<Translation>().Value;
        float distance = UnityEngine.Vector3.Distance(familiar.Read<Translation>().Value, playerPos);

        if (distance > 25f)
        {
            familiar.Write(new LastTranslation { Value = playerPos });
            familiar.Write(new Translation { Value = playerPos });

            Core.Log.LogInfo($"Returning familiar, applying target switch buff...");
            BuffUtilities.TryApplyBuff(familiar, SwitchTargetBuff);
        }
    }
    public static void ToggleShinies(ChatCommandContext ctx, ulong steamId)
    {
        PlayerUtilities.TogglePlayerBool(steamId, "FamiliarVisual");
        LocalizationService.HandleReply(ctx, PlayerUtilities.GetPlayerBool(steamId, "FamiliarVisual") ? "Shiny familiars <color=green>enabled</color>." : "Shiny familiars <color=red>disabled</color>.");
    }
    public static void ToggleVBloodEmotes(ChatCommandContext ctx, ulong steamId)
    {
        PlayerUtilities.TogglePlayerBool(steamId, "VBloodEmotes");
        LocalizationService.HandleReply(ctx, PlayerUtilities.GetPlayerBool(steamId, "VBloodEmotes") ? "VBlood Emotes <color=green>enabled</color>." : "VBlood Emotes <color=red>disabled</color>.");
    }
    public static bool TryParseFamiliarStat(string statType, out FamiliarStatType parsedStatType)
    { 
        parsedStatType = default;

        if (Enum.TryParse(statType, true, out parsedStatType))
        {
            return true;
        }
        else
        {
            parsedStatType = Enum.GetValues(typeof(FamiliarStatType))
                .Cast<FamiliarStatType>()
                .FirstOrDefault(pt => pt.ToString().Contains(statType, StringComparison.OrdinalIgnoreCase));

            if (!parsedStatType.Equals(default(FamiliarStatType)))
            {
                return true;
            }
        }

        return false;
    }
    public static void ClearBuffers(Entity playerCharacter, ulong steamId)
    {
        if (playerCharacter.Has<FollowerBuffer>())
        {
            var buffer = playerCharacter.ReadBuffer<FollowerBuffer>();

            foreach (FollowerBuffer follower in buffer)
            {
                Entity followerEntity = follower.Entity.GetEntityOnServer();

                if (followerEntity.Exists())
                {
                    DestroyUtility.Destroy(EntityManager, followerEntity);
                }
            }

            buffer.Clear();
        }

        if (playerCharacter.Has<MinionBuffer>())
        {
            var buffer = playerCharacter.ReadBuffer<MinionBuffer>();

            foreach (MinionBuffer minion in buffer)
            {
                if (minion.Entity.Exists())
                {
                    DestroyUtility.Destroy(EntityManager, minion.Entity);
                }
            }

            buffer.Clear();
        }

        ClearFamiliarActives(steamId);
    }
    public static void AutoDismiss(Entity player, Entity familiar)
    {
        User user = player.GetUser();
        ulong steamId = user.PlatformId;

        if (steamId.TryGetFamiliarActives(out var data)) 
        {
            if (FamiliarMinions.ContainsKey(familiar)) HandleFamiliarMinions(familiar);

            familiar.Add<Disabled>();

            Follower follower = familiar.Read<Follower>();
            follower.Followed._Value = Entity.Null;
            familiar.Write(follower);

            AggroConsumer aggroConsumer = familiar.Read<AggroConsumer>();
            aggroConsumer.Active._Value = false;
            aggroConsumer.AggroTarget._Entity = Entity.Null;
            aggroConsumer.AlertTarget._Entity = Entity.Null;
            familiar.Write(aggroConsumer);

            var buffer = player.ReadBuffer<FollowerBuffer>();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Entity._Entity.Equals(familiar))
                {
                    buffer.RemoveAt(i);
                    break;
                }
            }

            data = (familiar, data.FamKey); // entity stored when dismissed
            steamId.SetFamiliarActives(data);

            AutoCallMap.TryAdd(player, familiar);
            LocalizationService.HandleServerReply(EntityManager, user, "Familiar <color=red>disabled</color>.");
        }
    }
    public static void AutoCall(Entity player, Entity familiar)
    {
        User user = player.GetUser();
        ulong steamId = user.PlatformId;

        if (steamId.TryGetFamiliarActives(out var data))
        {
            float3 position = player.Read<Translation>().Value;
            familiar.Remove<Disabled>();

            familiar.Write(new Translation { Value = position });
            familiar.Write(new LastTranslation { Value = position });

            Follower follower = familiar.Read<Follower>();
            follower.Followed._Value = player;
            familiar.Write(follower);

            if (ConfigService.FamiliarCombat)
            {
                AggroConsumer aggroConsumer = familiar.Read<AggroConsumer>();
                aggroConsumer.Active._Value = true;
                familiar.Write(aggroConsumer);
            }

            data = (Entity.Null, data.FamKey);
            steamId.SetFamiliarActives(data);

            LocalizationService.HandleServerReply(EntityManager, user, "Familiar <color=green>enabled</color>.");
        }   
    }
}
