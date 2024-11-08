﻿using Bloodcraft.Services;
using Bloodcraft.Systems.Leveling;
using Bloodcraft.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class UpdateBuffsBufferDestroyPatch
{
    static EntityManager EntityManager => Core.EntityManager;
    static readonly bool Classes = ConfigService.SoftSynergies || ConfigService.HardSynergies;

    static readonly PrefabGUID CombatBuff = new(581443919);
    static readonly PrefabGUID TauntEmoteBuff = new(-508293388);
    static readonly PrefabGUID PhasingBuff = new(-79611032);
    static readonly PrefabGUID ExoFormBuff = new(-31099041);

    public static readonly List<PrefabGUID> PrestigeBuffs = [];
    public static readonly Dictionary<LevelingSystem.PlayerClass, List<PrefabGUID>> ClassBuffs = [];

    [HarmonyPatch(typeof(UpdateBuffsBuffer_Destroy), nameof(UpdateBuffsBuffer_Destroy.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostix(UpdateBuffsBuffer_Destroy __instance)
    {
        if (!Core.hasInitialized) return;
        else if (!(ConfigService.FamiliarSystem || ConfigService.PrestigeSystem || Classes)) return;

        NativeArray<Entity> entities = __instance.__query_401358720_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (!entity.TryGetComponent(out PrefabGUID prefabGUID)) continue;           

                if (ConfigService.FamiliarSystem && prefabGUID.Equals(CombatBuff))
                {
                    if (entity.GetBuffTarget().TryGetPlayer(out Entity character))
                    {
                        Entity familiar = FamiliarUtilities.FindPlayerFamiliar(character);
                        if (familiar.Exists())
                        {
                            character.With((ref CombatMusicListener_Shared shared) =>
                            {
                                shared.UnitPrefabGuid = PrefabGUID.Empty;
                            });
                        }
                    }
                }
                
                if (ConfigService.PrestigeSystem && entity.GetBuffTarget().TryGetPlayer(out Entity player)) // check if need to reapply prestige buff
                {
                    ulong steamId = player.GetSteamId();

                    if (PrestigeBuffs.Contains(prefabGUID)) // check if the buff is for prestige and reapply if so
                    {
                        if (steamId.TryGetPlayerPrestiges(out var prestigeData) && prestigeData.TryGetValue(PrestigeType.Experience, out var prestigeLevel))
                        {
                            //Core.Log.LogInfo($"UpdateBuffsBuffer_Destroy | {steamId} | {prestigeLevel} | {PrestigeBuffs.IndexOf(prefabGUID)} | {prefabGUID.LookupName()}");
                            
                            if (prestigeLevel > PrestigeBuffs.IndexOf(prefabGUID)) BuffUtilities.ApplyPermanentBuff(player, prefabGUID); // at 0 will not be greater than index of 0 so won't apply buffs, if greater than 0 will apply if allowed based on order of prefabs
                        }
                    }
                    else if (ConfigService.ExoPrestiging && prefabGUID.Equals(TauntEmoteBuff) && PlayerUtilities.GetPlayerBool(steamId, "ExoForm"))
                    {
                        if (EmoteSystemPatch.ExitingForm.Contains(steamId))
                        {
                            EmoteSystemPatch.ExitingForm.Remove(steamId);
                            continue;
                        }

                        HandleExoForm(player.GetUser(), player, steamId); // could maybe try SpawnPrefabOnGameplayEvent or something like that instead of slingshotting this around, will ponder
                    }
                }
                
                if (Classes && entity.GetBuffTarget().TryGetPlayer(out player))
                {
                    ulong steamId = player.GetSteamId();

                    if (ClassUtilities.HasClass(steamId))
                    {
                        LevelingSystem.PlayerClass playerClass = ClassUtilities.GetPlayerClass(steamId);

                        if (ClassBuffs.TryGetValue(playerClass, out List<PrefabGUID> classBuffs) && classBuffs.Contains(prefabGUID)) BuffUtilities.ApplyPermanentBuff(player, prefabGUID);
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
    static void HandleExoForm(User user, Entity character, ulong steamId)
    {
        // check for cooldown here and other such qualifiers before proceeding, also charge at 15 seconds of form time a day for level 1 up to maxDuration seconds of form time at max exo
        BuffUtilities.UpdateExoFormChargeStored(steamId);

        if (steamId.TryGetPlayerExoFormData(out var exoFormData) && exoFormData.Value < BuffUtilities.BaseDuration)
        {
            int exoLevel = steamId.TryGetPlayerPrestiges(out var prestiges) && prestiges.TryGetValue(PrestigeType.Exo, out int exoPrestiges) ? exoPrestiges : 0;
            float totalDuration = BuffUtilities.CalculateFormDuration(exoLevel);

            float chargeNeeded = BuffUtilities.BaseDuration - exoFormData.Value;
            float ratioToTotal = chargeNeeded / totalDuration;
            float secondsRequired = 86400f * ratioToTotal;

            // Convert seconds to hours, minutes, and seconds
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsRequired);
            string timeRemaining;

            // Format based on the amount of time left
            if (timeSpan.TotalHours >= 1)
            {
                // Display hours, minutes, and seconds if more than an hour remains
                timeRemaining = $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                // Display only minutes and seconds if less than an hour remains
                timeRemaining = $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }

            LocalizationService.HandleServerReply(EntityManager, user, $"Not enough energy to maintain form... (<color=yellow>{timeRemaining}</color> remaining)");
            return;
        }

        if (!character.HasBuff(ExoFormBuff))
        {
            BuffUtilities.TryApplyBuff(character, ExoFormBuff);
            BuffUtilities.TryApplyBuff(character, PhasingBuff);
        }
    }
}
