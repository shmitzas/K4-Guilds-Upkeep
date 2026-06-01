using K4_Guilds.Commands.Handlers;
using K4_Guilds.Config;
using K4_Guilds.Services;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace K4_Guilds.Commands;

using static LocalizedMessage;

public sealed class GuildCommands(Plugin plugin, GuildService guildService, UpgradeService upgradeService)
{
    public void RegisterCommands()
    {
        // Register main guild command
        var mainCmd = plugin.Commands.MainCommand;
        Plugin.Core.Command.RegisterCommand(mainCmd.Command, OnGuildCommand);
        foreach (var alias in mainCmd.Aliases)
            Plugin.Core.Command.RegisterCommand(alias, OnGuildCommand);

        // Register standalone guild chat command (/gc)
        var gcCmd = plugin.Commands.GuildChat;
        if (gcCmd.Enabled)
        {
            Plugin.Core.Command.RegisterCommand(gcCmd.Command, OnGuildChatCommand);
            foreach (var alias in gcCmd.Aliases)
                Plugin.Core.Command.RegisterCommand(alias, OnGuildChatCommand);
        }
    }

    private void OnGuildChatCommand(ICommandContext cmdCtx)
    {
        var player = cmdCtx.Sender;
        if (player == null || !player.IsValid) return;

        var gcCmd = plugin.Commands.GuildChat;
        if (!string.IsNullOrEmpty(gcCmd.Permission) && !Plugin.Core.Permission.PlayerHasPermission(player.SteamID, gcCmd.Permission))
        {
            Simple("k4.command.error.no_permission").SendImmediate(player);
            return;
        }

        var ctx = new CommandContext(plugin, guildService, upgradeService, player, cmdCtx.Args.ToArray());

        Task.Run(async () =>
        {
            try
            {
                // Preload guild for /gc command
                var guild = await guildService.GetPlayerGuildAsync(player.SteamID);
                if (guild == null)
                {
                    Simple("k4.guild.error.not_in_guild").Send(player);
                    return;
                }
                ctx.Guild = guild;

                var result = await ChatHandler.ExecuteAsync(ctx);
                if (result != null)
                    Plugin.Core.Scheduler.NextWorldUpdate(() => player.SendChat(result));
            }
            catch (Exception ex)
            {
                Plugin.Core.Logger.LogError(ex, "Error in guild chat command");
                Plugin.Core.Scheduler.NextWorldUpdate(() => player.SendChat("[Guild] An error occurred."));
            }
        });
    }

    private void OnGuildCommand(ICommandContext cmdCtx)
    {
        var player = cmdCtx.Sender;
        if (player == null || !player.IsValid) return;

        // Check main command permission
        var mainCmd = plugin.Commands.MainCommand;
        if (!string.IsNullOrEmpty(mainCmd.Permission) && !Plugin.Core.Permission.PlayerHasPermission(player.SteamID, mainCmd.Permission))
        {
            Simple("k4.command.error.no_permission").SendImmediate(player);
            return;
        }

        var args = cmdCtx.Args.ToList();
        var subCommandInput = args.Count > 0 ? args[0] : "help";
        var subArgs = args.Count > 1 ? [.. args.Skip(1)] : Array.Empty<string>();

        var subCommand = plugin.Commands.FindSubCommand(subCommandInput);
        if (subCommand == null || !subCommand.Enabled)
        {
            subCommand = plugin.Commands.Help;
            subArgs = [];
        }

        // Check permission
        if (!string.IsNullOrEmpty(subCommand.Permission) && !Plugin.Core.Permission.PlayerHasPermission(player.SteamID, subCommand.Permission))
        {
            Simple("k4.command.error.no_permission").SendImmediate(player);
            return;
        }

        var ctx = new CommandContext(plugin, guildService, upgradeService, player, subArgs);

        Task.Run(async () =>
        {
            try
            {
                // Check RequireGuild before dispatch
                if (RequiresGuild(subCommand))
                {
                    var guild = await guildService.GetPlayerGuildAsync(player.SteamID);
                    if (guild == null)
                    {
                        Simple("k4.guild.error.not_in_guild").Send(player);
                        return;
                    }
                    ctx.Guild = guild;
                }

                var result = await DispatchAsync(subCommand, ctx);
                if (result != null)
                    Plugin.Core.Scheduler.NextWorldUpdate(() => player.SendChat(result));
            }
            catch (Exception ex)
            {
                Plugin.Core.Logger.LogError(ex, "Error in guild command: {SubCommand}", subCommand.Name);
                Plugin.Core.Scheduler.NextWorldUpdate(() => player.SendChat("[Guild] An error occurred."));
            }
        });
    }

    private bool RequiresGuild(SubCommandSettings cmd)
    {
        var c = plugin.Commands;
        // Commands that don't require being in a guild
        return cmd != c.Create && cmd != c.Accept && cmd != c.Decline && cmd != c.Help && cmd != c.Top;
    }

    private async Task<string?> DispatchAsync(SubCommandSettings cmd, CommandContext ctx)
    {
        var c = plugin.Commands;
        return cmd switch
        {
            _ when cmd == c.Create => await CreateHandler.ExecuteAsync(ctx),
            _ when cmd == c.Disband => await DisbandHandler.ExecuteAsync(ctx),
            _ when cmd == c.Invite => await InviteHandler.ExecuteAsync(ctx),
            _ when cmd == c.Accept => await AcceptHandler.ExecuteAsync(ctx),
            _ when cmd == c.Decline => DeclineHandler.Execute(ctx),
            _ when cmd == c.Leave => await LeaveHandler.ExecuteAsync(ctx),
            _ when cmd == c.Kick => await KickHandler.ExecuteAsync(ctx),
            _ when cmd == c.Promote => await PromoteHandler.ExecuteAsync(ctx),
            _ when cmd == c.Demote => await DemoteHandler.ExecuteAsync(ctx),
            _ when cmd == c.Info => await InfoHandler.ExecuteAsync(ctx),
            _ when cmd == c.Members => await MembersHandler.ExecuteAsync(ctx),
            _ when cmd == c.Deposit => await DepositHandler.ExecuteAsync(ctx),
            _ when cmd == c.Withdraw => await WithdrawHandler.ExecuteAsync(ctx),
            _ when cmd == c.Upgrade => await UpgradeHandler.ExecuteAsync(ctx),
            _ when cmd == c.Chat => await ChatHandler.ExecuteAsync(ctx),
            _ when cmd == c.Perks => await PerksHandler.ExecuteAsync(ctx),
            _ when cmd == c.Rename => await RenameHandler.ExecuteAsync(ctx, c.Rename),
            _ when cmd == c.Top => await TopHandler.ExecuteAsync(ctx),
            _ => HelpHandler.Execute(ctx)
        };
    }
}
