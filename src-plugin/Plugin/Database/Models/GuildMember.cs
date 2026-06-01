using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4_Guilds.Database.Models;

/// <summary>
/// Database guild member record - Dommel entity for k4_guild_members table
/// </summary>
[Table("k4_guild_members")]
public sealed class GuildMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("guild_id")]
    public int GuildId { get; set; }

    [Column("steam_id")]
    public ulong SteamId { get; set; }

    [Column("player_name")]
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Rank priority value from config (maps to GuildRankSettings.Priority)
    /// </summary>
    [Column("rank_priority")]
    public int RankPriority { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [Column("last_seen")]
    public DateTime LastSeen { get; set; }
}
