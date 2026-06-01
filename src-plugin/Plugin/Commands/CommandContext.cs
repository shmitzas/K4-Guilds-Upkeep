using K4_Guilds.Database.Models;
using K4_Guilds.Services;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using SwiftlyS2.Shared.Translation;

namespace K4_Guilds.Commands;

/// <summary>Result of a target search operation</summary>
public sealed class TargetResult
{
	public bool Success { get; init; }
	public IPlayer? OnlinePlayer { get; init; }
	public GuildMember? GuildMember { get; init; }
	public ulong SteamId { get; init; }
	public string? ErrorKey { get; init; }

	public static TargetResult Found(IPlayer player) => new()
	{
		Success = true,
		OnlinePlayer = player,
		SteamId = player.SteamID
	};

	public static TargetResult Found(GuildMember member) => new()
	{
		Success = true,
		GuildMember = member,
		SteamId = member.SteamId
	};

	public static TargetResult Error(string errorKey) => new()
	{
		Success = false,
		ErrorKey = errorKey
	};
}

/// <summary>Context passed to command handlers</summary>
public sealed class CommandContext(Plugin plugin, GuildService guildService, UpgradeService upgradeService, IPlayer player, string[] args)
{
	public Plugin Plugin { get; } = plugin;
	public GuildService GuildService { get; } = guildService;
	public UpgradeService UpgradeService { get; } = upgradeService;
	public IPlayer Player { get; } = player;
	public string[] Args { get; } = args;
	public ILocalizer Localizer { get; } = Plugin.Core.Translation.GetPlayerLocalizer(player);

	/// <summary>Preloaded guild data (only set when RequireGuild is true)</summary>
	public Guild? Guild { get; set; }

	/// <summary>Format a LocalizedMessage with prefix</summary>
	public string Format(LocalizedMessage msg) => msg.Format(Localizer);

	/// <summary>Format a simple key with prefix</summary>
	public string Format(string key) => $"{Localizer["k4.general.prefix"]} {Localizer[key]}";

	/// <summary>Format a key with args and prefix</summary>
	public string Format(string key, params object[] args) => $"{Localizer["k4.general.prefix"]} {Localizer[key, args]}";

	/// <summary>Send a formatted message to the player (schedules to next world update)</summary>
	public void Reply(LocalizedMessage msg) => msg.Send(Player);

	/// <summary>Send a formatted message to the player (schedules to next world update)</summary>
	public void Reply(string key) => LocalizedMessage.Simple(key).Send(Player);

	/// <summary>Send a formatted message with args to the player (schedules to next world update)</summary>
	public void Reply(string key, params object[] args) => LocalizedMessage.WithArgs(key, args).Send(Player);

	/// <summary>Send multiple lines to the player (schedules to next world update)</summary>
	public void ReplyLines(params string[] lines)
	{
		Plugin.Core.Scheduler.NextWorldUpdate(() =>
		{
			foreach (var line in lines)
				Player.SendChat(line);
		});
	}

	/// <summary>Find an online player by name or partial name (for commands like invite that require online players)</summary>
	public TargetResult FindOnlineTarget(string input)
	{
		var searchMode = TargetSearchMode.NoMultipleTargets;
		var targets = Plugin.Core.PlayerManager.FindTargettedPlayers(Player, input, searchMode).ToList();

		if (targets.Count == 0)
			return TargetResult.Error("k4.command.error.player_not_found");

		if (targets.Count > 1)
			return TargetResult.Error("k4.command.error.multiple_matches");

		var target = targets[0];
		if (!target.IsValid || target.IsFakeClient)
			return TargetResult.Error("k4.command.error.player_not_found");

		return TargetResult.Found(target);
	}

	/// <summary>Find a guild member - first tries online targeting, then falls back to offline (SteamID/DB name)</summary>
	public TargetResult FindGuildMember(string input, IReadOnlyList<GuildMember> guildMembers)
	{
		// 1. First try online player targeting system (primary method)
		var onlineResult = FindOnlineTarget(input);
		if (onlineResult.Success)
		{
			var onlinePlayer = onlineResult.OnlinePlayer!;
			var memberByOnline = guildMembers.FirstOrDefault(m => m.SteamId == onlinePlayer.SteamID);
			if (memberByOnline != null)
			{
				return new TargetResult
				{
					Success = true,
					OnlinePlayer = onlinePlayer,
					GuildMember = memberByOnline,
					SteamId = onlinePlayer.SteamID
				};
			}
			// Player is online but not in guild
			return TargetResult.Error("k4.command.error.member_not_found");
		}

		// If multiple matches online, return that error immediately
		if (onlineResult.ErrorKey == "k4.command.error.multiple_matches")
			return onlineResult;

		// 2. No online player found - try parsing as SteamID
		var parsedSteamId = new CSteamID(input);
		if (parsedSteamId.IsValid())
		{
			var steamId64 = parsedSteamId.GetSteamID64();
			var memberBySteamId = guildMembers.FirstOrDefault(m => m.SteamId == steamId64);
			if (memberBySteamId != null)
				return TargetResult.Found(memberBySteamId);
		}

		// 3. Fall back to database name search (offline players)
		var matchingMembers = guildMembers
			.Where(m => m.PlayerName.Contains(input, StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (matchingMembers.Count == 0)
			return TargetResult.Error("k4.command.error.member_not_found");

		if (matchingMembers.Count > 1)
			return TargetResult.Error("k4.command.error.multiple_matches");

		return TargetResult.Found(matchingMembers[0]);
	}
}
