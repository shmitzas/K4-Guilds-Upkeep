using Economy.Contract;
using K4_Guilds.Commands;
using K4_Guilds.Config;
using K4_Guilds.Database;
using K4_Guilds.Database.Models;
using K4_Guilds.Models;
using K4_Guilds.Services;
using K4Guilds.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace K4_Guilds;

[PluginMetadata(
	Id = "k4.guilds",
	Version = "1.0.3",
	Name = "K4 - Guilds",
	Author = "K4ryuu",
	Description = "Guild system with ranks, upgrades, and developer API"
)]
public sealed partial class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
	public static new ISwiftlyCore Core { get; private set; } = null!;

	public static IOptionsMonitor<GuildConfig> GuildConfig { get; private set; } = null!;
	public static IOptionsMonitor<UpgradesConfig> UpgradesConfig { get; private set; } = null!;
	public static IOptionsMonitor<CommandsConfig> CommandsConfig { get; private set; } = null!;

	internal GuildConfig Guild => GuildConfig.CurrentValue;
	internal UpgradesConfig Upgrades => UpgradesConfig.CurrentValue;
	internal CommandsConfig Commands => CommandsConfig.CurrentValue;

	private DatabaseService _database = null!;
	private GuildService _guildService = null!;
	private UpgradeService _upgradeService = null!;
	private PerkService _perkService = null!;
	private InviteService _inviteService = null!;
	private GuildCommands _commands = null!;
	private CancellationTokenSource? _interestTimerCts;
	private CancellationTokenSource? _scoreboardTimerCts;

	public IEconomyAPIv1? EconomyAPI { get; private set; }

	public event EventHandler<GuildEventArgs>? GuildCreated;
	public event EventHandler<GuildEventArgs>? GuildDisbanded;
	public event EventHandler<GuildMemberEventArgs>? MemberJoined;
	public event EventHandler<GuildMemberEventArgs>? MemberLeft;
	public event EventHandler<GuildMemberEventArgs>? MemberKicked;
	public event EventHandler<GuildMemberEventArgs>? MemberPromoted;
	public event EventHandler<GuildMemberEventArgs>? MemberDemoted;
	public event EventHandler<GuildInviteEventArgs>? InviteSent;

	public override void Load(bool hotReload)
	{
		Core = base.Core;

		InitializeConfigs();
		InitializeDatabase();
		InitializeServices();
		RegisterEvents();
		_commands.RegisterCommands();
		StartInterestTimer();
		StartScoreboardRefreshTimer();
	}

	public override void Unload()
	{
		Core.Event.OnClientPutInServer -= OnClientPutInServerEvent;
		Core.Event.OnClientDisconnected -= OnClientDisconnected;
		_interestTimerCts?.Cancel();
		_interestTimerCts = null;
		_scoreboardTimerCts?.Cancel();
		_scoreboardTimerCts = null;
	}

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		if (!interfaceManager.HasSharedInterface("Economy.API.v1"))
		{
			Core.Logger.LogWarning("Economy API is not available.");
			return;
		}

		EconomyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
		EconomyAPI.EnsureWalletKind(Guild.WalletKind);
	}

	public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
	{
		const string apiVersion = "K4Guilds.Api.v1";
		var apiService = new GuildsApiService(this);
		interfaceManager.AddSharedInterface<IK4GuildsApi, GuildsApiService>(apiVersion, apiService);
	}

	private void InitializeConfigs()
	{
		GuildConfig = BuildConfigService<GuildConfig>("guild.json", "K4GuildsGuild");
		UpgradesConfig = BuildConfigService<UpgradesConfig>("upgrades.json", "K4GuildsUpgrades");
		CommandsConfig = BuildConfigService<CommandsConfig>("commands.json", "K4GuildsCommands");
	}

	private static IOptionsMonitor<T> BuildConfigService<T>(string fileName, string sectionName) where T : class, new()
	{
		Core.Configuration
			.InitializeJsonWithModel<T>(fileName, sectionName)
			.Configure(builder =>
			{
				builder.AddJsonFile(fileName, optional: false, reloadOnChange: true);
			});

		ServiceCollection services = new();
		services.AddSwiftly(Core)
			.AddOptions<T>()
			.BindConfiguration(sectionName);

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IOptionsMonitor<T>>();
	}

	private void InitializeDatabase()
	{
		_database = new DatabaseService(Guild.DatabaseConnection);
		_ = _database.InitializeAsync().ContinueWith(
			t => Core.Logger.LogError(t.Exception, "Failed to initialize K4-Guilds database."),
			TaskContinuationOptions.OnlyOnFaulted);
	}

	private void InitializeServices()
	{
		_guildService = new GuildService(this, _database);
		_inviteService = new InviteService();
		_guildService.SetInviteService(_inviteService);
		_upgradeService = new UpgradeService(this, _database, _guildService);
		_perkService = new PerkService(this, _database);
		_commands = new GuildCommands(this, _guildService, _upgradeService);
	}

	private void RegisterEvents()
	{
		Core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
		Core.Event.OnClientPutInServer += OnClientPutInServerEvent;
		Core.Event.OnClientDisconnected += OnClientDisconnected;
	}

	private void OnClientPutInServerEvent(IOnClientPutInServerEvent ev)
	{
		if (Guild.DetailedLogging)
			Core.Logger.LogInformation("[K4-Guilds] OnClientPutInServer: PlayerId={PlayerId}", ev.PlayerId);

		var player = Core.PlayerManager.GetPlayer(ev.PlayerId);

		if (player == null)
		{
			if (Guild.DetailedLogging)
				Core.Logger.LogInformation("[K4-Guilds] OnClientPutInServer: player is null for PlayerId={PlayerId}, skipping.", ev.PlayerId);
			return;
		}

		if (!player.IsValid || player.IsFakeClient)
		{
			if (Guild.DetailedLogging)
				Core.Logger.LogInformation("[K4-Guilds] OnClientPutInServer: player invalid (IsValid={IsValid}, IsFakeClient={IsFakeClient}), skipping.", player.IsValid, player.IsFakeClient);
			return;
		}

		if (Guild.DetailedLogging)
			Core.Logger.LogInformation("[K4-Guilds] OnClientPutInServer: SteamID={SteamID}, triggering scoreboard tag update.", player.SteamID);

		_ = UpdatePlayerScoreboardTag(player);
	}

	private HookResult OnPlayerDeath(EventPlayerDeath ev)
	{
		// Capture event data synchronously before going async (event object may be recycled after handler returns)
		var victim = ev.UserIdPlayer;
		var attacker = ev.AttackerPlayer;
		var assister = ev.AssisterPlayer;
		var isHeadshot = ev.Headshot;
		bool isSuicide = victim != null && attacker != null && attacker.SteamID == victim.SteamID;

		_ = HandlePlayerDeathStatsAsync(victim, attacker, assister, isHeadshot, isSuicide);

		return HookResult.Continue;
	}

	private async Task HandlePlayerDeathStatsAsync(IPlayer? victim, IPlayer? attacker, IPlayer? assister, bool isHeadshot, bool isSuicide)
	{
		// Resolve all three guilds in parallel — independent lookups, no reason to serialize them
		var victimTask = victim is { IsFakeClient: false }
			? _guildService.GetPlayerGuildAsync(victim.SteamID)
			: Task.FromResult<Guild?>(null);

		var attackerTask = attacker is { IsFakeClient: false } && !isSuicide
			? _guildService.GetPlayerGuildAsync(attacker.SteamID)
			: Task.FromResult<Guild?>(null);

		var assisterTask = assister is { IsFakeClient: false }
			? _guildService.GetPlayerGuildAsync(assister.SteamID)
			: Task.FromResult<Guild?>(null);

		await Task.WhenAll(victimTask, attackerTask, assisterTask);

		var victimGuild = victimTask.Result;
		var attackerGuild = attackerTask.Result;
		var assisterGuild = assisterTask.Result;

		// Accumulate per-guild stat deltas — coalesces into one DB call when multiple roles share a guild
		var deltas = new Dictionary<int, (int kills, int deaths, int headshots, int assists)>();

		if (victimGuild != null)
		{
			var (k, d, h, a) = deltas.GetValueOrDefault(victimGuild.Id);
			deltas[victimGuild.Id] = (k, d + 1, h, a);
		}

		if (attackerGuild != null)
		{
			var (k, d, h, a) = deltas.GetValueOrDefault(attackerGuild.Id);
			deltas[attackerGuild.Id] = (k + 1, d, h + (isHeadshot ? 1 : 0), a);
		}

		if (assisterGuild != null)
		{
			var (k, d, h, a) = deltas.GetValueOrDefault(assisterGuild.Id);
			deltas[assisterGuild.Id] = (k, d, h, a + 1);
		}

		// One DB upsert per distinct guild (often just 1-2 calls instead of up to 3)
		var dbTasks = deltas.Select(kvp =>
			_database.IncrementGuildStatsAsync(kvp.Key, kvp.Value.kills, kvp.Value.deaths, kvp.Value.headshots, kvp.Value.assists));

		await Task.WhenAll(dbTasks);
	}

	private async Task UpdatePlayerScoreboardTag(IPlayer player)
	{
		if (!Guild.ShowTagOnScoreboard)
		{
			if (Guild.DetailedLogging)
				Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag: ShowTagOnScoreboard=false, skipping SteamID={SteamID}.", player.SteamID);
			return;
		}

		if (Guild.DetailedLogging)
			Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag: querying guild for SteamID={SteamID}.", player.SteamID);

		var guild = await _guildService.GetPlayerGuildAsync(player.SteamID);

		if (Guild.DetailedLogging)
			Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag: SteamID={SteamID} guild={GuildName} (null={IsNull}).", player.SteamID, guild?.Name ?? "(none)", guild == null);

		var localizer = Core.Translation.GetPlayerLocalizer(player);
		string tag = string.Empty;
		if (guild != null)
		{
			try { tag = localizer["k4.clan.tag_format.scoreboard", guild.Tag]; }
			catch { tag = $"[{guild.Tag}]"; }
		}

		if (Guild.DetailedLogging)
			Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag: applying tag='{Tag}' for SteamID={SteamID}.", tag, player.SteamID);

		Core.Scheduler.NextWorldUpdate(() =>
		{
			if (!player.IsValid)
			{
				if (Guild.DetailedLogging)
					Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag (NextWorldUpdate): player no longer valid for SteamID={SteamID}.", player.SteamID);
				return;
			}
			var controller = player.Controller;
			if (controller == null)
			{
				if (Guild.DetailedLogging)
					Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag (NextWorldUpdate): controller is null for SteamID={SteamID}.", player.SteamID);
				return;
			}

			controller.Clan = tag;
			controller.ClanUpdated();
			if (Core.GameEvent.IsListeningToEvent<EventNextlevelChanged>(player.PlayerID))
				Core.GameEvent.FireToPlayerAsync<EventNextlevelChanged>(player.PlayerID);

			if (Guild.DetailedLogging)
				Core.Logger.LogInformation("[K4-Guilds] UpdatePlayerScoreboardTag (NextWorldUpdate): clan tag set to '{Tag}' for SteamID={SteamID}.", tag, player.SteamID);
		});
	}

	internal void RefreshPlayerScoreboardTag(ulong steamId)
	{
		foreach (var player in Core.PlayerManager.GetAllPlayers())
		{
			if (player.SteamID == steamId && player.IsValid)
			{
				_ = UpdatePlayerScoreboardTag(player);
				return;
			}
		}
	}

	internal void RefreshGuildScoreboardTags(int guildId)
	{
		_ = RefreshGuildScoreboardTagsAsync(guildId);
	}

	private async Task RefreshGuildScoreboardTagsAsync(int guildId)
	{
		var guild = await _guildService.GetGuildAsync(guildId);
		if (guild == null) return;

		var memberSteamIds = new HashSet<ulong>(guild.Members.Select(m => m.SteamId));
		var onlinePlayers = Core.PlayerManager.GetAllPlayers()
			.Where(p => p.IsValid && memberSteamIds.Contains(p.SteamID))
			.ToList();

		foreach (var player in onlinePlayers)
			await UpdatePlayerScoreboardTag(player);
	}

	private void StartInterestTimer()
	{
		if (!Upgrades.BankInterest.Enabled) return;

		var intervalSeconds = Upgrades.BankInterest.IntervalMinutes * 60f;
		_interestTimerCts = Core.Scheduler.RepeatBySeconds(intervalSeconds, ProcessBankInterest);
	}

	private void StartScoreboardRefreshTimer()
	{
		if (!Guild.ShowTagOnScoreboard || Guild.ScoreboardRefreshIntervalSeconds <= 0) return;

		_scoreboardTimerCts = Core.Scheduler.RepeatBySeconds(Guild.ScoreboardRefreshIntervalSeconds, RefreshAllScoreboardTags);
	}

	private void RefreshAllScoreboardTags()
	{
		_ = RefreshAllScoreboardTagsAsync();
	}

	private async Task RefreshAllScoreboardTagsAsync()
	{
		foreach (var player in Core.PlayerManager.GetAllPlayers())
		{
			if (player.IsValid)
				await UpdatePlayerScoreboardTag(player);
		}
	}

	private void ProcessBankInterest()
	{
		_ = ProcessBankInterestAsync();
	}

	private async Task ProcessBankInterestAsync()
	{
		var guilds = await _database.GetAllGuildsAsync(0, 1000);
		foreach (var guild in guilds)
		{
			var interestPercent = _upgradeService.CalculateBankInterestPercent(guild);
			if (interestPercent <= 0 || guild.BankBalance <= 0) continue;

			var interest = (long)(guild.BankBalance * interestPercent / 100);
			if (interest <= 0) continue;

			var maxCapacity = _guildService.CalculateMaxBankCapacity(guild);
			var newBalance = Math.Min(guild.BankBalance + interest, maxCapacity);

			await _database.UpdateBankBalanceAsync(guild.Id, newBalance);
			_guildService.InvalidateGuildCache(guild.Id);
		}
	}

	private void OnClientDisconnected(IOnClientDisconnectedEvent ev)
	{
		var player = Core.PlayerManager.GetPlayer(ev.PlayerId);

		if (player == null || !player.IsValid || player.IsFakeClient)
			return;

		_guildService.InvalidatePlayerCache(player.SteamID);
	}

	internal void OnGuildCreated(Guild guild)
	{
		GuildCreated?.Invoke(this, new GuildEventArgs { Guild = new GuildWrapper(guild, _guildService) });
	}

	internal void OnGuildDisbanded(Guild guild)
	{
		_inviteService.RemoveAllInvitesForGuild(guild.Id);
		GuildDisbanded?.Invoke(this, new GuildEventArgs { Guild = new GuildWrapper(guild, _guildService) });
	}

	internal void OnMemberJoined(Guild guild, GuildMember member, IPlayer? player)
	{
		RefreshPlayerScoreboardTag(member.SteamId);

		var rank = _guildService.GetRankByPriority(member.RankPriority);
		MemberJoined?.Invoke(this, new GuildMemberEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			Member = new MemberWrapper(member, rank),
			Player = player
		});
	}

	internal void OnMemberLeft(Guild guild, GuildMember member, IPlayer? player)
	{
		RefreshPlayerScoreboardTag(member.SteamId);

		var rank = _guildService.GetRankByPriority(member.RankPriority);
		MemberLeft?.Invoke(this, new GuildMemberEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			Member = new MemberWrapper(member, rank),
			Player = player
		});
	}

	internal void OnMemberKicked(Guild guild, GuildMember member, IPlayer? player)
	{
		RefreshPlayerScoreboardTag(member.SteamId);

		var rank = _guildService.GetRankByPriority(member.RankPriority);
		MemberKicked?.Invoke(this, new GuildMemberEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			Member = new MemberWrapper(member, rank),
			Player = player
		});
	}

	internal void OnMemberPromoted(Guild guild, GuildMember member, IPlayer? player)
	{
		var rank = _guildService.GetRankByPriority(member.RankPriority);
		MemberPromoted?.Invoke(this, new GuildMemberEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			Member = new MemberWrapper(member, rank),
			Player = player
		});
	}

	internal void OnMemberDemoted(Guild guild, GuildMember member, IPlayer? player)
	{
		var rank = _guildService.GetRankByPriority(member.RankPriority);
		MemberDemoted?.Invoke(this, new GuildMemberEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			Member = new MemberWrapper(member, rank),
			Player = player
		});
	}

	internal void OnInviteSent(Guild guild, ulong inviterSteamId, ulong inviteeSteamId)
	{
		InviteSent?.Invoke(this, new GuildInviteEventArgs
		{
			Guild = new GuildWrapper(guild, _guildService),
			InviterSteamId = inviterSteamId,
			InviteeSteamId = inviteeSteamId
		});
	}

	internal GuildService GetGuildService() => _guildService;
	internal UpgradeService GetUpgradeService() => _upgradeService;
	internal PerkService GetPerkService() => _perkService;
	internal DatabaseService GetDatabase() => _database;
}
