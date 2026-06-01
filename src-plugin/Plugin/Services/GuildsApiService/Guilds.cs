using K4_Guilds.Models;
using K4Guilds.Shared;

namespace K4_Guilds.Services;

internal partial class GuildsApiService
{
	public async Task<IGuild?> GetGuildAsync(int guildId)
	{
		var guild = await plugin.GetGuildService().GetGuildAsync(guildId);
		return guild != null ? new GuildWrapper(guild, plugin.GetGuildService()) : null;
	}

	public async Task<IGuild?> GetGuildByNameAsync(string name)
	{
		var guild = await plugin.GetGuildService().GetGuildByNameAsync(name);
		return guild != null ? new GuildWrapper(guild, plugin.GetGuildService()) : null;
	}

	public async Task<IGuild?> GetPlayerGuildAsync(ulong steamId)
	{
		var guild = await plugin.GetGuildService().GetPlayerGuildAsync(steamId);
		return guild != null ? new GuildWrapper(guild, plugin.GetGuildService()) : null;
	}

	public async Task<IGuildMember?> GetGuildMemberAsync(ulong steamId)
	{
		var guildService = plugin.GetGuildService();
		var guild = await guildService.GetPlayerGuildAsync(steamId);
		if (guild == null) return null;

		var member = guildService.FindMember(guild, steamId);
		if (member == null) return null;

		var rank = guildService.GetRankByPriority(member.RankPriority);
		return new MemberWrapper(member, rank);
	}

	public async Task<bool> IsInGuildAsync(ulong steamId)
		=> await plugin.GetGuildService().GetPlayerGuildAsync(steamId) != null;

	public async Task<bool> HasPermissionAsync(ulong steamId, GuildPermission permission)
		=> await plugin.GetGuildService().HasPermissionAsync(steamId, permission);

	public async Task<IReadOnlyList<IGuild>> GetAllGuildsAsync(int page = 0, int pageSize = 20)
	{
		var guildService = plugin.GetGuildService();
		var guilds = await plugin.GetDatabase().GetAllGuildsAsync(page, pageSize);
		return guilds.Select(g => (IGuild)new GuildWrapper(g, guildService)).ToArray();
	}
}
