using Il2CppInterop.Runtime;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using System.Collections;
using UnityEngine;
using ProjectM;

namespace Bloodcraft.Services;
internal class PlayerService
{
	static readonly ComponentType[] UserComponent =
		[
			ComponentType.ReadOnly(Il2CppType.Of<User>()),
		];

	static readonly ComponentType[] ClanComponent =
        [
            ComponentType.ReadOnly(Il2CppType.Of<ClanMemberStatus>()),
        ];	

	static EntityQuery ActiveUsersQuery;

	static EntityQuery AllUsersQuery;

	static EntityQuery ClansQuery;

	public static Dictionary<string, Entity> playerNameCache = []; //player name, player userEntity

	public static Dictionary<ulong, Entity> playerIdCache = []; //player steamID, player charEntity

	public PlayerService()
	{
		ActiveUsersQuery = Core.EntityManager.CreateEntityQuery(UserComponent);
		AllUsersQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
		{
			All = UserComponent,
			Options = EntityQueryOptions.IncludeDisabled
		});
		ClansQuery = Core.EntityManager.CreateEntityQuery(ClanComponent);
		Core.StartCoroutine(CacheUpdateLoop());
	}
	static IEnumerator CacheUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(60);

			//Core.Log.LogInfo("Updating player cache...");

			playerNameCache.Clear();
            playerNameCache = GetUsers()
                .Select(userEntity => new { CharacterName = userEntity.Read<User>().CharacterName.Value, Entity = userEntity })
				.GroupBy(user => user.CharacterName)
				.Select(group => group.First())
				.ToDictionary(user => user.CharacterName, user => user.Entity); // playerName : userEntity

			playerIdCache.Clear();
			playerIdCache = GetUsers()
                .Select(userEntity => new { SteamID = userEntity.Read<User>().PlatformId, Entity = userEntity.Read<User>().LocalCharacter._Entity })
				.GroupBy(user => user.SteamID)
				.Select(group => group.First())
				.ToDictionary(user => user.SteamID, user => user.Entity); // steamID : charEntity
        }
    }
	public static IEnumerable<Entity> GetUsers(bool includeDisabled = false)
	{
		NativeArray<Entity> userEntities = includeDisabled ? AllUsersQuery.ToEntityArray(Allocator.TempJob) : ActiveUsersQuery.ToEntityArray(Allocator.TempJob);
		try
		{
			foreach (Entity entity in userEntities)
			{
				if (Core.EntityManager.Exists(entity))
				{
					yield return entity;
				}
			}
		}
		finally
		{
			userEntities.Dispose();
		}
	}
    public static IEnumerable<Entity> GetClans()
    {
        NativeArray<Entity> clanEntities = ClansQuery.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (Entity entity in clanEntities)
            {
                if (Core.EntityManager.Exists(entity))
                {
                    yield return entity;
                }
            }
        }
        finally
        {
            clanEntities.Dispose();
        }
    }
    public static Entity GetUserByName(string playerName, bool includeDisabled = false)
	{
		Entity userEntity = GetUsers(includeDisabled).FirstOrDefault(entity => entity.Read<User>().CharacterName.Value.ToLower() == playerName.ToLower());
		return userEntity != Entity.Null ? userEntity : Entity.Null;
	}
	public static Entity GetClanByName(string clanName)
    {
		Entity clanEntity = GetClans().FirstOrDefault(entity => entity.Read<ClanTeam>().Name.Value.ToLower() == clanName.ToLower());
        return clanEntity != Entity.Null ? clanEntity : Entity.Null;
    }
}
