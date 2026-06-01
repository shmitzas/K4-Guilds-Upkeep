using K4_Guilds.Config;

namespace K4_Guilds.Commands.Handlers;

public static class RenameHandler
{
	public static async Task<string> ExecuteAsync(CommandContext ctx, RenameCommandSettings settings)
	{
		if (ctx.Args.Length < 1)
			return ctx.Format("k4.command.usage.rename");

		var newName = string.Join(" ", ctx.Args).Trim();

		var (_, message) = await ctx.GuildService.RenameGuildAsync(ctx.Guild!, ctx.Player, newName, settings.Cost);
		return ctx.Format(message);
	}
}
