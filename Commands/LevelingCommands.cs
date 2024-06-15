using Bloodcraft.Patches;
using Bloodcraft.Systems.Experience;
using Bloodcraft.Systems.Leveling;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;
using static Bloodcraft.Systems.Experience.LevelingSystem;
using static Bloodcraft.Services.PlayerService;
using ProjectM.Scripting;
using ProjectM.Gameplay.Scripting;
using ProjectM.Shared;
using static Il2CppSystem.Xml.Schema.XsdDuration;

namespace Bloodcraft.Commands
{
    public static class LevelingCommands
    {
        [Command(name: "quickStart", shortHand: "start", adminOnly: false, usage: ".start", description: "Completes GettingReadyForTheHunt if not already completed.")]
        public static void QuickStartCommand(ChatCommandContext ctx)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            EntityCommandBuffer entityCommandBuffer = Core.EntityCommandBufferSystem.CreateCommandBuffer();
            PrefabGUID achievementPrefabGUID = new(560247139); // Journal_GettingReadyForTheHunt
            Entity userEntity = ctx.Event.SenderUserEntity;
            Entity characterEntity = ctx.Event.SenderCharacterEntity;
            Entity achievementOwnerEntity = userEntity.Read<AchievementOwner>().Entity._Entity;
            Core.ClaimAchievementSystem.CompleteAchievement(entityCommandBuffer, achievementPrefabGUID, userEntity, characterEntity, achievementOwnerEntity, false, true);
            ctx.Reply("You are now prepared for the hunt.");
        }

        [Command(name: "logLevelingProgress", shortHand: "log l", adminOnly: false, usage: ".log l", description: "Toggles leveling progress logging.")]
        public static void LogExperienceCommand(ChatCommandContext ctx)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            var SteamID = ctx.Event.User.PlatformId;

