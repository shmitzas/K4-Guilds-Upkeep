using K4Guilds.Shared;

namespace K4_Guilds.Services;

internal partial class GuildsApiService
{
	public void RegisterPerk(IGuildPerk perk) => plugin.GetPerkService().RegisterPerk(perk);
	public void UnregisterPerk(string perkId) => plugin.GetPerkService().UnregisterPerk(perkId);
	public IReadOnlyList<IGuildPerk> GetRegisteredPerks() => plugin.GetPerkService().GetRegisteredPerks();

	public async Task<int> GetPerkLevelAsync(int guildId, string perkId)
		=> await plugin.GetPerkService().GetPerkLevelAsync(guildId, perkId);

	public async Task<bool> IsPerkActiveAsync(int guildId, string perkId)
		=> await plugin.GetPerkService().IsPerkActiveAsync(guildId, perkId);

	public async Task<(bool Success, int NewLevel, string Message)> PurchaseOrUpgradePerkAsync(int guildId, string perkId, ulong buyerSteamId)
	{
		var (success, newLevel, message) = await plugin.GetPerkService().PurchaseOrUpgradePerkAsync(guildId, perkId, buyerSteamId);
		return (success, newLevel, message.Key);
	}

	public async Task<bool> TogglePerkAsync(int guildId, string perkId)
	{
		var (success, _) = await plugin.GetPerkService().TogglePerkAsync(guildId, perkId);
		return success;
	}
}
