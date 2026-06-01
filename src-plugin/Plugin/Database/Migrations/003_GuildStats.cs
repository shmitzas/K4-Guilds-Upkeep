using FluentMigrator;

namespace K4_Guilds.Database.Migrations;

[Migration(202604060001)]
public class CreateGuildStats : Migration
{
    public override void Up()
    {
        if (!Schema.Table("k4_guild_stats").Exists())
        {
            Create.Table("k4_guild_stats")
                .WithColumn("guild_id").AsInt32().PrimaryKey().ForeignKey("k4_guilds", "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("kills").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("deaths").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("headshots").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("assists").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("k4_guild_stats").Exists())
            Delete.Table("k4_guild_stats");
    }
}
