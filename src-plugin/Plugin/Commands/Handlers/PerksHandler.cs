using K4_Guilds.Services;
using K4Guilds.Shared;

namespace K4_Guilds.Commands.Handlers;

public static class PerksHandler
{
	public static async Task<string?> ExecuteAsync(CommandContext ctx)
	{
		var guild = ctx.Guild!;
		var perkService = ctx.Plugin.GetPerkService();
		var perks = perkService.GetRegisteredPerks();
		if (perks.Count == 0)
			return ctx.Format("k4.guild.error.no_perks");

		if (ctx.Args.Length > 0)
		{
			var action = ctx.Args[0].ToLowerInvariant();

			if (action is "buy" or "upgrade" or "b" or "u")
			{
				if (ctx.Args.Length < 2)
					return ctx.Format("k4.command.usage.perk_buy");

				if (!ctx.GuildService.HasPermission(guild, ctx.Player.SteamID, GuildPermission.ManagePerks))
					return ctx.Format("k4.guild.error.no_permission");

				var perkInput = ctx.Args[1].ToLowerInvariant();
				var perk = FindPerk(perks, perkInput);
				if (perk == null)
					return ctx.Format("k4.guild.error.perk_not_found", perkInput);

				var (_, _, message) = await perkService.PurchaseOrUpgradePerkAsync(guild.Id, perk.Id, ctx.Player.SteamID);

				return message.Format(ctx.Localizer);
			}

			if (action is "toggle" or "t")
			{
				if (ctx.Args.Length < 2)
					return ctx.Format("k4.command.usage.perk_toggle");

				if (!ctx.GuildService.HasPermission(guild, ctx.Player.SteamID, GuildPermission.ManagePerks))
					return ctx.Format("k4.guild.error.no_permission");

				var perkInput = ctx.Args[1].ToLowerInvariant();
				var perk = FindPerk(perks, perkInput);
				if (perk == null)
					return ctx.Format("k4.guild.error.perk_not_found", perkInput);

				if (perk.PerkType != GuildPerkType.Purchasable)
					return ctx.Format("k4.guild.error.perk_not_toggleable");

				var currentLevel = perkService.GetPerkLevel(guild.EnabledPerks, perk.Id);
				if (currentLevel == 0)
					return ctx.Format("k4.guild.error.perk_not_purchased");

				var (success, newState) = await perkService.TogglePerkAsync(guild.Id, perk.Id);
				if (!success)
					return ctx.Format("k4.guild.error.perk_toggle_failed");

				return newState
					? ctx.Format("k4.guild.success.perk_enabled", perk.Name)
					: ctx.Format("k4.guild.success.perk_disabled", perk.Name);
			}

			if (action is "info" or "i")
			{
				if (ctx.Args.Length < 2)
					return ctx.Format("k4.command.usage.perk_info");

				var perkInput = ctx.Args[1].ToLowerInvariant();
				var perk = FindPerk(perks, perkInput);
				if (perk == null)
					return ctx.Format("k4.guild.error.perk_not_found", perkInput);

				SendPerkInfo(ctx, perkService, perk);
				return null;
			}
		}

		await SendPerkList(ctx, perkService, guild.Id, perks);
		return null;
	}

	private static IGuildPerk? FindPerk(IReadOnlyList<IGuildPerk> perks, string input)
	{
		return perks.FirstOrDefault(p =>
			p.Id.Equals(input, StringComparison.OrdinalIgnoreCase) ||
			p.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
	}

	private static Task SendPerkList(CommandContext ctx, PerkService perkService, int guildId, IReadOnlyList<IGuildPerk> perks)
	{
		var guildPerks = ctx.Guild!.EnabledPerks;

		Plugin.Core.Scheduler.NextWorldUpdate(() =>
			ctx.Player.SendChat(ctx.Format("k4.guild.perks.header")));

		foreach (var perk in perks)
		{
			var level = perkService.GetPerkLevel(guildPerks, perk.Id);
			var isActive = perkService.IsPerkActive(guildPerks, perk.Id);
			var typeIcon = perk.PerkType == GuildPerkType.Purchasable ? "●" : "▲";

			string message;
			if (level == 0)
			{
				var cost = perk.GetCostForLevel(1);
				message = ctx.Format("k4.guild.perks.item_not_purchased", typeIcon, perk.Name, perk.Id, cost);
			}
			else if (perk.PerkType == GuildPerkType.Purchasable)
			{
				var statusKey = isActive ? "k4.guild.perks.status_on" : "k4.guild.perks.status_off";
				message = ctx.Format("k4.guild.perks.item_purchasable", typeIcon, perk.Name, perk.Id, ctx.Localizer[statusKey]);
			}
			else
			{
				if (level >= perk.MaxLevel)
					message = ctx.Format("k4.guild.perks.item_maxed", typeIcon, perk.Name, perk.Id, level);
				else
				{
					var nextCost = perk.GetCostForLevel(level + 1);
					message = ctx.Format("k4.guild.perks.item_upgradeable", typeIcon, perk.Name, perk.Id, level, perk.MaxLevel, nextCost);
				}
			}

			Plugin.Core.Scheduler.NextWorldUpdate(() => ctx.Player.SendChat(message));
		}

		Plugin.Core.Scheduler.NextWorldUpdate(() =>
			ctx.Player.SendChat(ctx.Format("k4.guild.perks.usage_hint")));

		return Task.CompletedTask;
	}

	private static void SendPerkInfo(CommandContext ctx, PerkService perkService, IGuildPerk perk)
	{
		var guildPerks = ctx.Guild!.EnabledPerks;
		var level = perkService.GetPerkLevel(guildPerks, perk.Id);
		var isActive = perkService.IsPerkActive(guildPerks, perk.Id);

		var lines = new List<string>
		{
			ctx.Format("k4.guild.perks.info.header", perk.Name, perk.Id),
			ctx.Format("k4.guild.perks.info.description", perk.Description)
		};

		if (perk.PerkType == GuildPerkType.Purchasable)
		{
			if (level > 0)
			{
				var statusKey = isActive ? "k4.guild.perks.status_on" : "k4.guild.perks.status_off";
				lines.Add(ctx.Format("k4.guild.perks.info.status", ctx.Localizer[statusKey]));
			}
			else
			{
				lines.Add(ctx.Format("k4.guild.perks.info.cost", perk.BaseCost));
			}
		}
		else
		{
			lines.Add(ctx.Format("k4.guild.perks.info.level", level, perk.MaxLevel));
			if (level < perk.MaxLevel)
				lines.Add(ctx.Format("k4.guild.perks.info.next_cost", perk.GetCostForLevel(level + 1)));
		}

		ctx.ReplyLines([.. lines]);
	}
}
