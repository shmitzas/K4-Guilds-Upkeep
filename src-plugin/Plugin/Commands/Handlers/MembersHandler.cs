namespace K4_Guilds.Commands.Handlers;

public static class MembersHandler
{
	public static Task<string?> ExecuteAsync(CommandContext ctx)
	{
		var guild = ctx.Guild!;
		var ranks = ctx.GuildService.GetRanks();

		var rankByPriority = new Dictionary<int, string>(ranks.Count);
		foreach (var rank in ranks)
			rankByPriority[rank.Priority] = ctx.Localizer[rank.Name];

		var lines = new string[guild.Members.Count + 2];
		lines[0] = ctx.Localizer["k4.command.members.header"];

		for (int i = 0; i < guild.Members.Count; i++)
		{
			var member = guild.Members[i];
			var rankName = rankByPriority.TryGetValue(member.RankPriority, out var name) ? name : "?";
			lines[i + 1] = ctx.Localizer["k4.command.members.item", member.PlayerName, rankName];
		}

		lines[^1] = ctx.Localizer["k4.command.members.footer"];

		ctx.ReplyLines(lines);
		return Task.FromResult<string?>(null);
	}
}
