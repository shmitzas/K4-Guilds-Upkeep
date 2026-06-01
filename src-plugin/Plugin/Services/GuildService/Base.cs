using System.Collections.Concurrent;
using K4_Guilds.Database;
using K4_Guilds.Database.Models;
using Microsoft.Extensions.Logging;

namespace K4_Guilds.Services;

public partial class GuildService(Plugin plugin, DatabaseService database)
{
	private const int CACHE_TTL_SECONDS = 300;

	private InviteService? _inviteService;

	private readonly ConcurrentDictionary<ulong, (Guild? Guild, DateTime ExpiresAt)> _playerGuildCache = new();
	private readonly ConcurrentDictionary<int, (Guild Guild, DateTime ExpiresAt)> _guildCache = new();

	public void SetInviteService(InviteService inviteService) => _inviteService = inviteService;

	public async Task<Guild?> GetGuildAsync(int guildId)
	{
		if (_guildCache.TryGetValue(guildId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
			return cached.Guild;

		var guild = await database.GetGuildAsync(guildId);

		if (guild != null)
			_guildCache[guildId] = (guild, DateTime.UtcNow.AddSeconds(CACHE_TTL_SECONDS));
		else
			_guildCache.TryRemove(guildId, out _);

		return guild;
	}

	public async Task<Guild?> GetGuildByNameAsync(string name)
		=> await database.GetGuildByNameAsync(name);

	public async Task<Guild?> GetPlayerGuildAsync(ulong steamId)
	{
		if (_playerGuildCache.TryGetValue(steamId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
		{
			if (plugin.Guild.DetailedLogging)
				Plugin.Core.Logger.LogInformation("[K4-Guilds] GuildService.GetPlayerGuildAsync: cache HIT for SteamID={SteamID}, guild={GuildName}.", steamId, cached.Guild?.Name ?? "(none)");
			return cached.Guild;
		}

		if (plugin.Guild.DetailedLogging)
			Plugin.Core.Logger.LogInformation("[K4-Guilds] GuildService.GetPlayerGuildAsync: cache MISS for SteamID={SteamID}, querying DB.", steamId);

		var guild = await database.GetPlayerGuildAsync(steamId);
		var expiry = DateTime.UtcNow.AddSeconds(CACHE_TTL_SECONDS);

		// Self-heal: if this player is the guild leader but has no member row
		// (legacy data / failed insert / manual DB edit), recreate the member row
		// with the leader rank so they're treated as part of the guild.
		if (guild != null && guild.LeaderSteamId == steamId && guild.Members.All(m => m.SteamId != steamId))
		{
			var leaderRank = GetLeaderRank();
			var leaderName = guild.Members.FirstOrDefault()?.PlayerName ?? steamId.ToString();
			Plugin.Core.Logger.LogWarning(
				"[K4-Guilds] Leader SteamID={SteamID} of guild '{GuildName}' (Id={GuildId}) had no row in k4_guild_members. Inserting one with leader rank.",
				steamId, guild.Name, guild.Id);

			var newMember = await database.AddMemberAsync(guild.Id, steamId, leaderName, leaderRank?.Priority ?? 100);
			guild.Members.Add(newMember);
		}

		_playerGuildCache[steamId] = (guild, expiry);

		if (guild != null)
			_guildCache[guild.Id] = (guild, expiry);

		if (plugin.Guild.DetailedLogging)
			Plugin.Core.Logger.LogInformation("[K4-Guilds] GuildService.GetPlayerGuildAsync: DB result for SteamID={SteamID} => guild={GuildName} (null={IsNull}).", steamId, guild?.Name ?? "(none)", guild == null);

		return guild;
	}

	public void InvalidatePlayerCache(ulong steamId)
		=> _playerGuildCache.TryRemove(steamId, out _);

	public void InvalidateGuildCache(int guildId)
	{
		_guildCache.TryRemove(guildId, out _);

		var toRemove = _playerGuildCache
			.Where(x => x.Value.Guild?.Id == guildId)
			.Select(x => x.Key)
			.ToArray();

		foreach (var key in toRemove)
			_playerGuildCache.TryRemove(key, out _);
	}

	public void InvalidateMemberCache(ulong steamId, int guildId)
	{
		InvalidatePlayerCache(steamId);
		InvalidateGuildCache(guildId);
	}

	public GuildMember? FindMember(Guild guild, ulong steamId)
		=> guild.Members.FirstOrDefault(m => m.SteamId == steamId);
}
