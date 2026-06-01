using K4_Guilds.Services;

namespace K4_Guilds.Commands.Handlers;

public static class InviteHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx)
	{
		if (ctx.Args.Length < 1)
			return ctx.Format("k4.command.usage.invite");

		var targetInput = string.Join(" ", ctx.Args);
		var targetResult = ctx.FindOnlineTarget(targetInput);

		if (!targetResult.Success)
			return ctx.Format(targetResult.ErrorKey!);

		var target = targetResult.OnlinePlayer!;
		var (success, message) = await ctx.GuildService.InvitePlayerAsync(ctx.Guild!, ctx.Player, target);
		if (success)
		{
			LocalizedMessage.WithArgs("k4.chat.invite_received", ctx.Player.Controller.PlayerName).Send(target);
			LocalizedMessage.Simple("k4.chat.invite_usage").Send(target);
		}
		return ctx.Format(message);
	}
}
