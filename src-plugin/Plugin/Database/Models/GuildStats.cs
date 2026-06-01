using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K4_Guilds.Database.Models;

/// <summary>
/// Database guild stats record - Dommel entity for k4_guild_stats table
/// </summary>
[Table("k4_guild_stats")]
public sealed class GuildStats
{
    [Key]
    [Column("guild_id")]
    public int GuildId { get; set; }

    [Column("kills")]
    public int Kills { get; set; }

    [Column("deaths")]
    public int Deaths { get; set; }

    [Column("headshots")]
    public int Headshots { get; set; }

    [Column("assists")]
    public int Assists { get; set; }
}
