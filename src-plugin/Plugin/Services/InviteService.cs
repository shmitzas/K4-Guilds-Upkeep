namespace K4_Guilds.Services;

public sealed class InviteService
{
	private readonly Dictionary<ulong, (int GuildId, ulong InviterSteamId, DateTime ExpiresAt)> _pendingInvites = [];

	private const int INVITE_EXPIRE_SECONDS = 60;

	public bool HasAnyPendingInvite(ulong inviteeSteamId)
	{
		CleanupExpired();
		return _pendingInvites.ContainsKey(inviteeSteamId);
	}

	public (int GuildId, ulong InviterSteamId)? GetPendingInvite(ulong inviteeSteamId)
	{
		CleanupExpired();

		if (_pendingInvites.TryGetValue(inviteeSteamId, out var invite))
			return (invite.GuildId, invite.InviterSteamId);

		return null;
	}

	public void CreateInvite(int guildId, ulong inviterSteamId, ulong inviteeSteamId)
	{
		CleanupExpired();
		_pendingInvites[inviteeSteamId] = (guildId, inviterSteamId, DateTime.UtcNow.AddSeconds(INVITE_EXPIRE_SECONDS));
	}

	public bool AcceptInvite(ulong inviteeSteamId, int guildId)
	{
		CleanupExpired();

		if (_pendingInvites.TryGetValue(inviteeSteamId, out var invite) && invite.GuildId == guildId)
		{
			_pendingInvites.Remove(inviteeSteamId);
			return true;
		}

		return false;
	}

	public void DeclineInvite(ulong inviteeSteamId)
		=> _pendingInvites.Remove(inviteeSteamId);

	public void RemoveAllInvitesForGuild(int guildId)
	{
		var toRemove = _pendingInvites
			.Where(x => x.Value.GuildId == guildId)
			.Select(x => x.Key)
			.ToArray();

		foreach (var steamId in toRemove)
			_pendingInvites.Remove(steamId);
	}

	private void CleanupExpired()
	{
		var now = DateTime.UtcNow;
		var expired = _pendingInvites
			.Where(x => x.Value.ExpiresAt <= now)
			.Select(x => x.Key)
			.ToArray();

		foreach (var steamId in expired)
			_pendingInvites.Remove(steamId);
	}
}
