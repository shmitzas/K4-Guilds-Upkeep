namespace K4_Guilds.Commands.Handlers;

public static class LeaveHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		var (_, message) = await ctx.GuildService.LeaveGuildAsync(ctx.Guild!, ctx.Player);
		return ctx.Format(message);
	}
}