            if (Core.DataStructures.PlayerBools.TryGetValue(SteamID, out var bools))
            {
                bools["ExperienceLogging"] = !bools["ExperienceLogging"];
            }
            Core.DataStructures.SavePlayerBools();
            ctx.Reply($"Leveling experience logging {(bools["ExperienceLogging"] ? "<color=green>enabled</color>" : "<color=red>disabled</color>")}.");
        }

        [Command(name: "getLevelingProgress", shortHand: "get l", adminOnly: false, usage: ".get l", description: "Display current leveling progress.")]
        public static void GetLevelCommand(ChatCommandContext ctx)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            ulong steamId = ctx.Event.User.PlatformId;
            if (Core.DataStructures.PlayerExperience.TryGetValue(steamId, out var Leveling))
            {
                int level = Leveling.Key;
                int progress = (int)(Leveling.Value - LevelingSystem.ConvertLevelToXp(level));
                int percent = LevelingSystem.GetLevelProgress(steamId);
                ctx.Reply($"You're level [<color=white>{level}</color>] and have <color=yellow>{progress}</color> <color=#FFC0CB>experience</color> (<color=white>{percent}%</color>)");
            }
            else
            {
                ctx.Reply("No leveling experience yet.");
            }
        }

        [Command(name: "setLevel", shortHand: "sl", adminOnly: true, usage: ".sl [Player] [Level]", description: "Sets player level.")]
        public static void SetLevelCommand(ChatCommandContext ctx, string name, int level)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }

            Entity foundUserEntity = GetUserByName(name, true);
            if (foundUserEntity.Equals(Entity.Null))
            {
                ctx.Reply("Player not found...");
                return;
            }
            User foundUser = foundUserEntity.Read<User>();

            if (level < 0 || level > Plugin.MaxPlayerLevel.Value)
            {
                ctx.Reply($"Level must be between 0 and {Plugin.MaxPlayerLevel.Value}");
                return;
            }
            ulong steamId = foundUser.PlatformId;
            if (Core.DataStructures.PlayerExperience.TryGetValue(steamId, out var _))
            {
                var xpData = new KeyValuePair<int, float>(level, LevelingSystem.ConvertLevelToXp(level));
                Core.DataStructures.PlayerExperience[steamId] = xpData;
                Core.DataStructures.SavePlayerExperience();
                GearOverride.SetLevel(foundUser.LocalCharacter._Entity);
                ctx.Reply($"Level set to <color=white>{level}</color> for <color=green>{foundUser.CharacterName}</color>");
            }
            else
            {
                ctx.Reply("No experience data found.");
            }
        }

        [Command(name: "chooseClass", shortHand: "cc", adminOnly: false, usage: ".cc [Class]", description: "Choose class.")]
        public static void ClassChoiceCommand(ChatCommandContext ctx, string className)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }

            if (!TryParseClassName(className, out var parsedClassType))
            {
                ctx.Reply("Invalid class, use .classes to see options.");
                return;
            }

            ulong steamId = ctx.Event.User.PlatformId;

            if (Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
            {
                if (classes.Keys.Count > 0)
                {
                    ctx.Reply("You have already chosen a class.");
                    return;
                }
                UpdateClassData(ctx.Event.SenderCharacterEntity, parsedClassType, classes, steamId);
                ctx.Reply($"You have chosen <color=white>{parsedClassType}</color>");
            }
        }
        [Command(name: "chooseClassSpell", shortHand: "cs", adminOnly: false, usage: ".cs [#]", description: "Sets shift spell for class if prestige level is high enough.")]
        public static void ChooseClassSpell(ChatCommandContext ctx, int choice)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }
            if (!Plugin.ShiftSlots.Value)
            {
                ctx.Reply("Shift slots are not enabled for class spells.");
                return;
            }
            ulong steamId = ctx.Event.User.PlatformId;

            if (Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
            {
                if (classes.Keys.Count == 0)
                {
                    ctx.Reply("You haven't chosen a class yet.");
                    return;
                }
                PlayerClasses playerClass = classes.Keys.FirstOrDefault();
                if (Core.DataStructures.PlayerPrestiges.TryGetValue(steamId, out var prestigeData) && prestigeData.TryGetValue(PrestigeSystem.PrestigeType.Experience, out var prestigeLevel))
                {
                    if (prestigeLevel < Core.ParseConfigString(Plugin.PrestigeLevelsToUnlockClassSpells.Value)[choice - 1])
                    {
                        ctx.Reply("You do not have the required prestige level for that spell.");
                        return;
                    }

                    List<int> spells = Core.ParseConfigString(LevelingSystem.ClassSpellsMap[playerClass]);

                    if (spells.Count == 0)
                    {
                        ctx.Reply("No spells found for class.");
                        return;
                    }

                    if (choice < 1 || choice > spells.Count)
                    {
                        ctx.Reply($"Invalid spell choice. (Use 1-{spells.Count})");
                        return;
                    }

                    if (Core.DataStructures.PlayerSpells.TryGetValue(steamId, out var spellsData))
                    {
                        spellsData.ClassSpell = spells[choice - 1];
                        Core.DataStructures.PlayerSpells[steamId] = spellsData;
                        Core.DataStructures.SavePlayerSpells();

                        ctx.Reply($"You have chosen spell <color=#CBC3E3>{new PrefabGUID(spells[choice - 1]).LookupName()}</color> from <color=white>{playerClass}</color>, it will be available on weapons/unarmed if .shift is enabled.");

                    }
                }
                else
                {
                    ctx.Reply("You haven't prestiged in leveling yet.");
                }
            }
            else
            {
                ctx.Reply("You haven't chosen a class yet.");
                return;
            }
            
        }

        [Command(name: "changeClass", shortHand: "change", adminOnly: false, usage: ".change [Class]", description: "Change classes.")]
        public static void ClassChangeCommand(ChatCommandContext ctx, string className)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }

            if (!TryParseClassName(className, out var parsedClassType))
            {
                ctx.Reply("Invalid class, use .classes to see options.");
                return;
            }

            ulong steamId = ctx.Event.User.PlatformId;
            Entity character = ctx.Event.SenderCharacterEntity;
            //int level = PrestigeSystem.GetExperiencePrestigeLevel(steamId);

            if (!Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
            {
                ctx.Reply("Class data not found.");
                return;
            }

            if (Plugin.ChangeClassItem.Value != 0 && !HandleClassChangeItem(ctx, classes, steamId))
            {
                ctx.Reply("You do not have the required item to change classes.");
                return;
            }

            RemoveClassBuffs(ctx, steamId);

            classes.Clear();
            UpdateClassData(character, parsedClassType, classes, steamId);
            ctx.Reply($"You have changed to <color=white>{parsedClassType}</color>");
        }

        [Command(name: "listClasses", shortHand: "classes", adminOnly: false, usage: ".classes", description: "Sets player level.")]
        public static void ListClasses(ChatCommandContext ctx)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }

            string classTypes = string.Join(", ", Enum.GetNames(typeof(LevelingSystem.PlayerClasses)));
            ctx.Reply($"Available Classes: <color=white>{classTypes}</color>");
        }

        [Command(name: "listClassBuffs", shortHand: "lcb", adminOnly: false, usage: ".lcb", description: "Shows perks that can be gained from class.")]
        public static void ClassPerks(ChatCommandContext ctx)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }
            ulong steamId = ctx.Event.User.PlatformId;
            if (Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
            {                
                if (classes.Keys.Count == 0)
                {
                    ctx.Reply("You haven't chosen a class yet.");
                    return;
                }
                PlayerClasses playerClass = classes.Keys.FirstOrDefault();
                List<int> perks = LevelingSystem.GetClassBuffs(steamId);

                if (perks.Count == 0)
                {
                    ctx.Reply("No buffs configured.");
                    return;
                }

                int step = Plugin.MaxPlayerLevel.Value / perks.Count;

                string replyMessage = string.Join(", ", perks.Select((perk, index) =>
                {
                    int level = (index + 1) * step;
                    return $"<color=white>{new PrefabGUID(perk).LookupName()}</color> at level <color=yellow>{level}</color>";
                }));
                ctx.Reply($"{playerClass} perks: {replyMessage}");
            }
            else
            {
                ctx.Reply("You haven't chosen a class yet.");
                return;
            }
        }

        [Command(name: "listClassSpells", shortHand: "lcs", adminOnly: false, usage: ".lcs", description: "Shows perks that can be gained from class.")]
        public static void ListClassSpells(ChatCommandContext ctx)
        {
            if (!Plugin.SoftSynergies.Value && !Plugin.HardSynergies.Value)
            {
                ctx.Reply("Classes are not enabled.");
                return;
            }
            ulong steamId = ctx.Event.User.PlatformId;
            if (Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
            {
                if (classes.Keys.Count == 0)
                {
                    ctx.Reply("You haven't chosen a class yet.");
                    return;
                }
                PlayerClasses playerClass = classes.Keys.FirstOrDefault();
                List<int> perks = LevelingSystem.GetClassSpells(steamId);
                string replyMessage = string.Join("", perks.Select(perk => $"<color=white>{new PrefabGUID(perk).LookupName()}</color>"));
                ctx.Reply($"{playerClass} spells: {replyMessage}");
            }
            else
            {
                ctx.Reply("You haven't chosen a class yet.");
                return;
            }
        }

        [Command(name: "playerPrestige", shortHand: "prestige", adminOnly: false, usage: ".prestige [PrestigeType]", description: "Handles player prestiging.")]
        public static void PrestigeCommand(ChatCommandContext ctx, string prestigeType)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestiging is not enabled.");
                return;
            }

            if (!PrestigeSystem.TryParsePrestigeType(prestigeType, out var parsedPrestigeType))
            {
                ctx.Reply("Invalid prestige, use .lpp to see options.");
                return;
            }

            if ((Plugin.SoftSynergies.Value || Plugin.HardSynergies.Value) &&
                Core.DataStructures.PlayerClasses.TryGetValue(ctx.Event.User.PlatformId, out var classes) &&
                classes.Keys.Count == 0 &&
                parsedPrestigeType == PrestigeSystem.PrestigeType.Experience)
            {
                ctx.Reply("You must choose a class before prestiging in experience.");
                return;
            }

            var steamId = ctx.Event.User.PlatformId;
            var handler = PrestigeHandlerFactory.GetPrestigeHandler(parsedPrestigeType);

            if (handler == null)
            {
                ctx.Reply("Invalid prestige type.");
                return;
            }

            var xpData = handler.GetExperienceData(steamId);
            if (PrestigeSystem.CanPrestige(steamId, parsedPrestigeType, xpData.Key))
            {
                PrestigeSystem.PerformPrestige(ctx, steamId, parsedPrestigeType, handler);
            }
            else
            {
                ctx.Reply($"You have not reached the required level to prestige in <color=#90EE90>{parsedPrestigeType}</color>.");
            }
        }

        [Command(name: "listPrestigeBuffs", shortHand: "lpb", adminOnly: false, usage: ".lpb", description: "Lists prestige buff names.")]
        public static void PrestigeBuffsCommand(ChatCommandContext ctx)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestiging is not enabled.");
                return;
            }

            List<int> buffs = Core.ParseConfigString(Plugin.PrestigeBuffs.Value);

            if (buffs.Count == 0)
            {
                ctx.Reply("Prestiging buffs not found.");
                return;
            }
            string replyMessage = string.Join(", ", buffs.Select((buff, index) =>
            {
                int level = index++;
                return $"<color=white>{new PrefabGUID(buff).LookupName()}</color> at prestige <color=yellow>{level}</color>";
            }));
            ctx.Reply($"{replyMessage}");
        }

        [Command(name: "resetPrestige", shortHand: "rpr", adminOnly: true, usage: ".rpr [Name] [PrestigeType]", description: "Handles resetting prestiging.")]
        public static void ResetPrestige(ChatCommandContext ctx, string name, string prestigeType)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestiging is not enabled.");
                return;
            }

            if (!PrestigeSystem.TryParsePrestigeType(prestigeType, out var parsedPrestigeType))
            {
                ctx.Reply("Invalid prestige, use .lpp to see options.");
                return;
            }

            Entity foundUserEntity = GetUserByName(name, true);

            if (foundUserEntity.Equals(Entity.Null))
            {
                ctx.Reply("Player not found...");
                return;
            }

            User foundUser = foundUserEntity.Read<User>();
            ulong steamId = foundUser.PlatformId;

            if (Core.DataStructures.PlayerPrestiges.TryGetValue(steamId, out var prestigeData) &&
                prestigeData.TryGetValue(parsedPrestigeType, out var prestigeLevel))
            {
                if (parsedPrestigeType == PrestigeSystem.PrestigeType.Experience)
                {
                    PrestigeSystem.RemovePrestigeBuffs(ctx, prestigeLevel);
                }

                prestigeData[parsedPrestigeType] = 0;
                Core.DataStructures.SavePlayerPrestiges();
                ctx.Reply($"<color=#90EE90>{parsedPrestigeType}</color> prestige reset for <color=white>{foundUser.CharacterName}</color>.");
            }
        }

        /*
        [Command(name: "resetPrestige", shortHand: "rpr", adminOnly: true, usage: ".rpr [PrestigeType]", description: "Handles resetting prestiging.")]
        public unsafe static void ResetPrestige(ChatCommandContext ctx, string prestigeType)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestiging is not enabled.");
                return;
            }

            if (!Enum.TryParse(prestigeType, true, out PrestigeSystem.PrestigeType parsedPrestigeType))
            {
                // Attempt a substring match with existing enum names
                parsedPrestigeType = Enum.GetValues(typeof(PrestigeSystem.PrestigeType))
                                         .Cast<PrestigeSystem.PrestigeType>()
                                         .FirstOrDefault(pt => pt.ToString().Contains(prestigeType, StringComparison.OrdinalIgnoreCase));

                if (parsedPrestigeType == default)
                {
                    ctx.Reply("Invalid prestige, use .lpp to see options.");
                    return;
                }
            }
            List<int> buffs = Core.ParseConfigString(Plugin.PrestigeBuffs.Value);
            List<int> classBuffs = [];
            var steamId = ctx.Event.User.PlatformId;
            if (Core.DataStructures.PlayerPrestiges.TryGetValue(steamId, out var prestigeData) && prestigeData.TryGetValue(parsedPrestigeType, out var prestigeLevel))
            {
                if (parsedPrestigeType.Equals(PrestigeSystem.PrestigeType.Experience))
                {
                    BuffUtility.BuffSpawner buffSpawner = BuffUtility.BuffSpawner.Create(Core.ServerGameManager);
                    EntityCommandBuffer entityCommandBuffer = Core.EntityCommandBufferSystem.CreateCommandBuffer();
                    if (Plugin.SoftSynergies.Value || Plugin.HardSynergies.Value)
                    {
                        if (Core.DataStructures.PlayerClasses.TryGetValue(steamId, out var classes))
                        {
                            LevelingSystem.PlayerClasses playerClass = classes.Keys.FirstOrDefault();
                            classBuffs = Core.ParseConfigString(LevelingSystem.ClassPrestigeBuffsMap[playerClass]);
                        }
                    }
                    for (int i = 0; i < prestigeLevel; i++)
                    {
                        PrefabGUID buffPrefab = new(buffs[i]);
                        if (Core.ServerGameManager.TryGetBuff(ctx.Event.SenderCharacterEntity, buffPrefab.ToIdentifier(), out var buff))
                        {
                            BuffUtility.TryRemoveBuff(ref buffSpawner, entityCommandBuffer, buffPrefab.ToIdentifier(), ctx.Event.SenderCharacterEntity);
                        }
                        if (classBuffs.Count == 0 || classBuffs[i].Equals(0)) continue;
                        buffPrefab = new(classBuffs[i]);
                        if (Core.ServerGameManager.TryGetBuff(ctx.Event.SenderCharacterEntity, buffPrefab.ToIdentifier(), out var classBuff))
                        {
                            BuffUtility.TryRemoveBuff(ref buffSpawner, entityCommandBuffer, buffPrefab.ToIdentifier(), ctx.Event.SenderCharacterEntity);
                        }
                    }
                }
                prestigeData[parsedPrestigeType] = 0;
                Core.DataStructures.SavePlayerPrestiges();
                ctx.Reply($"<color=#90EE90>{parsedPrestigeType}</color> prestige reset.");
            }
        }
        */

        [Command(name: "getPrestige", shortHand: "gpr", adminOnly: false, usage: ".gpr [PrestigeType]", description: "Shows information about player's prestige status.")]
        public unsafe static void GetPrestigeCommand(ChatCommandContext ctx, string prestigeType)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestiging is not enabled.");
                return;
            }

            if (!PrestigeSystem.TryParsePrestigeType(prestigeType, out var parsedPrestigeType))
            {
                ctx.Reply("Invalid prestige, use .lpp to see options.");
                return;
            }

            var steamId = ctx.Event.User.PlatformId;
            var handler = PrestigeHandlerFactory.GetPrestigeHandler(parsedPrestigeType);

            if (handler == null)
            {
                ctx.Reply("Invalid prestige type.");
                return;
            }

            var maxPrestigeLevel = PrestigeSystem.PrestigeTypeToMaxPrestigeLevel[parsedPrestigeType];
            if (Core.DataStructures.PlayerPrestiges.TryGetValue(steamId, out var prestigeData) &&
                prestigeData.TryGetValue(parsedPrestigeType, out var prestigeLevel) && prestigeLevel > 0)
            {
                PrestigeSystem.DisplayPrestigeInfo(ctx, steamId, parsedPrestigeType, prestigeLevel, maxPrestigeLevel);
            }
            else
            {
                ctx.Reply($"You have not prestiged in <color=#90EE90>{parsedPrestigeType}</color>.");
            }
        }

        [Command(name: "listPlayerPrestiges", shortHand: "lpp", adminOnly: false, usage: ".lpp", description: "Lists prestiges available.")]
        public static void ListPlayerPrestigeTypes(ChatCommandContext ctx)
        {
            if (!Plugin.PrestigeSystem.Value)
            {
                ctx.Reply("Prestige is not enabled.");
                return;
            }
            string prestigeTypes = string.Join(", ", Enum.GetNames(typeof(PrestigeSystem.PrestigeType)));
            ctx.Reply($"Available Prestiges: <color=#90EE90>{prestigeTypes}</color>");
        }

        [Command(name: "toggleGrouping", shortHand: "grouping", adminOnly: false, usage: ".grouping", description: "Toggles being able to be invited to group with players not in clan for exp sharing.")]
        public static void ToggleGroupingCommand(ChatCommandContext ctx)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            if (!Plugin.PlayerGrouping.Value)
            {
                ctx.Reply("Grouping is not enabled.");
                return;
            }

            var SteamID = ctx.Event.User.PlatformId;

            if (Core.DataStructures.PlayerBools.TryGetValue(SteamID, out var bools))
            {
                bools["Grouping"] = !bools["Grouping"];
            }
            Core.DataStructures.SavePlayerBools();
            ctx.Reply($"Group invites {(bools["Grouping"] ? "<color=green>enabled</color>" : "<color=red>disabled</color>")}.");
        }

        [Command(name: "groupAdd", shortHand: "ga", adminOnly: false, usage: ".ga [Player]", description: "Adds player to group for exp sharing if they permit it in settings.")]
        public static void GroupAddCommand(ChatCommandContext ctx, string name)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            ulong ownerId = ctx.Event.User.PlatformId;
            Entity foundUserEntity = GetUserByName(name);
            if (foundUserEntity.Equals(Entity.Null))
            {
                ctx.Reply("Player not found...");
                return;
            }
            User foundUser = foundUserEntity.Read<User>();
            if (foundUser.PlatformId == ownerId)
            {
                ctx.Reply("You can't add yourself to your own group.");
                return;
            }
            Entity characterToAdd = foundUser.LocalCharacter._Entity;
            if (Core.DataStructures.PlayerBools.TryGetValue(foundUser.PlatformId, out var bools) && bools["Grouping"] && !Core.DataStructures.PlayerGroups.ContainsKey(foundUser.PlatformId)) // get consent, make sure they don't have their own group made first
            {
                if (!Core.DataStructures.PlayerGroups.ContainsKey(ownerId)) // check if player has a group, make one if not
                {
                    Core.DataStructures.PlayerGroups[ownerId] = [];
                }
                List<Entity> group = Core.DataStructures.PlayerGroups[ownerId]; // check size and if player is already present in group before adding
                if (group.Count < Plugin.MaxGroupSize.Value && !group.Contains(characterToAdd))
                {
                    group.Add(characterToAdd);
                    ctx.Reply($"<color=green>{foundUser.CharacterName}</color> added to your group.");
                }
                else
                {
                    ctx.Reply($"Group is full or <color=green>{foundUser.CharacterName}</color> is already in group.");
                }
            }
            else
            {
                ctx.Reply($"<color=green>{foundUser.CharacterName}</color> does not have grouping enabled or they already have a group created.");
            }
        }

        [Command(name: "groupRemove", shortHand: "gr", adminOnly: false, usage: ".gr [Player]", description: "Removes player from group for exp sharing.")]
        public static void GroupRemoveCommand(ChatCommandContext ctx, string name)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }
            ulong ownerId = ctx.Event.User.PlatformId;
            Entity foundUserEntity = GetUserByName(name, true);
            if (foundUserEntity.Equals(Entity.Null))
            {
                ctx.Reply("Player not found...");
                return;
            }
            User foundUser = foundUserEntity.Read<User>();
            Entity characterToRemove = foundUser.LocalCharacter._Entity;

            if (!Core.DataStructures.PlayerGroups.ContainsKey(ownerId)) // check if player has a group, make one if not
            {
                ctx.Reply("You don't have a group. Create one and add people before trying to remove them.");
                return;
            }
            List<Entity> group = Core.DataStructures.PlayerGroups[ownerId]; // check size and if player is already present in group before adding
            if (group.Contains(characterToRemove))
            {
                group.Remove(characterToRemove);
                ctx.Reply($"<color=green>{foundUser.CharacterName}</color> removed from your group.");
            }
            else
            {
                ctx.Reply($"<color=green>{foundUser.CharacterName}</color> is not in the group and therefore cannot be removed from it.");
            }
        }

        [Command(name: "groupDisband", shortHand: "disband", adminOnly: false, usage: ".disband", description: "Disbands exp sharing group.")]
        public static void GroupDisbandCommand(ChatCommandContext ctx)
        {
            if (!Plugin.LevelingSystem.Value)
            {
                ctx.Reply("Leveling is not enabled.");
                return;
            }

            ulong ownerId = ctx.Event.User.PlatformId;

            if (!Core.DataStructures.PlayerGroups.ContainsKey(ownerId)) // check if player has a group, make one if not
            {
                ctx.Reply("You don't have a group. Create one by adding a player who has group invites enabled.");
                return;
            }
            else
            {
                Core.DataStructures.PlayerGroups.Remove(ownerId);
                ctx.Reply("Group disbanded.");
            }
        }
        /*
        [Command(name: "bufftest", shortHand: "bufftest", adminOnly: true, usage: ".bufftest", description: "buff test")]
        public static void TestCommand(ChatCommandContext ctx)
        {
          
            DebugEventsSystem debugEventsSystem = Core.DebugEventsSystem;
            ServerGameManager serverGameManager = Core.ServerGameManager;
            ApplyBuffDebugEvent applyBuffDebugEvent = new()
            {
                BuffPrefabGUID = new(-429891372) // mugging powersurge for it's components, prefectly ethical
            };
            FromCharacter fromCharacter = new()
            {
                Character = ctx.Event.SenderCharacterEntity,
                User = ctx.Event.SenderUserEntity
            };


            debugEventsSystem.ApplyBuff(fromCharacter, applyBuffDebugEvent);
            if (serverGameManager.TryGetBuff(ctx.Event.SenderCharacterEntity, applyBuffDebugEvent.BuffPrefabGUID.ToIdentifier(), out Entity firstBuff)) // if present, modify based on prestige level
            {
                Core.Log.LogInfo($"Applied {applyBuffDebugEvent.BuffPrefabGUID.LookupName()} for class buff, modifying...");

                Buff buff = firstBuff.Read<Buff>();
                buff.BuffType = BuffType.Parallel;
                firstBuff.Write(buff);

                if (firstBuff.Has<RemoveBuffOnGameplayEvent>())
                {
                    firstBuff.Remove<RemoveBuffOnGameplayEvent>();
                }
                if (firstBuff.Has<RemoveBuffOnGameplayEventEntry>())
                {
                    firstBuff.Remove<RemoveBuffOnGameplayEventEntry>();
                }
                if (firstBuff.Has<CreateGameplayEventsOnSpawn>())
                {
                    firstBuff.Remove<CreateGameplayEventsOnSpawn>();
                }
                if (!firstBuff.Has<Buff_Persists_Through_Death>())
                {
                    firstBuff.Add<Buff_Persists_Through_Death>();
                }
                if (firstBuff.Has<LifeTime>())
                {
                    LifeTime lifeTime = firstBuff.Read<LifeTime>();
                    lifeTime.Duration = -1;
                    lifeTime.EndAction = LifeTimeEndAction.None;
                    firstBuff.Write(lifeTime);
                }
                if (Core.DataStructures.PlayerClasses.TryGetValue(ctx.Event.User.PlatformId, out var classes) && classes.Keys.Count > 0) // so basically if prestiged already and at the level threshold again, handle the buff matching the index and scale for prestige
                {
                    PlayerClasses playerClass = classes.Keys.FirstOrDefault();
                    Buff_ApplyBuffOnDamageTypeDealt_DataShared onHitBuff = firstBuff.Read<Buff_ApplyBuffOnDamageTypeDealt_DataShared>();
                    onHitBuff.ProcBuff = ClassApplyBuffOnDamageDealtMap[playerClass];
                    onHitBuff.ProcChance = 1;
                    firstBuff.Write(onHitBuff);

                    Core.Log.LogInfo($"Applied {onHitBuff.ProcBuff.GetPrefabName()} to class buff, removing uneeded components...");

                    if (firstBuff.Has<Buff_EmpowerDamageDealtByType_DataShared>())
                    {
                        firstBuff.Remove<Buff_EmpowerDamageDealtByType_DataShared>();
                    }
                    if (firstBuff.Has<ModifyMovementSpeedBuff>())
                    {
                        firstBuff.Remove<ModifyMovementSpeedBuff>();
                    }
                    if (firstBuff.Has<CreateGameplayEventOnBuffReapply>())
                    {
                        firstBuff.Remove<CreateGameplayEventOnBuffReapply>();
                    }
                    if (firstBuff.Has<AdjustLifetimeOnGameplayEvent>())
                    {
                        firstBuff.Remove<AdjustLifetimeOnGameplayEvent>();
                    }
                    if (firstBuff.Has<ApplyBuffOnGameplayEvent>())
                    {
                        firstBuff.Remove<CreateGameplayEventsOnSpawn>();
                    }
                    if (firstBuff.Has<SpellModSetComponent>())
                    {
                        firstBuff.Remove<SpellModSetComponent>();
                    }
                    if (firstBuff.Has<ApplyBuffOnGameplayEvent>())
                    {
                        firstBuff.Remove<ApplyBuffOnGameplayEvent>();
                    }
                    if (firstBuff.Has<SpellModArithmetic>())
                    {
                        firstBuff.Remove<SpellModArithmetic>();
                    }

                           
                }

            }
                    // each class gets an applyBuffOnDamageTypeDealt effect? like BloodKnight gets one that has a chance to proc leech, that I could somewhat safely scale with prestige level easily       
        }

        [Command(name: "shapetest", shortHand: "shapetest", adminOnly: true, usage: ".shapetest", description: "shapeshift test")]
        public static void ShiftTestCommand(ChatCommandContext ctx)
        {

            Entity dracula_SpellPhase = Core.PrefabCollectionSystem._PrefabGuidToEntityMap[new(-1785054766)];
            var buffer = dracula_SpellPhase.ReadBuffer<LinkedEntityGroup>();
            for (int i = 0; i < buffer.Length; i++)
            {
                Entity entity = buffer[i].Value;
                Core.Log.LogInfo(entity.Read<PrefabGUID>().LookupName());
                entity.LogComponentTypes();
            }
        }
        */
    }
}