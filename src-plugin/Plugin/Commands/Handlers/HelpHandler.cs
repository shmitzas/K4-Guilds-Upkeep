namespace K4_Guilds.Commands.Handlers;

public static class HelpHandler
{
	public static string? Execute(CommandContext ctx)
	{
		ctx.ReplyLines(
			ctx.Localizer["k4.command.help.header"],
			ctx.Localizer["k4.command.help.create"],
			ctx.Localizer["k4.command.help.info"],
			ctx.Localizer["k4.command.help.members"],
			ctx.Localizer["k4.command.help.invite"],
			ctx.Localizer["k4.command.help.accept"],
			ctx.Localizer["k4.command.help.decline"],
			ctx.Localizer["k4.command.help.leave"],
			ctx.Localizer["k4.command.help.kick"],
			ctx.Localizer["k4.command.help.promote"],
			ctx.Localizer["k4.command.help.demote"],
			ctx.Localizer["k4.command.help.deposit"],
			ctx.Localizer["k4.command.help.withdraw"],
			ctx.Localizer["k4.command.help.upgrade"],
			ctx.Localizer["k4.command.help.chat"],
			ctx.Localizer["k4.command.help.perks"],
			ctx.Localizer["k4.command.help.rename"],
			ctx.Localizer["k4.command.help.disband"],
			ctx.Localizer["k4.command.help.top"],
			ctx.Localizer["k4.command.help.footer"]
		);
		return null;
	}
}
