using K4_Guilds.Database.Models;
using K4Guilds.Shared;
using SwiftlyS2.Shared.Players;

namespace K4_Guilds.Services;

public partial class GuildService
{
	public async Task<(bool Success, LocalizedMessage Message)> InvitePlayerAsync(Guild guild, IPlayer inviter, IPlayer invitee)
	{
		if (_inviteService == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.service_unavailable"));

		var member = FindMember(guild, inviter.SteamID);
		var rank = member != null ? GetRankByPriority(member.RankPriority) : null;
		if (rank == null || !rank.HasPermission(GuildPermission.Invite))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_invite"));

		var inviteeGuild = await GetPlayerGuildAsync(invitee.SteamID);
		if (inviteeGuild != null)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_in_guild"));

		if (_inviteService.HasAnyPendingInvite(invitee.SteamID))
			return (false, LocalizedMessage.Simple("k4.guild.error.target_has_invite"));

		var maxSlots = CalculateMaxSlots(guild);
		if (guild.Members.Count >= maxSlots)
			return (false, LocalizedMessage.Simple("k4.guild.error.guild_full"));

		_inviteService.CreateInvite(guild.Id, inviter.SteamID, invitee.SteamID);
		plugin.OnInviteSent(guild, inviter.SteamID, invitee.SteamID);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.invite_sent", invitee.Controller.PlayerName));
	}

	public async Task<(bool Success, LocalizedMessage Message)> AcceptInviteAsync(IPlayer player)
	{
		if (_inviteService == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.service_unavailable"));

		var pendingInvite = _inviteService.GetPendingInvite(player.SteamID);
		if (pendingInvite == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.no_pending_invite"));

		var guildId = pendingInvite.Value.GuildId;
		var guild = await GetGuildAsync(guildId);
		if (guild == null)
		{
			_inviteService.DeclineInvite(player.SteamID);
			return (false, LocalizedMessage.Simple("k4.guild.error.guild_not_exists"));
		}

		var currentGuild = await GetPlayerGuildAsync(player.SteamID);
		if (currentGuild != null)
		{
			_inviteService.DeclineInvite(player.SteamID);
			return (false, LocalizedMessage.Simple("k4.guild.error.already_in_guild"));
		}

		var maxSlots = CalculateMaxSlots(guild);
		if (guild.Members.Count >= maxSlots)
		{
			_inviteService.DeclineInvite(player.SteamID);
			return (false, LocalizedMessage.Simple("k4.guild.error.target_guild_full"));
		}

		var defaultRank = GetDefaultRank();
		if (defaultRank == null)
		{
			_inviteService.DeclineInvite(player.SteamID);
			return (false, LocalizedMessage.Simple("k4.guild.error.no_default_rank"));
		}

		_inviteService.AcceptInvite(player.SteamID, guildId);
		await database.AddMemberAsync(guildId, player.SteamID, player.Controller.PlayerName, defaultRank.Priority);

		InvalidateMemberCache(player.SteamID, guildId);

		var memberInfo = await database.GetGuildMemberAsync(player.SteamID);
		plugin.OnMemberJoined(guild, memberInfo!, player);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.joined", guild.Name));
	}

	public (bool Success, LocalizedMessage Message) DeclineInviteAsync(IPlayer player)
	{
		if (_inviteService == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.service_unavailable"));

		if (!_inviteService.HasAnyPendingInvite(player.SteamID))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_pending_invite"));

		_inviteService.DeclineInvite(player.SteamID);
		return (true, LocalizedMessage.Simple("k4.guild.success.invite_declined"));
	}

	public async Task<(bool Success, LocalizedMessage Message)> LeaveGuildAsync(Guild guild, IPlayer player)
	{
		if (guild.LeaderSteamId == player.SteamID)
			return (false, LocalizedMessage.Simple("k4.guild.error.leader_cannot_leave"));

		var member = FindMember(guild, player.SteamID);
		await database.RemoveMemberAsync(player.SteamID);

		InvalidateMemberCache(player.SteamID, guild.Id);
		plugin.OnMemberLeft(guild, member!, player);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.left", guild.Name));
	}

	public async Task<(bool Success, LocalizedMessage Message)> KickMemberAsync(Guild guild, IPlayer kicker, ulong targetSteamId)
	{
		var kickerMember = FindMember(guild, kicker.SteamID);
		var kickerRank = kickerMember != null ? GetRankByPriority(kickerMember.RankPriority) : null;
		if (kickerRank == null || !kickerRank.HasPermission(GuildPermission.Kick))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_kick"));

		var targetMember = FindMember(guild, targetSteamId);
		if (targetMember == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_not_in_guild"));

		if (targetSteamId == guild.LeaderSteamId)
			return (false, LocalizedMessage.Simple("k4.guild.error.cannot_kick_leader"));

		var targetRank = GetRankByPriority(targetMember.RankPriority);
		if (targetRank != null && targetRank.Priority >= kickerRank.Priority)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_higher_rank"));

		await database.RemoveMemberAsync(targetSteamId);

		InvalidateMemberCache(targetSteamId, guild.Id);
		plugin.OnMemberKicked(guild, targetMember, null);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.kicked", targetMember.PlayerName));
	}
}
