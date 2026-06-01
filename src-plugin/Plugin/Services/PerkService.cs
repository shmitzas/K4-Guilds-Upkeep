using K4_Guilds.Database;
using K4Guilds.Shared;
using Microsoft.Extensions.Logging;

namespace K4_Guilds.Services;

public sealed class PerkService(Plugin plugin, DatabaseService database)
{
	private readonly Dictionary<string, IGuildPerk> _registeredPerks = [];

	public void RegisterPerk(IGuildPerk perk)
	{
		_registeredPerks[perk.Id] = perk;
		Plugin.Core.Logger.LogInformation("Registered guild perk: {PerkId} - {PerkName} ({PerkType})", perk.Id, perk.Name, perk.PerkType);
	}

	public void UnregisterPerk(string perkId)
	{
		if (_registeredPerks.Remove(perkId))
			Plugin.Core.Logger.LogInformation("Unregistered guild perk: {PerkId}", perkId);
	}

	public IReadOnlyList<IGuildPerk> GetRegisteredPerks() => [.. _registeredPerks.Values];

	/// <summary>Get perk level from preloaded guild data (no DB query)</summary>
	public int GetPerkLevel(IReadOnlyList<Database.Models.GuildPerk> guildPerks, string perkId)
	{
		if (!_registeredPerks.ContainsKey(perkId)) return 0;
		var perk = guildPerks.FirstOrDefault(p => p.PerkId == perkId);
		return perk?.Level ?? 0;
	}

	/// <summary>Check if perk is active from preloaded guild data (no DB query)</summary>
	public bool IsPerkActive(IReadOnlyList<Database.Models.GuildPerk> guildPerks, string perkId)
	{
		if (!_registeredPerks.ContainsKey(perkId)) return false;
		var perk = guildPerks.FirstOrDefault(p => p.PerkId == perkId);
		return perk != null && perk.Level > 0 && perk.Enabled;
	}

	/// <summary>Get perk level with DB lookup (use when guild not preloaded)</summary>
	public async Task<int> GetPerkLevelAsync(int guildId, string perkId)
	{
		if (!_registeredPerks.ContainsKey(perkId)) return 0;
		return await database.GetPerkLevelAsync(guildId, perkId);
	}

	/// <summary>Check if perk is active with DB lookup (use when guild not preloaded)</summary>
	public async Task<bool> IsPerkActiveAsync(int guildId, string perkId)
	{
		if (!_registeredPerks.ContainsKey(perkId)) return false;
		return await database.IsPerkActiveAsync(guildId, perkId);
	}

	public async Task<(bool Success, int NewLevel, LocalizedMessage Message)> PurchaseOrUpgradePerkAsync(int guildId, string perkId, ulong buyerSteamId)
	{
		if (!_registeredPerks.TryGetValue(perkId, out var perk))
			return (false, 0, LocalizedMessage.Simple("k4.guild.error.perk_not_found"));

		var currentLevel = await database.GetPerkLevelAsync(guildId, perkId);
		var nextLevel = currentLevel + 1;

		// Check if already at max level
		if (perk.PerkType == GuildPerkType.Purchasable && currentLevel >= 1)
			return (false, currentLevel, LocalizedMessage.Simple("k4.guild.error.perk_already_purchased"));

		if (perk.PerkType == GuildPerkType.Upgradeable && currentLevel >= perk.MaxLevel)
			return (false, currentLevel, LocalizedMessage.Simple("k4.guild.error.perk_max_level"));

		// Calculate cost
		var cost = perk.GetCostForLevel(nextLevel);

		// Check and deduct balance
		if (plugin.EconomyAPI != null && cost > 0)
		{
			var balance = plugin.EconomyAPI.GetPlayerBalance(buyerSteamId, plugin.Guild.WalletKind);
			if (balance < cost)
				return (false, currentLevel, LocalizedMessage.WithArgs("k4.guild.error.not_enough_currency", cost, balance));

			plugin.EconomyAPI.SubtractPlayerBalance(buyerSteamId, plugin.Guild.WalletKind, (int)cost);
		}

		// Upgrade the perk
		await database.SetPerkLevelAsync(guildId, perkId, nextLevel);

		var message = perk.PerkType == GuildPerkType.Purchasable
			? LocalizedMessage.WithArgs("k4.guild.success.perk_purchased", perk.Name, nextLevel)
			: LocalizedMessage.WithArgs("k4.guild.success.perk_upgraded", perk.Name, nextLevel);

		return (true, nextLevel, message);
	}

	public async Task<(bool Success, bool NewState)> TogglePerkAsync(int guildId, string perkId)
	{
		if (!_registeredPerks.TryGetValue(perkId, out var perk))
			return (false, false);

		// Only Purchasable perks can be toggled
		if (perk.PerkType != GuildPerkType.Purchasable)
			return (false, false);

		// TogglePerkEnabledAsync returns (false, false) if the perk hasn't been purchased yet
		return await database.TogglePerkEnabledAsync(guildId, perkId);
	}
}
