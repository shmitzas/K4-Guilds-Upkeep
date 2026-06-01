namespace K4_Guilds.Config;

public sealed class CommandsConfig
{
	public MainCommandSettings MainCommand { get; set; } = new();
	public GuildChatCommandSettings GuildChat { get; set; } = new();
	public SubCommandSettings Create { get; set; } = new() { Name = "create", Aliases = ["c"] };
	public SubCommandSettings Disband { get; set; } = new() { Name = "disband", Aliases = ["delete"] };
	public SubCommandSettings Invite { get; set; } = new() { Name = "invite", Aliases = ["inv", "i"] };
	public SubCommandSettings Accept { get; set; } = new() { Name = "accept", Aliases = ["a"] };
	public SubCommandSettings Decline { get; set; } = new() { Name = "decline", Aliases = ["deny", "d"] };
	public SubCommandSettings Leave { get; set; } = new() { Name = "leave", Aliases = ["quit", "l"] };
	public SubCommandSettings Kick { get; set; } = new() { Name = "kick", Aliases = ["k"] };
	public SubCommandSettings Promote { get; set; } = new() { Name = "promote", Aliases = ["prom"] };
	public SubCommandSettings Demote { get; set; } = new() { Name = "demote", Aliases = ["dem"] };
	public SubCommandSettings Info { get; set; } = new() { Name = "info", Aliases = ["status"] };
	public SubCommandSettings Members { get; set; } = new() { Name = "members", Aliases = ["list", "m"] };
	public SubCommandSettings Deposit { get; set; } = new() { Name = "deposit", Aliases = ["dep"] };
	public SubCommandSettings Withdraw { get; set; } = new() { Name = "withdraw", Aliases = ["wd"] };
	public SubCommandSettings Upgrade { get; set; } = new() { Name = "upgrade", Aliases = ["up"] };
	public SubCommandSettings Chat { get; set; } = new() { Name = "chat", Aliases = ["gc", "c"] };
	public SubCommandSettings Perks { get; set; } = new() { Name = "perks", Aliases = ["perk", "p"] };
	public RenameCommandSettings Rename { get; set; } = new() { Name = "rename", Aliases = ["rn"] };
	public SubCommandSettings Top { get; set; } = new() { Name = "top", Aliases = ["leaderboard", "lb"] };
	public SubCommandSettings Help { get; set; } = new() { Name = "help", Aliases = ["h", "?"] };

	private Dictionary<string, SubCommandSettings>? _commandMap;

	public SubCommandSettings? FindSubCommand(string input)
	{
		_commandMap ??= BuildCommandMap();
		return _commandMap.GetValueOrDefault(input.ToLowerInvariant());
	}

	private Dictionary<string, SubCommandSettings> BuildCommandMap()
	{
		var map = new Dictionary<string, SubCommandSettings>(StringComparer.OrdinalIgnoreCase);
		var allCommands = new SubCommandSettings[] { Create, Disband, Invite, Accept, Decline, Leave, Kick, Promote, Demote, Info, Members, Deposit, Withdraw, Upgrade, Chat, Perks, Rename, Top, Help };

		foreach (var cmd in allCommands)
		{
			if (!cmd.Enabled) continue;
			map[cmd.Name] = cmd;
			foreach (var alias in cmd.Aliases)
				map[alias] = cmd;
		}
		return map;
	}
}

public sealed class MainCommandSettings
{
	public string Command { get; set; } = "guild";
	public List<string> Aliases { get; set; } = ["g", "guilds", "clan", "clans"];
	public string Permission { get; set; } = "";
}

public class SubCommandSettings
{
	public bool Enabled { get; set; } = true;
	public string Name { get; set; } = "";
	public List<string> Aliases { get; set; } = [];
	public string Permission { get; set; } = "";
}

public sealed class RenameCommandSettings : SubCommandSettings
{
	/// <summary>Cost to rename guild (0 = free)</summary>
	public long Cost { get; set; } = 1000;
}

public sealed class GuildChatCommandSettings
{
	public bool Enabled { get; set; } = true;
	public string Command { get; set; } = "gc";
	public List<string> Aliases { get; set; } = ["guildchat"];
	public string Permission { get; set; } = "";
}
