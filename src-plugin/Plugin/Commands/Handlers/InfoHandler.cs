namespace K4_Guilds.Commands.Handlers;

public static class InfoHandler
{
	public static Task<string?> ExecuteAsync(CommandContext ctx)
	{
		var guild = ctx.Guild!;
		var maxSlots = ctx.GuildService.CalculateMaxSlots(guild);
		var maxBank = ctx.GuildService.CalculateMaxBankCapacity(guild);
		var xpBoost = ctx.GuildService.CalculateXPBoostPercent(guild);

		ctx.ReplyLines(
			ctx.Localizer["k4.command.info.header", guild.Name, guild.Tag],
			ctx.Localizer["k4.command.info.members", guild.Members.Count, maxSlots],
			ctx.Localizer["k4.command.info.bank", guild.BankBalance, maxBank],
			ctx.Localizer["k4.command.info.xpboost", xpBoost],
			ctx.Localizer["k4.command.info.footer"]
		);
		return Task.FromResult<string?>(null);
	}
}
