namespace K4_Guilds.Commands.Handlers;

public static class DepositHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		if (ctx.Args.Length < 1 || !long.TryParse(ctx.Args[0], out var amount))
			return ctx.Format("k4.command.usage.deposit");

		var (_, message) = await ctx.UpgradeService.DepositToBankAsync(ctx.Guild!, ctx.Player, amount);
		return ctx.Format(message);
	}
}
