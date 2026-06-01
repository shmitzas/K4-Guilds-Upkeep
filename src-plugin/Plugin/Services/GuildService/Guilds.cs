using System.Globalization;
using System.Text.RegularExpressions;
using K4_Guilds.Database.Models;
using SwiftlyS2.Shared.Players;

namespace K4_Guilds.Services;

public partial class GuildService
{
	private static readonly Regex ValidNamePattern = new(@"^[\p{L}\p{N}\s\-_\.]+$", RegexOptions.Compiled);
	private static readonly Regex ValidTagPattern = new(@"^[\p{L}\p{N}\p{S}\p{M}\-_]+$", RegexOptions.Compiled);

	private (bool Valid, LocalizedMessage Error) ValidateGuildName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return (false, LocalizedMessage.Simple("k4.guild.error.name_empty"));

		if (name.Trim() != name)
			return (false, LocalizedMessage.Simple("k4.guild.error.name_whitespace"));

		if (!ValidNamePattern.IsMatch(name))
			return (false, LocalizedMessage.Simple("k4.guild.error.name_invalid_chars"));

		if (name.Length < plugin.Guild.MinNameLength)
			return (false, LocalizedMessage.WithArgs("k4.guild.error.name_too_short", plugin.Guild.MinNameLength));

		if (name.Length > plugin.Guild.MaxNameLength)
			return (false, LocalizedMessage.WithArgs("k4.guild.error.name_too_long", plugin.Guild.MaxNameLength));

		return (true, default);
	}

	private (bool Valid, LocalizedMessage Error) ValidateGuildTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
			return (false, LocalizedMessage.Simple("k4.guild.error.tag_empty"));

		if (!ValidTagPattern.IsMatch(tag))
			return (false, LocalizedMessage.Simple("k4.guild.error.tag_invalid_chars"));

		if (new StringInfo(tag).LengthInTextElements > plugin.Guild.MaxTagLength)
			return (false, LocalizedMessage.WithArgs("k4.guild.error.tag_too_long", plugin.Guild.MaxTagLength));

		return (true, default);
	}

	public async Task<(bool Success, LocalizedMessage Message, Guild? Guild)> CreateGuildAsync(IPlayer player, string name)
	{
		var tag = name;

		var (nameOk, nameErr) = ValidateGuildName(name);
		if (!nameOk) return (false, nameErr, null);

		var (tagOk, tagErr) = ValidateGuildTag(tag);
		if (!tagOk) return (false, tagErr, null);

		var existing = await GetPlayerGuildAsync(player.SteamID);
		if (existing != null)
			return (false, LocalizedMessage.Simple("k4.guild.error.already_in_guild"), null);

		if (await database.GetGuildByNameAsync(name) != null)
			return (false, LocalizedMessage.Simple("k4.guild.error.name_taken"), null);

		if (plugin.EconomyAPI != null && plugin.Guild.CreationCost > 0)
		{
			var balance = plugin.EconomyAPI.GetPlayerBalance(player.SteamID, plugin.Guild.WalletKind);
			if (balance < plugin.Guild.CreationCost)
				return (false, LocalizedMessage.WithArgs("k4.guild.error.not_enough_currency", plugin.Guild.CreationCost, balance), null);

			plugin.EconomyAPI.SubtractPlayerBalance(player.SteamID, plugin.Guild.WalletKind, (int)plugin.Guild.CreationCost);
		}

		var leaderRank = GetLeaderRank();
		var guild = await database.CreateGuildAsync(
			player.SteamID,
			player.Controller.PlayerName,
			name,
			tag,
			leaderRank?.Priority ?? 100
		);

		var expiry = DateTime.UtcNow.AddSeconds(CACHE_TTL_SECONDS);
		_playerGuildCache[player.SteamID] = (guild, expiry);
		_guildCache[guild.Id] = (guild, expiry);
		plugin.OnGuildCreated(guild);
		plugin.RefreshPlayerScoreboardTag(player.SteamID);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.created", name), guild);
	}

	public async Task<(bool Success, LocalizedMessage Message)> RenameGuildAsync(Guild guild, IPlayer player, string newName, long cost)
	{
		if (guild.LeaderSteamId != player.SteamID)
			return (false, LocalizedMessage.Simple("k4.guild.error.not_leader"));

		var (nameOk, nameErr) = ValidateGuildName(newName);
		if (!nameOk) return (false, nameErr);

		var other = await database.GetGuildByNameAsync(newName);
		if (other != null && other.Id != guild.Id)
			return (false, LocalizedMessage.Simple("k4.guild.error.name_taken"));

		if (plugin.EconomyAPI != null && cost > 0)
		{
			var balance = plugin.EconomyAPI.GetPlayerBalance(player.SteamID, plugin.Guild.WalletKind);
			if (balance < cost)
				return (false, LocalizedMessage.WithArgs("k4.guild.error.not_enough_currency", cost, balance));

			plugin.EconomyAPI.SubtractPlayerBalance(player.SteamID, plugin.Guild.WalletKind, (int)cost);
		}

		var oldName = guild.Name;
		await database.UpdateGuildNameAsync(guild.Id, newName, newName);
		InvalidateGuildCache(guild.Id);
		plugin.RefreshGuildScoreboardTags(guild.Id);

		return (true, LocalizedMessage.WithArgs("k4.guild.success.renamed", oldName, newName));
	}

	public async Task<(bool Success, LocalizedMessage Message)> DisbandGuildAsync(Guild guild, IPlayer player)
	{
		if (guild.LeaderSteamId != player.SteamID)
			return (false, LocalizedMessage.Simple("k4.guild.error.not_leader"));

		var memberSteamIds = guild.Members.Select(m => m.SteamId).ToList();

		foreach (var member in guild.Members)
			InvalidatePlayerCache(member.SteamId);

		await database.DisbandGuildAsync(guild.Id);
		plugin.OnGuildDisbanded(guild);

		foreach (var steamId in memberSteamIds)
			plugin.RefreshPlayerScoreboardTag(steamId);

		return (true, LocalizedMessage.Simple("k4.guild.success.disbanded"));
	}
}
