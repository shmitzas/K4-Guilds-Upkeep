using FluentMigrator;

namespace K4_Guilds.Database.Migrations;

[Migration(202604050001)]
public class UnicodeTagSupport : Migration
{
    public override void Up()
    {
        // MySQL defaults to utf8 (3-byte), which cannot store 4-byte Unicode characters (e.g. emoji).
        // Alter text columns in k4_guilds and k4_guild_members to utf8mb4 so all Unicode code points
        // are stored correctly.  PostgreSQL and SQLite natively support full Unicode, no changes needed.
        IfDatabase("MySql5").Execute.Sql(
            "ALTER TABLE k4_guilds " +
            "MODIFY COLUMN name VARCHAR(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL, " +
            "MODIFY COLUMN tag VARCHAR(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL"
        );

        IfDatabase("MySql5").Execute.Sql(
            "ALTER TABLE k4_guild_members " +
            "MODIFY COLUMN player_name VARCHAR(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL"
        );
    }

    public override void Down()
    {
        // Reverting charset would risk corrupting existing Unicode data — intentionally left empty.
    }
}
