using K4Guilds.Shared;

namespace K4_Guilds.Commands.Handlers;

public static class ChatHandler
{
	public static Task<string?> ExecuteAsync(CommandContext ctx)
	{
		var chatMessage = string.Join(" ", ctx.Args).Trim();
		if (string.IsNullOrEmpty(chatMessage))
			return Task.FromResult<string?>(ctx.Format("k4.command.usage.gc"));

		var guild = ctx.Guild!;
		var ranks = ctx.GuildService.GetRanks();

		var senderMember = ctx.GuildService.FindMember(guild, ctx.Player.SteamID);
		var senderRank = senderMember != null ? ctx.GuildService.GetRankByPriority(senderMember.RankPriority) : null;
		if (senderRank == null || !senderRank.HasPermission(GuildPermission.Chat))
			return Task.FromResult<string?>(ctx.Format("k4.guild.error.no_permission_chat"));

		var senderName = ctx.Player.Controller.PlayerName;
		var guildTag = guild.Tag;

		var chatPermissionPriorities = new HashSet<int>();
		foreach (var rank in ranks)
		{
			if (rank.HasPermission(GuildPermission.Chat))
				chatPermissionPriorities.Add(rank.Priority);
		}

		var eligibleMemberSteamIds = new HashSet<ulong>();
		foreach (var member in guild.Members)
		{
			if (chatPermissionPriorities.Contains(member.RankPriority))
				eligibleMemberSteamIds.Add(member.SteamId);
		}

		Plugin.Core.Scheduler.NextWorldUpdate(() =>
		{
			foreach (var player in Plugin.Core.PlayerManager.GetAllPlayers())
			{
				if (player.IsValid && eligibleMemberSteamIds.Contains(player.SteamID))
				{
					var localizer = Plugin.Core.Translation.GetPlayerLocalizer(player);
					player.SendChat(localizer["k4.chat.format", guildTag, senderName, chatMessage]);
				}
			}
		});

		return Task.FromResult<string?>(null);
	}
}
