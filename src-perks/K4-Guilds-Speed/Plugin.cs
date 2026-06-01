using K4Guilds.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;

namespace K4_Guilds_Speed;

[PluginMetadata(
	Id = "k4.guilds.speed",
	Version = "1.0.0",
	Name = "K4 - Guilds Speed Perk",
	Author = "K4ryuu",
	Description = "Speed boost perk for K4-Guilds"
)]
public sealed class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
	public static new ISwiftlyCore Core { get; private set; } = null!;

	private IK4GuildsApi? _guildsApi;
	private SpeedConfig _config = null!;

	public override void Load(bool hotReload)
	{
		Core = base.Core;
		_config = BuildConfigService<SpeedConfig>("config.json", "SpeedPerk").Value;
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
			Core.Logger.LogWarning("K4-Guilds API is not available. Speed perk will not function.");
			return;
		}

		_guildsApi = interfaceManager.GetSharedInterface<IK4GuildsApi>("K4Guilds.Api.v1");
		_guildsApi.RegisterPerk(new SpeedBoostPerk(_config));

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
		var speedBonus = _config.SpeedBonusPerLevel;

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

				pawn.VelocityModifier = 1.0f + (level * speedBonus);
				pawn.VelocityModifierUpdated();
			});
		});

		return HookResult.Continue;
	}
}

public sealed class SpeedConfig
{
	public string PerkId { get; set; } = "speed_boost";
	public string PerkName { get; set; } = "Speed Boost";
	public string PerkDescription { get; set; } = "Increased movement speed on spawn";
	public long BaseCost { get; set; } = 2000;
	public int MaxLevel { get; set; } = 5;
	public double CostMultiplier { get; set; } = 1.5;
	public float SpeedBonusPerLevel { get; set; } = 0.02f;
}

public sealed class SpeedBoostPerk(SpeedConfig config) : IGuildPerk
{
	public string Id => config.PerkId;
	public string Name => config.PerkName;
	public string Description => config.PerkDescription;
	public GuildPerkType PerkType => GuildPerkType.Upgradeable;
	public long BaseCost => config.BaseCost;
	public int MaxLevel => config.MaxLevel;
	public double CostMultiplier => config.CostMultiplier;
}
