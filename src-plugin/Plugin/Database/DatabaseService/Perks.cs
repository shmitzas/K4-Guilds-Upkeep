using Dapper;
using Dommel;
using K4_Guilds.Database.Models;
using MySqlConnector;

namespace K4_Guilds.Database;

public partial class DatabaseService
{
	public async Task<IEnumerable<GuildPerk>> GetGuildPerksAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();
		return await conn.SelectAsync<GuildPerk>(p => p.GuildId == guildId);
	}

	public async Task<GuildPerk?> GetGuildPerkAsync(int guildId, string perkId)
	{
		using var conn = GetConnection();
		conn.Open();
		return (await conn.SelectAsync<GuildPerk>(p => p.GuildId == guildId && p.PerkId == perkId)).FirstOrDefault();
	}

	public async Task<int> GetPerkLevelAsync(int guildId, string perkId)
	{
		var perk = await GetGuildPerkAsync(guildId, perkId);
		return perk?.Level ?? 0;
	}

	public async Task<bool> IsPerkActiveAsync(int guildId, string perkId)
	{
		var perk = await GetGuildPerkAsync(guildId, perkId);
		return perk != null && perk.Level > 0 && perk.Enabled;
	}

	public async Task<int> SetPerkLevelAsync(int guildId, string perkId, int level, bool enabled = true)
	{
		using var conn = GetConnection();
		conn.Open();

		var now = DateTime.UtcNow;
		string sql = conn switch
		{
			MySqlConnection => """
				INSERT INTO k4_guild_perks (guild_id, perk_id, level, enabled, purchased_at, updated_at)
				VALUES (@GuildId, @PerkId, @Level, @Enabled, @Now, @Now)
				ON DUPLICATE KEY UPDATE level = @Level, enabled = @Enabled, updated_at = @Now
				""",
			_ => """
				INSERT INTO k4_guild_perks (guild_id, perk_id, level, enabled, purchased_at, updated_at)
				VALUES (@GuildId, @PerkId, @Level, @Enabled, @Now, @Now)
				ON CONFLICT (guild_id, perk_id) DO UPDATE SET level = @Level, enabled = @Enabled, updated_at = @Now
				"""
		};

		await conn.ExecuteAsync(sql, new { GuildId = guildId, PerkId = perkId, Level = level, Enabled = enabled, Now = now });
		return level;
	}

	public async Task<(bool Success, bool NewState)> TogglePerkEnabledAsync(int guildId, string perkId)
	{
		using var conn = GetConnection();
		conn.Open();

		var existing = (await conn.SelectAsync<GuildPerk>(p => p.GuildId == guildId && p.PerkId == perkId)).FirstOrDefault();
		if (existing == null || existing.Level == 0) return (false, false);

		existing.Enabled = !existing.Enabled;
		existing.UpdatedAt = DateTime.UtcNow;
		await conn.UpdateAsync(existing);
		return (true, existing.Enabled);
	}
}
