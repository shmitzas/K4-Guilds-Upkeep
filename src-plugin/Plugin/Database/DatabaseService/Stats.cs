using System.Data;
using Dapper;
using Dommel;
using K4_Guilds.Database.Models;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace K4_Guilds.Database;

public sealed class TopGuildEntry
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Headshots { get; set; }
    public int Assists { get; set; }
}

public partial class DatabaseService
{
    public async Task<GuildStats?> GetGuildStatsAsync(int guildId)
    {
        using var conn = GetConnection();
        conn.Open();
        return (await conn.SelectAsync<GuildStats>(s => s.GuildId == guildId)).FirstOrDefault();
    }

    /// <summary>
    /// Atomically increments the specified stats columns for a guild.
    /// Creates a stats row for the guild if one does not yet exist.
    /// </summary>
    public async Task IncrementGuildStatsAsync(int guildId, int kills = 0, int deaths = 0, int headshots = 0, int assists = 0)
    {
        using var conn = GetConnection();
        conn.Open();

        string sql = conn switch
        {
            MySqlConnection => """
                INSERT INTO k4_guild_stats (guild_id, kills, deaths, headshots, assists)
                VALUES (@GuildId, @Kills, @Deaths, @Headshots, @Assists)
                ON DUPLICATE KEY UPDATE
                    kills     = kills     + VALUES(kills),
                    deaths    = deaths    + VALUES(deaths),
                    headshots = headshots + VALUES(headshots),
                    assists   = assists   + VALUES(assists)
                """,
            NpgsqlConnection => """
                INSERT INTO k4_guild_stats (guild_id, kills, deaths, headshots, assists)
                VALUES (@GuildId, @Kills, @Deaths, @Headshots, @Assists)
                ON CONFLICT (guild_id) DO UPDATE SET
                    kills     = k4_guild_stats.kills     + EXCLUDED.kills,
                    deaths    = k4_guild_stats.deaths    + EXCLUDED.deaths,
                    headshots = k4_guild_stats.headshots + EXCLUDED.headshots,
                    assists   = k4_guild_stats.assists   + EXCLUDED.assists
                """,
            SqliteConnection => """
                INSERT INTO k4_guild_stats (guild_id, kills, deaths, headshots, assists)
                VALUES (@GuildId, @Kills, @Deaths, @Headshots, @Assists)
                ON CONFLICT (guild_id) DO UPDATE SET
                    kills     = kills     + excluded.kills,
                    deaths    = deaths    + excluded.deaths,
                    headshots = headshots + excluded.headshots,
                    assists   = assists   + excluded.assists
                """,
            _ => throw new NotSupportedException($"Unsupported database connection type: {conn.GetType().Name}")
        };

        await conn.ExecuteAsync(sql, new { GuildId = guildId, Kills = kills, Deaths = deaths, Headshots = headshots, Assists = assists });
    }

    public async Task<IReadOnlyList<TopGuildEntry>> GetTopGuildsByKillsAsync(int limit)
    {
        using var conn = GetConnection();
        conn.Open();

        var rows = await conn.QueryAsync<TopGuildEntry>(
            """
            SELECT g.name AS Name, g.tag AS Tag, s.kills AS Kills, s.deaths AS Deaths, s.headshots AS Headshots, s.assists AS Assists
            FROM k4_guild_stats s
            JOIN k4_guilds g ON g.id = s.guild_id
            ORDER BY s.kills DESC
            LIMIT @Limit
            """,
            new { Limit = limit });

        return rows.ToList();
    }
}
