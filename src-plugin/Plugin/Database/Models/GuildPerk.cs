using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4_Guilds.Database.Models;

/// <summary>
/// Database guild perk record - Dommel entity for k4_guild_perks table
/// </summary>
[Table("k4_guild_perks")]
public sealed class GuildPerk
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("guild_id")]
    public int GuildId { get; set; }

    [Column("perk_id")]
    public string PerkId { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Column("purchased_at")]
    public DateTime PurchasedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
