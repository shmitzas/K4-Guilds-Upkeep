using Dommel;
using K4_Guilds.Database.Models;
using K4Guilds.Shared;

namespace K4_Guilds.Database;

public partial class DatabaseService
{
	public async Task<IEnumerable<GuildUpgrade>> GetGuildUpgradesAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();
		return await conn.SelectAsync<GuildUpgrade>(u => u.GuildId == guildId);
	}

	public async Task<int> GetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType)
	{
		using var conn = GetConnection();
		conn.Open();

		var upgradeTypeInt = (int)upgradeType;
		var upgrade = (await conn.SelectAsync<GuildUpgrade>(u => u.GuildId == guildId && u.UpgradeType == upgradeTypeInt)).FirstOrDefault();
		return upgrade?.Level ?? 0;
	}

	public async Task SetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType, int level)
	{
		using var conn = GetConnection();
		conn.Open();

		var upgradeTypeInt = (int)upgradeType;
		var existing = (await conn.SelectAsync<GuildUpgrade>(u => u.GuildId == guildId && u.UpgradeType == upgradeTypeInt)).FirstOrDefault();

		if (existing != null)
		{
			existing.Level = level;
			existing.PurchasedAt = DateTime.UtcNow;
			await conn.UpdateAsync(existing);
		}
		else
		{
			var upgrade = new GuildUpgrade
			{
				GuildId = guildId,
				UpgradeType = upgradeTypeInt,
				Level = level,
				PurchasedAt = DateTime.UtcNow
			};
			await conn.InsertAsync(upgrade);
		}
	}
}
