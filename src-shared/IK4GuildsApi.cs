using SwiftlyS2.Shared.Players;

namespace K4Guilds.Shared;

/// <summary>
/// Guild rank permission flags
/// </summary>
[Flags]
public enum GuildPermission
{
    None = 0,
    Chat = 1 << 0,           // Can use guild chat
    Invite = 1 << 1,         // Can invite players
    Kick = 1 << 2,           // Can kick members
    Promote = 1 << 3,        // Can promote members
    Demote = 1 << 4,         // Can demote members
    Withdraw = 1 << 5,       // Can withdraw from guild bank
    Upgrade = 1 << 6,        // Can purchase upgrades
    ManagePerks = 1 << 7,    // Can enable/disable perks
    All = ~0                 // All permissions (Leader)
}

/// <summary>
/// Guild upgrade types
/// </summary>
public enum GuildUpgradeType
{
    Slots,          // Member slot capacity
    BankCapacity,   // Guild bank capacity
    XPBoost,        // XP multiplier for members
    BankInterest    // Passive interest on guild bank balance
}

/// <summary>
/// Guild member information
/// </summary>
public interface IGuildMember
{
    ulong SteamId { get; }
    string PlayerName { get; }
    int RankId { get; }
    string RankName { get; }
    GuildPermission Permissions { get; }
    DateTime JoinedAt { get; }
}

/// <summary>
/// Guild rank information
/// </summary>
public interface IGuildRank
{
    int Id { get; }
    string Name { get; }
    GuildPermission Permissions { get; }
    int Priority { get; }
    bool IsDefault { get; }
}

/// <summary>
/// Guild information
/// </summary>
public interface IGuild
{
    int Id { get; }
    string Name { get; }
    string Tag { get; }
    ulong LeaderSteamId { get; }
    int MemberCount { get; }
    int MaxSlots { get; }
    long BankBalance { get; }
    long MaxBankCapacity { get; }
    int XPBoostPercent { get; }
    DateTime CreatedAt { get; }

    IReadOnlyList<IGuildMember> Members { get; }
    IReadOnlyList<IGuildRank> Ranks { get; }
}

/// <summary>
/// Perk context passed to custom perk handlers
/// </summary>
public interface IPerkContext
{
    IGuild Guild { get; }
    IGuildMember Member { get; }
    IPlayer Player { get; }
    /// <summary>Current level of the perk (1 for purchased Purchasable perks, 1+ for Upgradeable)</summary>
    int PerkLevel { get; }
}

/// <summary>
/// Perk type - determines how the perk can be acquired and used
/// </summary>
public enum GuildPerkType
{
    /// <summary>One-time purchase, can be toggled on/off</summary>
    Purchasable,
    /// <summary>Can be upgraded to higher levels for increased effect</summary>
    Upgradeable
}

/// <summary>
/// Base interface for custom guild perks
/// </summary>
public interface IGuildPerk
{
    /// <summary>Unique identifier for this perk</summary>
    string Id { get; }

    /// <summary>Display name for the perk</summary>
    string Name { get; }

    /// <summary>Description of what the perk does</summary>
    string Description { get; }

    /// <summary>Type of perk (Purchasable or Upgradeable)</summary>
    GuildPerkType PerkType { get; }

    /// <summary>Base cost to purchase (Purchasable) or first level cost (Upgradeable)</summary>
    long BaseCost { get; }

    /// <summary>Max level for Upgradeable perks (ignored for Purchasable)</summary>
    int MaxLevel { get; }

    /// <summary>Cost multiplier per level for Upgradeable perks (e.g., 1.5 = 50% more each level)</summary>
    double CostMultiplier { get; }

    /// <summary>Get cost for a specific level (1 = first purchase/upgrade)</summary>
    long GetCostForLevel(int level) => level <= 1 ? BaseCost : (long)(BaseCost * Math.Pow(CostMultiplier, level - 1));

    /// <summary>Get description for a specific level</summary>
    string GetLevelDescription(int level) => Description;

    /// <summary>Called when a guild member spawns</summary>
    void OnMemberSpawn(IPerkContext context) { }

    /// <summary>Called when a guild member dies</summary>
    void OnMemberDeath(IPerkContext context) { }

    /// <summary>Called when a guild member gets a kill</summary>
    void OnMemberKill(IPerkContext context, IPlayer victim) { }

    /// <summary>Called every round start for guild members</summary>
    void OnRoundStart(IPerkContext context) { }

    /// <summary>Called every round end for guild members</summary>
    void OnRoundEnd(IPerkContext context, int winnerTeam) { }
}

/// <summary>
/// Event arguments for guild events
/// </summary>
public class GuildEventArgs : EventArgs
{
    public IGuild Guild { get; init; } = null!;
}

