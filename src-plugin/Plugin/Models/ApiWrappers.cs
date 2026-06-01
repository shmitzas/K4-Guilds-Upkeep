using K4_Guilds.Config;
using K4_Guilds.Database.Models;
using K4_Guilds.Services;
using K4Guilds.Shared;
using SwiftlyS2.Shared.Players;

namespace K4_Guilds.Models;

/// <summary>Wrapper for Guild to implement IGuild interface</summary>
internal sealed class GuildWrapper : IGuild
{
	private readonly Guild _guild;
	private readonly GuildService _guildService;
	private readonly Dictionary<int, GuildRankSettings> _rankByPriority;

	public GuildWrapper(Guild guild, GuildService guildService)
	{
		_guild = guild;
		_guildService = guildService;
		var ranks = guildService.GetRanks();
		_rankByPriority = new Dictionary<int, GuildRankSettings>(ranks.Count);
		foreach (var rank in ranks)
			_rankByPriority[rank.Priority] = rank;
	}

	public int Id => _guild.Id;
	public string Name => _guild.Name;
	public string Tag => _guild.Tag;
	public ulong LeaderSteamId => _guild.LeaderSteamId;
	public int MemberCount => _guild.Members.Count;
	public int MaxSlots => _guildService.CalculateMaxSlots(_guild);
	public long BankBalance => _guild.BankBalance;
	public long MaxBankCapacity => _guildService.CalculateMaxBankCapacity(_guild);
	public int XPBoostPercent => _guildService.CalculateXPBoostPercent(_guild);
	public DateTime CreatedAt => _guild.CreatedAt;

	public IReadOnlyList<IGuildMember> Members
	{
		get
		{
			var members = new IGuildMember[_guild.Members.Count];
			for (int i = 0; i < _guild.Members.Count; i++)
			{
				var m = _guild.Members[i];
				_rankByPriority.TryGetValue(m.RankPriority, out var rank);
				members[i] = new MemberWrapper(m, rank);
			}
			return members;
		}
	}

	public IReadOnlyList<IGuildRank> Ranks
	{
		get
		{
			var configRanks = _guildService.GetRanks();
			var ranks = new IGuildRank[configRanks.Count];
			for (int i = 0; i < configRanks.Count; i++)
				ranks[i] = new RankWrapper(configRanks[i]);
			return ranks;
		}
	}
}

/// <summary>Wrapper for GuildMember to implement IGuildMember interface</summary>
internal sealed class MemberWrapper(GuildMember member, GuildRankSettings? rank) : IGuildMember
{
	public ulong SteamId => member.SteamId;
	public string PlayerName => member.PlayerName;
	public int RankId => member.RankPriority; // Using priority as ID for API compatibility
	public string RankName => rank?.Name ?? "Unknown";
	public GuildPermission Permissions => rank?.GetPermissions() ?? GuildPermission.None;
	public DateTime JoinedAt => member.JoinedAt;
}

/// <summary>Wrapper for GuildRankSettings to implement IGuildRank interface</summary>
internal sealed class RankWrapper(GuildRankSettings rank) : IGuildRank
{
	public int Id => rank.Priority; // Using priority as ID for API compatibility
	public string Name => rank.Name;
	public GuildPermission Permissions => rank.GetPermissions();
	public int Priority => rank.Priority;
	public bool IsDefault => rank.IsDefault;
}

/// <summary>Wrapper for perk context to implement IPerkContext interface</summary>
internal sealed class PerkContext(IGuild guild, IGuildMember member, IPlayer player, int perkLevel) : IPerkContext
{
	public IGuild Guild => guild;
	public IGuildMember Member => member;
	public IPlayer Player => player;
	public int PerkLevel => perkLevel;
}