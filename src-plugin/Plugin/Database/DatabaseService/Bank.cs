using Dapper;
using Dommel;
using K4_Guilds.Database.Models;

namespace K4_Guilds.Database;

public partial class DatabaseService
{
	public async Task<long> GetBankBalanceAsync(int guildId)
	{
		using var conn = GetConnection();
		conn.Open();

		var guild = await conn.GetAsync<Guild>(guildId);
		return guild?.BankBalance ?? 0;
	}

	public async Task<bool> UpdateBankBalanceAsync(int guildId, long newBalance)
	{
		using var conn = GetConnection();
		conn.Open();

		var affected = await conn.ExecuteAsync(
			"UPDATE k4_guilds SET bank_balance = @Balance, updated_at = @Now WHERE id = @Id",
			new { Balance = newBalance, Now = DateTime.UtcNow, Id = guildId });
		return affected > 0;
	}

	/// <summary>Atomically add to bank balance with overflow protection</summary>
	public async Task<(bool Success, long NewBalance)> AtomicAddToBankAsync(int guildId, long amount, long maxCapacity)
	{
		if (amount <= 0) return (false, 0);

		using var conn = GetConnection();
		conn.Open();
		using var transaction = conn.BeginTransaction();

		try
		{
			var guild = await conn.GetAsync<Guild>(guildId, transaction);
			if (guild == null)
			{
				transaction.Rollback();
				return (false, 0);
			}

			// Overflow protection
			if (guild.BankBalance > long.MaxValue - amount)
			{
				transaction.Rollback();
				return (false, guild.BankBalance);
			}

			var newBalance = guild.BankBalance + amount;
			if (newBalance > maxCapacity)
			{
				transaction.Rollback();
				return (false, guild.BankBalance);
			}

			guild.BankBalance = newBalance;
			guild.UpdatedAt = DateTime.UtcNow;
			await conn.UpdateAsync(guild, transaction);
			transaction.Commit();

			return (true, newBalance);
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>Atomically subtract from bank balance with validation</summary>
	public async Task<(bool Success, long NewBalance)> AtomicSubtractFromBankAsync(int guildId, long amount)
	{
		if (amount <= 0) return (false, 0);

		using var conn = GetConnection();
		conn.Open();
		using var transaction = conn.BeginTransaction();

		try
		{
			var guild = await conn.GetAsync<Guild>(guildId, transaction);
			if (guild == null)
			{
				transaction.Rollback();
				return (false, 0);
			}

			if (guild.BankBalance < amount)
			{
				transaction.Rollback();
				return (false, guild.BankBalance);
			}

			var newBalance = guild.BankBalance - amount;
			guild.BankBalance = newBalance;
			guild.UpdatedAt = DateTime.UtcNow;
			await conn.UpdateAsync(guild, transaction);
			transaction.Commit();

			return (true, newBalance);
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}
}
