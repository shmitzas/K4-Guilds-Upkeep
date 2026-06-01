using K4Guilds.Shared;

namespace K4_Guilds.Config;

/// <summary>
/// Guild configuration (guild.json)
/// Main guild settings and database connection
/// </summary>
public sealed class GuildConfig
{
	/* ==================== Database & Economy ==================== */

	/// <summary>DB connection name (from SwiftlyS2's database.jsonc)</summary>
	public string DatabaseConnection { get; set; } = "default";

	/// <summary>Wallet kind to use for guild transactions</summary>
	public string WalletKind { get; set; } = "credits";

	/* ==================== Guild Creation ==================== */

	/// <summary>Default member slots for new guilds</summary>
	public int DefaultSlots { get; set; } = 5;

	/// <summary>Maximum slots a guild can have (with upgrades)</summary>
	public int MaxSlots { get; set; } = 20;

	/// <summary>Cost to create a guild (uses Economy API)</summary>
	public long CreationCost { get; set; } = 5000;

	/* ==================== Guild Names ==================== */

	/// <summary>Minimum guild name length</summary>
	public int MinNameLength { get; set; } = 3;

	/// <summary>Maximum guild name length</summary>
	public int MaxNameLength { get; set; } = 32;

	/// <summary>Maximum guild tag length</summary>
	public int MaxTagLength { get; set; } = 4;

	/* ==================== Debug ==================== */

	/// <summary>Enable verbose debug logging (wrap LogInformation in if (cfg.DetailedLogging))</summary>
	public bool DetailedLogging { get; set; } = false;

	/* ==================== Scoreboard ==================== */

	/// <summary>Show guild tag on scoreboard (clan tag)</summary>
	public bool ShowTagOnScoreboard { get; set; } = true;

	/// <summary>Interval in seconds to refresh scoreboard tags for all players (0 = disabled, only event-based refresh)</summary>
	public int ScoreboardRefreshIntervalSeconds { get; set; } = 60;

	/* ==================== Guild Ranks ==================== */

	/// <summary>
	/// Default ranks created when a guild is formed.
	/// Name is a translation key like "k4.rank.leader".
	/// Permissions use GuildPermission flags: Chat=1, Invite=2, Kick=4, Promote=8, Demote=16, Withdraw=32, Upgrade=64, ManagePerks=128, All=-1
	/// </summary>
	public List<GuildRankSettings> GuildRanks { get; set; } =
	[
		new()
		{
			Name = "k4.rank.leader",
			Permissions = (int)GuildPermission.All,  // -1 = All permissions
			Priority = 100,
			IsDefault = false
		},
		new()
		{
			Name = "k4.rank.officer",
			Permissions = (int)(GuildPermission.Chat | GuildPermission.Invite | GuildPermission.Kick | GuildPermission.Promote | GuildPermission.Demote | GuildPermission.Withdraw | GuildPermission.ManagePerks),  // 1+2+4+8+16+32+128 = 191
			Priority = 50,
			IsDefault = false
		},
		new()
		{
			Name = "k4.rank.member",
			Permissions = (int)GuildPermission.Chat,  // 1 = Chat only
			Priority = 0,
			IsDefault = true
		}
	];
}

/// <summary>
/// Single guild rank definition (global across all guilds)
/// </summary>
public sealed class GuildRankSettings
{
	/// <summary>Translation key for rank name (e.g., "k4.rank.leader")</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Permission flags (bitfield).
	/// Use GuildPermission enum values: Chat=1, Invite=2, Kick=4, Promote=8, Demote=16, Withdraw=32, Upgrade=64, ManagePerks=128
	/// Use -1 for all permissions.
	/// </summary>
	public int Permissions { get; set; }

	/// <summary>Rank priority (higher = more authority, used for promote/demote limits)</summary>
	public int Priority { get; set; }

	/// <summary>If true, new members get this rank when joining</summary>
	public bool IsDefault { get; set; }

	/// <summary>Get permissions as GuildPermission enum</summary>
	public GuildPermission GetPermissions() => (GuildPermission)Permissions;

	/// <summary>Check if this rank has a specific permission</summary>
	public bool HasPermission(GuildPermission permission)
		=> Permissions == -1 || (GetPermissions() & permission) == permission;
}
