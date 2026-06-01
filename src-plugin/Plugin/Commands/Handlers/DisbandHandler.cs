namespace K4_Guilds.Commands.Handlers;

public static class DisbandHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		var (_, message) = await ctx.GuildService.DisbandGuildAsync(ctx.Guild!, ctx.Player);
		return ctx.Format(message);
	}
}
