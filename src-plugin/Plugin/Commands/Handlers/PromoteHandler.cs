namespace K4_Guilds.Commands.Handlers;

public static class PromoteHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		if (ctx.Args.Length < 1)
			return ctx.Format("k4.command.usage.promote");

		var targetInput = string.Join(" ", ctx.Args);
		var targetResult = ctx.FindGuildMember(targetInput, ctx.Guild!.Members);

		if (!targetResult.Success)
			return ctx.Format(targetResult.ErrorKey!);

		var (_, message) = await ctx.GuildService.PromoteMemberAsync(ctx.Guild!, ctx.Player, targetResult.SteamId);
		return ctx.Format(message);
	}
}
