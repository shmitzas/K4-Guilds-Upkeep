namespace K4_Guilds.Services;

internal partial class GuildsApiService
{
	public async Task<long> GetBankBalanceAsync(int guildId)
		=> await plugin.GetDatabase().GetBankBalanceAsync(guildId);

	public async Task<bool> AddToBankAsync(int guildId, long amount, string reason)
	{
		// Use atomic operation to prevent race conditions
		var (success, _) = await plugin.GetDatabase().AtomicAddToBankAsync(guildId, amount, long.MaxValue);
		if (success)
			plugin.GetGuildService().InvalidateGuildCache(guildId);
		return success;
	}

	public async Task<bool> RemoveFromBankAsync(int guildId, long amount, string reason)
	{
		// Use atomic operation to prevent race conditions
		var (success, _) = await plugin.GetDatabase().AtomicSubtractFromBankAsync(guildId, amount);
		if (success)
			plugin.GetGuildService().InvalidateGuildCache(guildId);
		return success;
	}
}
