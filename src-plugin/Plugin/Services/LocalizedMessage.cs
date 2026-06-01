using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace K4_Guilds.Services;

/// <summary>Wraps a translation key so it gets resolved through the localizer when used as a message argument</summary>
public readonly record struct LocalizedArg(string Key);

/// <summary>Represents a localized message with key and optional parameters</summary>
public readonly record struct LocalizedMessage(string Key, params object[] Args)
{
	public static LocalizedMessage Simple(string key) => new(key);
	public static LocalizedMessage WithArgs(string key, params object[] args) => new(key, args);

	/// <summary>Format the message with a localizer and prefix</summary>
	public string Format(ILocalizer localizer)
	{
		var prefix = localizer["k4.general.prefix"];
		string text;
		if (Args.Length > 0)
		{
			var resolvedArgs = new object[Args.Length];
			for (int i = 0; i < Args.Length; i++)
				resolvedArgs[i] = Args[i] is LocalizedArg la ? localizer[la.Key] : Args[i];
			text = localizer[Key, resolvedArgs];
		}
		else
		{
			text = localizer[Key];
		}
		return $"{prefix} {text}";
	}

	/// <summary>Send the formatted message to a player</summary>
	public void Send(IPlayer player)
	{
		var localizer = Plugin.Core.Translation.GetPlayerLocalizer(player);
		var formatted = Format(localizer);
		Plugin.Core.Scheduler.NextWorldUpdate(() => player.SendChat(formatted));
	}

	/// <summary>Send the formatted message to a player (immediate, must be on main thread)</summary>
	public void SendImmediate(IPlayer player, ILocalizer localizer)
	{
		player.SendChat(Format(localizer));
	}

	/// <summary>Send the formatted message to a player (immediate, gets localizer automatically)</summary>
	public void SendImmediate(IPlayer player)
	{
		var localizer = Plugin.Core.Translation.GetPlayerLocalizer(player);
		player.SendChat(Format(localizer));
	}
}
