using K4_Guilds.Database.Models;
using K4Guilds.Shared;

namespace K4_Guilds.Services;

public partial class GuildService
{
	private static int GetUpgradeLevel(Guild guild, GuildUpgradeType type)
		=> guild.Upgrades.FirstOrDefault(u => u.GetUpgradeType() == type)?.Level ?? 0;

	public int CalculateMaxSlots(Guild guild)
	{
		var level = GetUpgradeLevel(guild, GuildUpgradeType.Slots);
		var calculated = plugin.Guild.DefaultSlots + (level * plugin.Upgrades.SlotUpgrade.SlotsPerLevel);
		return Math.Min(calculated, plugin.Guild.MaxSlots);
	}

	public long CalculateMaxBankCapacity(Guild guild)
	{
		var level = GetUpgradeLevel(guild, GuildUpgradeType.BankCapacity);
		return plugin.Upgrades.BankCapacity.BaseCapacity + (level * plugin.Upgrades.BankCapacity.CapacityPerLevel);
	}

	public int CalculateXPBoostPercent(Guild guild)
		=> GetUpgradeLevel(guild, GuildUpgradeType.XPBoost) * plugin.Upgrades.XPBoost.BoostPerLevel;

	public bool HasPermission(Guild guild, ulong steamId, GuildPermission permission)
	{
		var member = FindMember(guild, steamId);
		if (member == null)
			return false;

		var rank = GetRankByPriority(member.RankPriority);
		return rank?.HasPermission(permission) ?? false;
	}

	public async Task<bool> HasPermissionAsync(ulong steamId, GuildPermission permission)
	{
		var guild = await GetPlayerGuildAsync(steamId);
		return guild != null && HasPermission(guild, steamId, permission);
	}
}
