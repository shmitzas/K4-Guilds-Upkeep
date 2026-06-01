namespace K4_Guilds.Commands.Handlers;

public static class WithdrawHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		if (ctx.Args.Length < 1 || !long.TryParse(ctx.Args[0], out var amount))
			return ctx.Format("k4.command.usage.withdraw");

		var (_, message) = await ctx.UpgradeService.WithdrawFromBankAsync(ctx.Guild!, ctx.Player, amount);
		return ctx.Format(message);
	}
}
