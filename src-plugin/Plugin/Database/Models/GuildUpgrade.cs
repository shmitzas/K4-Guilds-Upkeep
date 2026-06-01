using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using K4Guilds.Shared;

namespace K4_Guilds.Database.Models;

/// <summary>
/// Database guild upgrade record - Dommel entity for k4_guild_upgrades table
/// </summary>
[Table("k4_guild_upgrades")]
public sealed class GuildUpgrade
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("guild_id")]
    public int GuildId { get; set; }

    [Column("upgrade_type")]
    public int UpgradeType { get; set; }

    [Column("level")]
    public int Level { get; set; }

    [Column("purchased_at")]
    public DateTime PurchasedAt { get; set; }

    public GuildUpgradeType GetUpgradeType() => (GuildUpgradeType)UpgradeType;
    public void SetUpgradeType(GuildUpgradeType type) => UpgradeType = (int)type;
}
