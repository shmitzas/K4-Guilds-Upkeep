using System.Data;
using Dapper;
using Dommel;
using K4_Guilds.Database.Models;
using Microsoft.Extensions.Logging;


namespace K4_Guilds.Database;

public partial class DatabaseService
{
	public async Task<Guild?> GetGuildAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();

		var guild = await conn.GetAsync<Guild>(guildId);
		if (guild != null)
			await LoadGuildRelationsAsync(guild);

		return guild;
	}

	public async Task<Guild?> GetGuildByNameAsync(string name)
	{
		using var conn = GetConnection();
		conn.Open();

		var guild = (await conn.SelectAsync<Guild>(g => g.Name == name)).FirstOrDefault();
		if (guild != null)
			await LoadGuildRelationsAsync(guild);

		return guild;
	}

	public async Task<Guild?> GetPlayerGuildAsync(ulong steamId)
	{
		using var conn = GetConnection();
		conn.Open();

		if (Plugin.GuildConfig.CurrentValue.DetailedLogging)
			Plugin.Core.Logger.LogInformation("[K4-Guilds] DatabaseService.GetPlayerGuildAsync: querying k4_guild_members / k4_guilds (leader fallback) for steam_id={SteamId} (long={SteamIdLong}).", steamId, (long)steamId);

		// Look up by membership first; fall back to leader_steam_id so guild owners
		// whose member row is missing (legacy data / failed insert / manual DB edits)
		// are still recognized as belonging to their guild.
		var guildId = await conn.QueryFirstOrDefaultAsync<int?>(
			"SELECT guild_id FROM k4_guild_members WHERE steam_id = @SteamId " +
			"UNION " +
			"SELECT id FROM k4_guilds WHERE leader_steam_id = @SteamId " +
			"LIMIT 1",
			new { SteamId = (long)steamId });

		if (Plugin.GuildConfig.CurrentValue.DetailedLogging)
			Plugin.Core.Logger.LogInformation("[K4-Guilds] DatabaseService.GetPlayerGuildAsync: steam_id={SteamId} => guildId={GuildId} (null={IsNull}).", steamId, guildId, guildId == null);

		if (guildId == null) return null;

		var guild = await conn.GetAsync<Guild>(guildId.Value);
		if (guild != null)
			await LoadGuildRelationsAsync(guild);

		if (Plugin.GuildConfig.CurrentValue.DetailedLogging)
			Plugin.Core.Logger.LogInformation("[K4-Guilds] DatabaseService.GetPlayerGuildAsync: loaded guild={GuildName} (null={IsNull}) for SteamID={SteamId}.", guild?.Name ?? "(none)", guild == null, steamId);

		return guild;
	}

	public async Task<Guild> CreateGuildAsync(ulong leaderSteamId, string leaderName, string name, string tag, int leaderRankPriority)
	{
		using var conn = GetConnection();
		conn.Open();
		using var transaction = conn.BeginTransaction();

		try
		{
			var guild = new Guild
			{
				Name = name,
				Tag = tag,
				LeaderSteamId = leaderSteamId,
				BankBalance = 0,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
			guild.Id = Convert.ToInt32(await conn.InsertAsync(guild, transaction));

			var member = new GuildMember
			{
				GuildId = guild.Id,
				SteamId = leaderSteamId,
				PlayerName = leaderName,
				RankPriority = leaderRankPriority,
				JoinedAt = DateTime.UtcNow,
				LastSeen = DateTime.UtcNow
			};
			await conn.InsertAsync(member, transaction);

			transaction.Commit();

			guild.Members = [member];
			guild.Upgrades = [];
			guild.EnabledPerks = [];
			return guild;
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	public async Task<bool> DisbandGuildAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();

		var guild = await conn.GetAsync<Guild>(guildId);
		if (guild == null) return false;

		return await conn.DeleteAsync(guild);
	}

	public async Task<IReadOnlyList<Guild>> GetAllGuildsAsync(int page, int pageSize)
	{
		using var conn = GetConnection();
		conn.Open();

		var guilds = (await conn.QueryAsync<Guild>(
			"SELECT id AS Id, name AS Name, tag AS Tag, leader_steam_id AS LeaderSteamId, bank_balance AS BankBalance, created_at AS CreatedAt, updated_at AS UpdatedAt FROM k4_guilds ORDER BY created_at DESC LIMIT @PageSize OFFSET @Offset",
			new { PageSize = pageSize, Offset = page * pageSize })).ToList();

		if (guilds.Count == 0) return guilds;

		await LoadGuildsRelationsBatchAsync(conn, guilds);
		return guilds;
	}

	public async Task UpdateGuildNameAsync(int guildId, string newName, string newTag)
	{
		using var conn = GetConnection();
		conn.Open();

		var guild = await conn.GetAsync<Guild>(guildId);
		if (guild != null)
		{
			guild.Name = newName;
			guild.Tag = newTag;
			guild.UpdatedAt = DateTime.UtcNow;
			await conn.UpdateAsync(guild);
		}
	}

	private async Task LoadGuildRelationsAsync(Guild guild)
	{
		// Open three separate connections so all three queries run in parallel
		using var conn1 = GetConnection(); conn1.Open();
		using var conn2 = GetConnection(); conn2.Open();
		using var conn3 = GetConnection(); conn3.Open();

		var membersTask  = conn1.SelectAsync<GuildMember>(m => m.GuildId == guild.Id);
		var upgradesTask = conn2.SelectAsync<GuildUpgrade>(u => u.GuildId == guild.Id);
		var perksTask    = conn3.SelectAsync<GuildPerk>(p => p.GuildId == guild.Id);

		await Task.WhenAll(membersTask, upgradesTask, perksTask);

		guild.Members      = [.. membersTask.Result];
		guild.Upgrades     = [.. upgradesTask.Result];
		guild.EnabledPerks = [.. perksTask.Result];
	}

	private static async Task LoadGuildsRelationsBatchAsync(IDbConnection conn, List<Guild> guilds)
	{
		var guildIds = guilds.Select(g => g.Id).ToHashSet();

		var allMembers = (await conn.SelectAsync<GuildMember>(m => guildIds.Contains(m.GuildId))).ToLookup(m => m.GuildId);
		var allUpgrades = (await conn.SelectAsync<GuildUpgrade>(u => guildIds.Contains(u.GuildId))).ToLookup(u => u.GuildId);
		var allPerks = (await conn.SelectAsync<GuildPerk>(p => guildIds.Contains(p.GuildId))).ToLookup(p => p.GuildId);

		foreach (var guild in guilds)
		{
			guild.Members = [.. allMembers[guild.Id]];
			guild.Upgrades = [.. allUpgrades[guild.Id]];
			guild.EnabledPerks = [.. allPerks[guild.Id]];
		}
	}
}
