using K4Guilds.Shared;

namespace K4_Guilds.Services;

internal partial class GuildsApiService
{
	public event EventHandler<GuildEventArgs>? GuildCreated
	{
		add => plugin.GuildCreated += value;
		remove => plugin.GuildCreated -= value;
	}

	public event EventHandler<GuildEventArgs>? GuildDisbanded
	{
		add => plugin.GuildDisbanded += value;
		remove => plugin.GuildDisbanded -= value;
	}

	public event EventHandler<GuildMemberEventArgs>? MemberJoined
	{
		add => plugin.MemberJoined += value;
		remove => plugin.MemberJoined -= value;
	}

	public event EventHandler<GuildMemberEventArgs>? MemberLeft
	{
		add => plugin.MemberLeft += value;
		remove => plugin.MemberLeft -= value;
	}

	public event EventHandler<GuildMemberEventArgs>? MemberKicked
	{
		add => plugin.MemberKicked += value;
		remove => plugin.MemberKicked -= value;
	}

	public event EventHandler<GuildMemberEventArgs>? MemberPromoted
	{
		add => plugin.MemberPromoted += value;
		remove => plugin.MemberPromoted -= value;
	}

	public event EventHandler<GuildMemberEventArgs>? MemberDemoted
	{
		add => plugin.MemberDemoted += value;
		remove => plugin.MemberDemoted -= value;
	}

	public event EventHandler<GuildInviteEventArgs>? InviteSent
	{
		add => plugin.InviteSent += value;
		remove => plugin.InviteSent -= value;
	}
}
