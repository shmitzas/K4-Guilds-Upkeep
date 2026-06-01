using K4_Guilds.Database;
using K4_Guilds.Database.Models;
using K4Guilds.Shared;
using SwiftlyS2.Shared.Players;

namespace K4_Guilds.Services;

public sealed class UpgradeService(Plugin plugin, DatabaseService database, GuildService guildService)
{
	public static int GetUpgradeLevel(Guild guild, GuildUpgradeType upgradeType)
	{
		var upgrade = guild.Upgrades.FirstOrDefault(u => u.GetUpgradeType() == upgradeType);
		return upgrade?.Level ?? 0;
	}

	public async Task<int> GetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType)
		=> await database.GetUpgradeLevelAsync(guildId, upgradeType);

	public double CalculateBankInterestPercent(Guild guild)
	{
		var level = GetUpgradeLevel(guild, GuildUpgradeType.BankInterest);
		return level * plugin.Upgrades.BankInterest.InterestPerLevel;
	}

	public async Task<(bool Success, LocalizedMessage Message)> PurchaseUpgradeAsync(Guild guild, IPlayer player, GuildUpgradeType upgradeType)
	{
		if (!guildService.HasPermission(guild, player.SteamID, GuildPermission.Upgrade))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_upgrade"));

		var settings = plugin.Upgrades[upgradeType];
		if (!settings.Enabled)
			return (false, LocalizedMessage.Simple("k4.upgrade.error.disabled"));

		var currentLevel = GetUpgradeLevel(guild, upgradeType);
		if (currentLevel >= settings.MaxLevel)
			return (false, LocalizedMessage.Simple("k4.upgrade.error.max_level"));

		var cost = settings.GetCost(currentLevel);

		var (success, _) = await database.AtomicSubtractFromBankAsync(guild.Id, cost);
		if (!success)
			return (false, LocalizedMessage.WithArgs("k4.upgrade.error.insufficient_funds", cost, guild.BankBalance));

		await database.SetUpgradeLevelAsync(guild.Id, upgradeType, currentLevel + 1);
		guildService.InvalidateGuildCache(guild.Id);

		return (true, LocalizedMessage.WithArgs("k4.upgrade.success.purchased", upgradeType.ToString(), currentLevel + 1));
	}

	public async Task<(bool Success, LocalizedMessage Message)> DepositToBankAsync(Guild guild, IPlayer player, long amount)
	{
		if (amount <= 0)
			return (false, LocalizedMessage.Simple("k4.bank.error.invalid_amount"));

		if (plugin.EconomyAPI == null)
			return (false, LocalizedMessage.Simple("k4.economy.error.unavailable"));

		if (!plugin.EconomyAPI.HasSufficientFunds(player.SteamID, plugin.Guild.WalletKind, (int)amount))
			return (false, LocalizedMessage.Simple("k4.bank.error.insufficient_balance"));

		var maxCapacity = guildService.CalculateMaxBankCapacity(guild);

		plugin.EconomyAPI.SubtractPlayerBalance(player.SteamID, plugin.Guild.WalletKind, (int)amount);

		var (success, _) = await database.AtomicAddToBankAsync(guild.Id, amount, maxCapacity);
		if (!success)
		{
			plugin.EconomyAPI.AddPlayerBalance(player.SteamID, plugin.Guild.WalletKind, (int)amount);
			return (false, LocalizedMessage.WithArgs("k4.bank.error.exceed_capacity", maxCapacity, guild.BankBalance));
		}

		guildService.InvalidateGuildCache(guild.Id);
		return (true, LocalizedMessage.WithArgs("k4.bank.success.deposited", amount));
	}

	public async Task<(bool Success, LocalizedMessage Message)> WithdrawFromBankAsync(Guild guild, IPlayer player, long amount)
	{
		if (amount <= 0)
			return (false, LocalizedMessage.Simple("k4.bank.error.invalid_amount"));

		if (!guildService.HasPermission(guild, player.SteamID, GuildPermission.Withdraw))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_withdraw"));

		if (plugin.EconomyAPI == null)
			return (false, LocalizedMessage.Simple("k4.economy.error.unavailable"));

		var (success, currentBalance) = await database.AtomicSubtractFromBankAsync(guild.Id, amount);
		if (!success)
			return (false, LocalizedMessage.WithArgs("k4.bank.error.insufficient_bank", currentBalance));

		plugin.EconomyAPI.AddPlayerBalance(player.SteamID, plugin.Guild.WalletKind, (int)amount);
		guildService.InvalidateGuildCache(guild.Id);

		return (true, LocalizedMessage.WithArgs("k4.bank.success.withdrawn", amount));
	}
}
