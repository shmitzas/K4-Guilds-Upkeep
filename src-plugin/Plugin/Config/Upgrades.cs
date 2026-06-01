using K4Guilds.Shared;

namespace K4_Guilds.Config;

/// <summary>Common interface for all upgrade settings</summary>
public interface IUpgradeSettings
{
	bool Enabled { get; }
	int MaxLevel { get; }
	long BaseCost { get; }
	double CostMultiplier { get; }

	/// <summary>Calculates upgrade cost for given level</summary>
	long GetCost(int currentLevel) => currentLevel >= MaxLevel ? -1 : (long)(BaseCost * Math.Pow(CostMultiplier, currentLevel));
}

/// <summary>
/// Upgrades configuration (upgrades.json)
/// </summary>
public sealed class UpgradesConfig
{
	public SlotUpgradeSettings SlotUpgrade { get; set; } = new();
	public BankCapacitySettings BankCapacity { get; set; } = new();
	public XPBoostSettings XPBoost { get; set; } = new();
	public BankInterestSettings BankInterest { get; set; } = new();

	private Dictionary<GuildUpgradeType, IUpgradeSettings>? _settingsMap;

	/// <summary>Gets settings for upgrade type</summary>
	public IUpgradeSettings this[GuildUpgradeType type] => (_settingsMap ??= new()
	{
		[GuildUpgradeType.Slots] = SlotUpgrade,
		[GuildUpgradeType.BankCapacity] = BankCapacity,
		[GuildUpgradeType.XPBoost] = XPBoost,
		[GuildUpgradeType.BankInterest] = BankInterest
	})[type];
}

public sealed class SlotUpgradeSettings : IUpgradeSettings
{
	public bool Enabled { get; set; } = true;
	public int MaxLevel { get; set; } = 5;
	public long BaseCost { get; set; } = 1000;
	public double CostMultiplier { get; set; } = 1.5;
	public int SlotsPerLevel { get; set; } = 2;
}

public sealed class BankCapacitySettings : IUpgradeSettings
{
	public bool Enabled { get; set; } = true;
	public int MaxLevel { get; set; } = 10;
	public long BaseCost { get; set; } = 500;
	public double CostMultiplier { get; set; } = 1.3;
	public long BaseCapacity { get; set; } = 10000;
	public long CapacityPerLevel { get; set; } = 5000;
}

public sealed class XPBoostSettings : IUpgradeSettings
{
	public bool Enabled { get; set; } = true;
	public int MaxLevel { get; set; } = 5;
	public long BaseCost { get; set; } = 2000;
	public double CostMultiplier { get; set; } = 2.0;
	public int BoostPerLevel { get; set; } = 5;
}

public sealed class BankInterestSettings : IUpgradeSettings
{
	public bool Enabled { get; set; } = true;
	public int MaxLevel { get; set; } = 5;
	public long BaseCost { get; set; } = 3000;
	public double CostMultiplier { get; set; } = 2.5;
	public double InterestPerLevel { get; set; } = 1.0;
	public int IntervalMinutes { get; set; } = 60;
}
