using K4_Guilds.Config;
using K4_Guilds.Database.Models;
using K4Guilds.Shared;
using SwiftlyS2.Shared.Players;

namespace K4_Guilds.Services;

public partial class GuildService
{
	public IReadOnlyList<GuildRankSettings> GetRanks()
		=> plugin.Guild.GuildRanks.OrderByDescending(r => r.Priority).ToList();

	public GuildRankSettings? GetRankByPriority(int priority)
		=> plugin.Guild.GuildRanks.FirstOrDefault(r => r.Priority == priority);

	public GuildRankSettings? GetDefaultRank()
		=> plugin.Guild.GuildRanks.FirstOrDefault(r => r.IsDefault);

	public GuildRankSettings? GetLeaderRank()
		=> plugin.Guild.GuildRanks.FirstOrDefault(r => r.Permissions == -1)
		   ?? plugin.Guild.GuildRanks.OrderByDescending(r => r.Priority).FirstOrDefault();

	public async Task<(bool Success, LocalizedMessage Message)> PromoteMemberAsync(Guild guild, IPlayer promoter, ulong targetSteamId)
	{
		var ranks = GetRanks();

		var promoterMember = FindMember(guild, promoter.SteamID);
		var promoterRank = promoterMember != null ? GetRankByPriority(promoterMember.RankPriority) : null;

		if (promoterRank == null || !promoterRank.HasPermission(GuildPermission.Promote))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_promote"));

		var targetMember = FindMember(guild, targetSteamId);
		if (targetMember == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_not_in_guild"));

		var targetRank = GetRankByPriority(targetMember.RankPriority);
		if (targetRank == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.invalid_rank"));

		var nextRank = ranks
			.Where(r => r.Priority > targetRank.Priority && r.Priority < promoterRank.Priority)
			.OrderBy(r => r.Priority)
			.FirstOrDefault();

		if (nextRank == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.no_higher_rank"));

		await database.UpdateMemberRankAsync(targetSteamId, nextRank.Priority);
		InvalidateGuildCache(guild.Id);

		targetMember.RankPriority = nextRank.Priority;
		plugin.OnMemberPromoted(guild, targetMember, null);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.promoted", targetMember.PlayerName, new LocalizedArg(nextRank.Name)));
	}

	public async Task<(bool Success, LocalizedMessage Message)> DemoteMemberAsync(Guild guild, IPlayer demoter, ulong targetSteamId)
	{
		var ranks = GetRanks();

		var demoterMember = FindMember(guild, demoter.SteamID);
		var demoterRank = demoterMember != null ? GetRankByPriority(demoterMember.RankPriority) : null;

		if (demoterRank == null || !demoterRank.HasPermission(GuildPermission.Demote))
			return (false, LocalizedMessage.Simple("k4.guild.error.no_permission_demote"));

		var targetMember = FindMember(guild, targetSteamId);
		if (targetMember == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_not_in_guild"));

		var targetRank = GetRankByPriority(targetMember.RankPriority);
		if (targetRank == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.invalid_rank"));

		if (targetRank.Priority >= demoterRank.Priority)
			return (false, LocalizedMessage.Simple("k4.guild.error.target_higher_rank"));

		var prevRank = ranks
			.Where(r => r.Priority < targetRank.Priority)
			.OrderByDescending(r => r.Priority)
			.FirstOrDefault();

		if (prevRank == null)
			return (false, LocalizedMessage.Simple("k4.guild.error.no_lower_rank"));

		await database.UpdateMemberRankAsync(targetSteamId, prevRank.Priority);
		InvalidateGuildCache(guild.Id);

		targetMember.RankPriority = prevRank.Priority;
		plugin.OnMemberDemoted(guild, targetMember, null);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.demoted", targetMember.PlayerName, new LocalizedArg(prevRank.Name)));
	}
}