public class GuildMemberEventArgs : GuildEventArgs
{
    public IGuildMember Member { get; init; } = null!;
    public IPlayer? Player { get; init; }
}

public class GuildInviteEventArgs : GuildEventArgs
{
    public ulong InviterSteamId { get; init; }
    public ulong InviteeSteamId { get; init; }
}

/// <summary>
/// K4-Guilds Developer API
/// </summary>
public interface IK4GuildsApi
{
    // ==================== Guild Queries ====================

    /// <summary>
    /// Get a guild by its ID
    /// </summary>
    Task<IGuild?> GetGuildAsync(int guildId);

    /// <summary>
    /// Get a guild by its name
    /// </summary>
    Task<IGuild?> GetGuildByNameAsync(string name);

    /// <summary>
    /// Get the guild a player belongs to
    /// </summary>
    Task<IGuild?> GetPlayerGuildAsync(ulong steamId);

    /// <summary>
    /// Get guild member info for a player
    /// </summary>
    Task<IGuildMember?> GetGuildMemberAsync(ulong steamId);

    /// <summary>
    /// Check if a player is in any guild
    /// </summary>
    Task<bool> IsInGuildAsync(ulong steamId);

    /// <summary>
    /// Check if a player has a specific permission in their guild
    /// </summary>
    Task<bool> HasPermissionAsync(ulong steamId, GuildPermission permission);

    /// <summary>
    /// Get all guilds (paginated)
    /// </summary>
    Task<IReadOnlyList<IGuild>> GetAllGuildsAsync(int page = 0, int pageSize = 20);

    // ==================== Guild Bank ====================

    /// <summary>
    /// Get guild bank balance
    /// </summary>
    Task<long> GetBankBalanceAsync(int guildId);

    /// <summary>
    /// Add to guild bank (from external source)
    /// </summary>
    Task<bool> AddToBankAsync(int guildId, long amount, string reason);

    /// <summary>
    /// Remove from guild bank (to external source)
    /// </summary>
    Task<bool> RemoveFromBankAsync(int guildId, long amount, string reason);

    // ==================== Perk System ====================

    /// <summary>
    /// Register a custom perk
    /// </summary>
    void RegisterPerk(IGuildPerk perk);

    /// <summary>
    /// Unregister a custom perk
    /// </summary>
    void UnregisterPerk(string perkId);

    /// <summary>
    /// Get all registered perks
    /// </summary>
    IReadOnlyList<IGuildPerk> GetRegisteredPerks();

    /// <summary>
    /// Get perk level for a guild (0 = not purchased, 1+ = purchased/upgraded level)
    /// </summary>
    Task<int> GetPerkLevelAsync(int guildId, string perkId);

    /// <summary>
    /// Check if a perk is active for a guild (level > 0 and enabled)
    /// </summary>
    Task<bool> IsPerkActiveAsync(int guildId, string perkId);

    /// <summary>
    /// Purchase or upgrade a perk for a guild (returns new level)
    /// </summary>
    Task<(bool Success, int NewLevel, string Message)> PurchaseOrUpgradePerkAsync(int guildId, string perkId, ulong buyerSteamId);

    /// <summary>
    /// Toggle perk enabled state (only for purchased Purchasable perks)
    /// </summary>
    Task<bool> TogglePerkAsync(int guildId, string perkId);

    // ==================== Upgrade System ====================

    /// <summary>
    /// Get current upgrade level for a guild
    /// </summary>
    Task<int> GetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType);

    /// <summary>
    /// Get cost for next upgrade level
    /// </summary>
    Task<long> GetUpgradeCostAsync(int guildId, GuildUpgradeType upgradeType);

    // ==================== Events ====================

    /// <summary>
    /// Fired when a new guild is created
    /// </summary>
    event EventHandler<GuildEventArgs>? GuildCreated;

    /// <summary>
    /// Fired when a guild is disbanded
    /// </summary>
    event EventHandler<GuildEventArgs>? GuildDisbanded;

    /// <summary>
    /// Fired when a player joins a guild
    /// </summary>
    event EventHandler<GuildMemberEventArgs>? MemberJoined;

    /// <summary>
    /// Fired when a player leaves a guild
    /// </summary>
    event EventHandler<GuildMemberEventArgs>? MemberLeft;

    /// <summary>
    /// Fired when a player is kicked from a guild
    /// </summary>
    event EventHandler<GuildMemberEventArgs>? MemberKicked;

    /// <summary>
    /// Fired when a player is promoted
    /// </summary>
    event EventHandler<GuildMemberEventArgs>? MemberPromoted;

    /// <summary>
    /// Fired when a player is demoted
    /// </summary>
    event EventHandler<GuildMemberEventArgs>? MemberDemoted;

    /// <summary>
    /// Fired when a guild invite is sent
    /// </summary>
    event EventHandler<GuildInviteEventArgs>? InviteSent;
}
