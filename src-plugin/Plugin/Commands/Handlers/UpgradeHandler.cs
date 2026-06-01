using K4_Guilds.Services;
using K4Guilds.Shared;

namespace K4_Guilds.Commands.Handlers;

public static class UpgradeHandler
{
	public static async Task<string?> ExecuteAsync(CommandContext ctx)
	{
		if (ctx.Args.Length < 1)
		{
			ShowUpgradeList(ctx);
			return null;
		}

		var upgradeType = ctx.Args[0].ToLowerInvariant() switch
		{
			"slots" or "slot" => GuildUpgradeType.Slots,
			"bank" => GuildUpgradeType.BankCapacity,
			"xp" => GuildUpgradeType.XPBoost,
			"interest" => GuildUpgradeType.BankInterest,
			_ => (GuildUpgradeType?)null
		};

		if (upgradeType == null)
			return ctx.Format("k4.command.error.invalid_upgrade");

		var (_, message) = await ctx.UpgradeService.PurchaseUpgradeAsync(ctx.Guild!, ctx.Player, upgradeType.Value);
		return ctx.Format(message);
	}

	private static void ShowUpgradeList(CommandContext ctx)
	{
		var guild = ctx.Guild!;
		var upgrades = ctx.Plugin.Upgrades;

		var lines = new List<string> { ctx.Localizer["k4.command.upgrades.header"] };

		AddUpgradeLine(lines, ctx, guild, GuildUpgradeType.Slots, upgrades.SlotUpgrade, "Slots", "slots");
		AddUpgradeLine(lines, ctx, guild, GuildUpgradeType.BankCapacity, upgrades.BankCapacity, "Bank Capacity", "bank");
		AddUpgradeLine(lines, ctx, guild, GuildUpgradeType.XPBoost, upgrades.XPBoost, "XP Boost", "xp");
		AddUpgradeLine(lines, ctx, guild, GuildUpgradeType.BankInterest, upgrades.BankInterest, "Bank Interest", "interest");

		lines.Add(ctx.Localizer["k4.command.upgrades.usage_hint"]);
		lines.Add(ctx.Localizer["k4.command.upgrades.footer"]);

		ctx.ReplyLines([.. lines]);
	}

	private static void AddUpgradeLine(List<string> lines, CommandContext ctx, Database.Models.Guild guild, GuildUpgradeType type, Config.IUpgradeSettings settings, string name, string cmd)
	{
		if (!settings.Enabled) return;

		var level = UpgradeService.GetUpgradeLevel(guild, type);

		if (level >= settings.MaxLevel)
		{
			lines.Add(ctx.Localizer["k4.command.upgrades.item_maxed", name, cmd, level]);
		}
		else
		{
			var nextCost = settings.GetCost(level);
			lines.Add(ctx.Localizer["k4.command.upgrades.item", name, cmd, level, settings.MaxLevel, nextCost]);
		}
	}
}
