namespace K4_Guilds.Commands.Handlers;

public static class TopHandler
{
	private const int DefaultLimit = 5;
	private const int MaxLimit = 25;

	public static async Task<string?> ExecuteAsync(CommandContext ctx)
	{
		var limit = DefaultLimit;
		if (ctx.Args.Length > 0 && int.TryParse(ctx.Args[0], out var parsed))
			limit = Math.Clamp(parsed, 1, MaxLimit);

		var entries = await ctx.Plugin.GetDatabase().GetTopGuildsByKillsAsync(limit);

		if (entries.Count == 0)
		{
			ctx.Reply("k4.command.top.no_stats");
			return null;
		}

		var lines = new string[entries.Count + 2];
		lines[0] = ctx.Localizer["k4.command.top.header"];

		for (int i = 0; i < entries.Count; i++)
		{
			var e = entries[i];
			lines[i + 1] = ctx.Localizer["k4.command.top.item", i + 1, e.Name, e.Tag, e.Kills, e.Deaths, e.Headshots, e.Assists];
		}

		lines[^1] = ctx.Localizer["k4.command.top.footer"];
		ctx.ReplyLines(lines);
		return null;
	}
}
