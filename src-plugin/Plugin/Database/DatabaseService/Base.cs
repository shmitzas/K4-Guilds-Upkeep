using System.Data;
using K4_Guilds.Database.Migrations;
using Microsoft.Extensions.Logging;

namespace K4_Guilds.Database;

public partial class DatabaseService(string connectionName)
{
	private readonly string _connectionName = connectionName;

	public bool IsEnabled { get; private set; }

	public Task InitializeAsync()
	{
		try
		{
			using var connection = Plugin.Core.Database.GetConnection(_connectionName);
			MigrationRunner.RunMigrations(connection);
			IsEnabled = true;
		}
		catch (Exception ex)
		{
			Plugin.Core.Logger.LogError(ex, "Failed to initialize K4-Guilds database.");
			IsEnabled = false;
		}
		return Task.CompletedTask;
	}

	private IDbConnection GetConnection() => Plugin.Core.Database.GetConnection(_connectionName);
}
