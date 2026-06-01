namespace K4_Guilds.Commands.Handlers;

public static class DeclineHandler
{
	public static string Execute(CommandContext ctx)
	{
		var (_, message) = ctx.GuildService.DeclineInviteAsync(ctx.Player);
		return ctx.Format(message);
	}
}
