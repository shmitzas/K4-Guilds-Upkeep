using K4Guilds.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;

namespace K4_Guilds_Health;

[PluginMetadata(
	Id = "k4.guilds.health",
	Version = "1.0.0",
	Name = "K4 - Guilds Health Perk",
	Author = "K4ryuu",
	Description = "Health boost perk for K4-Guilds"
)]
public sealed class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
	public static new ISwiftlyCore Core { get; private set; } = null!;

	private IK4GuildsApi? _guildsApi;
	private HealthConfig _config = null!;

	public override void Load(bool hotReload)
	{
		Core = base.Core;
		_config = BuildConfigService<HealthConfig>("config.json", "HealthPerk").Value;
	}

	private static IOptions<T> BuildConfigService<T>(string fileName, string sectionName) where T : class, new()
	{
		Core.Configuration
			.InitializeJsonWithModel<T>(fileName, sectionName)
			.Configure(cfg => cfg.AddJsonFile(Core.Configuration.GetConfigPath(fileName), optional: false, reloadOnChange: true));

		ServiceCollection services = new();
		services.AddSwiftly(Core)
			.AddOptionsWithValidateOnStart<T>()
			.BindConfiguration(sectionName);

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IOptions<T>>();
	}

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		if (!interfaceManager.HasSharedInterface("K4Guilds.Api.v1"))
		{
			Core.Logger.LogWarning("K4-Guilds API is not available. Health perk will not function.");
			return;
		}

		_guildsApi = interfaceManager.GetSharedInterface<IK4GuildsApi>("K4Guilds.Api.v1");
		_guildsApi.RegisterPerk(new HealthBoostPerk(_config));

		Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
	}

	public override void Unload()
	{
		_guildsApi?.UnregisterPerk(_config.PerkId);
	}

	private HookResult OnPlayerSpawn(EventPlayerSpawn ev)
	{
		if (_guildsApi == null) return HookResult.Continue;

		var player = ev.UserIdPlayer;
		if (!player.IsValid) return HookResult.Continue;

		var perkId = _config.PerkId;
		var baseHealth = _config.BaseHealth;
		var healthPerLevel = _config.HealthPerLevel;

		Task.Run(async () =>
		{
			var guild = await _guildsApi.GetPlayerGuildAsync(player.SteamID);
			if (guild == null) return;

			if (!await _guildsApi.IsPerkActiveAsync(guild.Id, perkId)) return;

			var level = await _guildsApi.GetPerkLevelAsync(guild.Id, perkId);
			if (level <= 0) return;

			Core.Scheduler.NextWorldUpdate(() =>
			{
				if (!player.IsValid) return;

				var pawn = player.Controller?.PlayerPawn.Value;
				if (pawn == null) return;

				var totalHealth = baseHealth + (level * healthPerLevel);

				pawn.MaxHealth = totalHealth;
				pawn.MaxHealthUpdated();

				pawn.Health = totalHealth;
				pawn.HealthUpdated();
			});
		});

		return HookResult.Continue;
	}
}

public sealed class HealthConfig
{
	public string PerkId { get; set; } = "health_boost";
	public string PerkName { get; set; } = "Health Boost";
	public string PerkDescription { get; set; } = "Increased health on spawn";
	public long BaseCost { get; set; } = 1500;
	public int MaxLevel { get; set; } = 10;
	public double CostMultiplier { get; set; } = 1.3;
	public int BaseHealth { get; set; } = 100;
	public int HealthPerLevel { get; set; } = 1;
}

public sealed class HealthBoostPerk(HealthConfig config) : IGuildPerk
{
	public string Id => config.PerkId;
	public string Name => config.PerkName;
	public string Description => config.PerkDescription;
	public GuildPerkType PerkType => GuildPerkType.Upgradeable;
	public long BaseCost => config.BaseCost;
	public int MaxLevel => config.MaxLevel;
	public double CostMultiplier => config.CostMultiplier;
}
