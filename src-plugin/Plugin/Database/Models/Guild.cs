using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4_Guilds.Database.Models;

/// <summary>
/// Database guild record - Dommel entity for k4_guilds table
/// </summary>
[Table("k4_guilds")]
public sealed class Guild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("tag")]
    public string Tag { get; set; } = string.Empty;

    [Column("leader_steam_id")]
    public ulong LeaderSteamId { get; set; }

    [Column("bank_balance")]
    public long BankBalance { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation (not from DB, populated in code)
    [NotMapped]
    public List<GuildMember> Members { get; set; } = [];

    [NotMapped]
    public List<GuildUpgrade> Upgrades { get; set; } = [];

    [NotMapped]
    public List<GuildPerk> EnabledPerks { get; set; } = [];
}
