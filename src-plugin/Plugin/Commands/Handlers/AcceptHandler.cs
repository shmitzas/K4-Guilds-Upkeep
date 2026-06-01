namespace K4_Guilds.Commands.Handlers;

public static class AcceptHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		var (_, message) = await ctx.GuildService.AcceptInviteAsync(ctx.Player);
		return ctx.Format(message);
	}
}
