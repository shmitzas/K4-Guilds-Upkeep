using Dapper;
using Dommel;
using K4_Guilds.Database.Models;

namespace K4_Guilds.Database;

public partial class DatabaseService
{
	public async Task<IEnumerable<GuildMember>> GetGuildMembersAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();
		return await conn.SelectAsync<GuildMember>(m => m.GuildId == guildId);
	}

	public async Task<GuildMember?> GetGuildMemberAsync(ulong steamId)
	{
		using var conn = GetConnection();
		conn.Open();
		return await conn.QueryFirstOrDefaultAsync<GuildMember>(
			"SELECT id AS Id, guild_id AS GuildId, steam_id AS SteamId, player_name AS PlayerName, rank_priority AS RankPriority, joined_at AS JoinedAt, last_seen AS LastSeen FROM k4_guild_members WHERE steam_id = @SteamId",
			new { SteamId = (long)steamId });
	}

	public async Task<GuildMember> AddMemberAsync(int guildId, ulong steamId, string playerName, int rankPriority)
	{
		using var conn = GetConnection();
		conn.Open();

		var member = new GuildMember
		{
			GuildId = guildId,
			SteamId = steamId,
			PlayerName = playerName,
			RankPriority = rankPriority,
			JoinedAt = DateTime.UtcNow,
			LastSeen = DateTime.UtcNow
		};
		member.Id = Convert.ToInt32(await conn.InsertAsync(member));
		return member;
	}

	public async Task<bool> RemoveMemberAsync(ulong steamId)
	{
		using var conn = GetConnection();
		conn.Open();

		var affected = await conn.ExecuteAsync(
			"DELETE FROM k4_guild_members WHERE steam_id = @SteamId",
			new { SteamId = (long)steamId });
		return affected > 0;
	}

	public async Task<bool> UpdateMemberRankAsync(ulong steamId, int rankPriority)
	{
		using var conn = GetConnection();
		conn.Open();

		var affected = await conn.ExecuteAsync(
			"UPDATE k4_guild_members SET rank_priority = @Rank WHERE steam_id = @SteamId",
			new { Rank = rankPriority, SteamId = (long)steamId });
		return affected > 0;
	}
}
