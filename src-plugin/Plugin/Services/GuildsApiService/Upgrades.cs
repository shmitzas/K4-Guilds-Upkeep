using K4Guilds.Shared;

namespace K4_Guilds.Services;

internal partial class GuildsApiService
{
	public async Task<int> GetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType)
		=> await plugin.GetUpgradeService().GetUpgradeLevelAsync(guildId, upgradeType);

	public async Task<long> GetUpgradeCostAsync(int guildId, GuildUpgradeType upgradeType)
	{
		var currentLevel = await GetUpgradeLevelAsync(guildId, upgradeType);
		return plugin.Upgrades[upgradeType].GetCost(currentLevel);
	}
}
