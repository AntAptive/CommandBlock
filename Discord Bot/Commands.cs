using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using static CommandBlock.HttpService;
using static CommandBlock.Utils;
using static CommandBlock.Commands.Commands;

namespace CommandBlock.Commands
{
    internal static class Commands
    {
        public static async Task<DiscordChannel?> GetLogChannel(SlashCommandContext ctx) {
            if (Config.CurrentConfig.loggingChannelId == 0)
                return null;
            return await ctx.Client.GetChannelAsync(Config.CurrentConfig.loggingChannelId);
        }

        public static async Task SendMessageToLogChannel(SlashCommandContext ctx, string message, DiscordColor color)
        {
            DiscordChannel? logChannel = await GetLogChannel(ctx);
            if (logChannel is not null)
                await logChannel.SendMessageAsync(
                    SimpleMessage(
                        message,
                        color));
        }

        public static async Task<bool> CanUserRunCommand(SlashCommandContext ctx)
        {
            DiscordMember member = await ctx.Guild.GetMemberAsync(ctx.User.Id);

            List<string> roleIds = new();
            foreach (var role in member.Roles)
                roleIds.Add(role.Id.ToString());

            bool userIsAdmin = member.Permissions.HasPermission(DiscordPermission.Administrator);

            // Allow anyone with the role or admin perms
            if (!roleIds.Any(Config.CurrentConfig.allowedRoles.Contains) && !userIsAdmin)
            {
                await ctx.RespondAsync(SimpleMessage(
                        "You do not have permission to run this command.",
                        DiscordColor.Red));
                return false;
            }

            return true;
        }

        public class ServerStatsData
        {
            public bool online { get; set; }

            public string gameVersion { get; set; } = "";
            public string uptime { get; set; } = "";

            public int onlinePlayers { get; set; }
            public int maxPlayers { get; set; }
            
            public double tps { get; set; }

            public List<string> playerList = new();
        }

        public static DiscordEmbed SimpleMessage(string message, DiscordColor color)
        {
            DiscordEmbedBuilder embed = new()
            {
                Description = message,
                Color = color
            };

            return embed.Build();
        }

        public static DiscordEmbed ThinkingMessage() =>
            SimpleMessage($"Thinking...", DiscordColor.Blurple);

        public static DiscordEmbed ServerStatsMessage(ServerStatsData data)
        {
            DiscordEmbedBuilder embed = new();

            embed.Title = "Server Stats";

            if (data.online)
            {
                string allPlayers;

                if (data.playerList.Count == 0)
                    allPlayers = "No players online";
                else
                    allPlayers = string.Join("\n", data.playerList);

                embed.Color = DiscordColor.Green;
                embed.Description = $"**IP:** `{Config.CurrentConfig.serverIp}`\n" +
                                    $"**Status:** 🟢 Online\n" +
                                    $"**Version:** {data.gameVersion}";
                embed.AddField("Player List", allPlayers, true);
                embed.AddField("Online/Max Players", $"{data.onlinePlayers} / {data.maxPlayers}", true);
                embed.AddField("Uptime", data.uptime, true);
                embed.AddField("TPS (~20 is best)", data.tps.ToString("0.000"), true);
            }
            else
            {
                embed.Description = "The Minecraft server is currently offline or unreachable.";
                embed.Description = $"**IP:** `{Config.CurrentConfig.serverIp}`\n" +
                                    $"**Status:** 🔴 Offline";
                embed.Color = DiscordColor.NotQuiteBlack;
            }

            return embed.Build();
        }
    }

    #region Bot Management Commands
    internal class SetHttpServer
    {
        [Command("SetHttpServer")]
        [Description("Sets the CommandBlock HTTP server to communicate to")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("CommandBlock Plugin IP & Port (Example: http://127.0.0.1:25580")] string serverIp)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /set_http_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /set_http_server.");

            if (!Uri.IsWellFormedUriString(serverIp, UriKind.Absolute))
            {
                PrintError($"Failed to set {serverIp} as the HTTP server IP since it's not a valid URI.");
                await ctx.RespondAsync(
                    SimpleMessage(
                        "The server IP provided is not a valid URL. Please ensure it starts with `http://` or `https://`.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }


            Config.CurrentConfig.serverHTTPUrl = serverIp;
            Config.SaveConfig(Config.CurrentConfig, "config.json");
            await ctx.RespondAsync(
                SimpleMessage(
                    $"CommandBlock HTTP server set to: {serverIp}",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"CommandBlock HTTP server set to: {serverIp}\n" +
                $"Set by: <@{ctx.User.Id}>",
                DiscordColor.Green);

        }
    }

    internal class SetMcServer
    {
        [Command("SetMcServer")]
        [Description("Sets the IP the bot will display to users on Discord")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("IP address of your Minecraft server")] string serverIp)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /set_mc_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /set_mc_server.");

            Config.CurrentConfig.serverIp = serverIp;
            Config.SaveConfig(Config.CurrentConfig, "config.json");
            await ctx.RespondAsync(
                SimpleMessage(
                    $"Minecraft IP address set to: {serverIp}",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"Minecraft IP address set to: {serverIp}\n" +
                $"Set by: <@{ctx.User.Id}>",
                DiscordColor.Green);

        }
    }

    internal class AddGuild
    {
        [Command("AddGuild")]
        [Description("Adds a guild ID to the list of allowed guilds")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The ID of the guild to add")] string guildId)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /add_guild but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /add_guild.");

            if (Config.CurrentConfig.allowedGuilds.Contains(guildId))
            {
                await ctx.RespondAsync(
                    SimpleMessage(
                        $"Guild ID {guildId} is already in the allowed guilds list.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            Config.CurrentConfig.allowedGuilds.Add(guildId);
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                SimpleMessage(
                    $"Successfully added guild ID {guildId} to the allowed guilds list.",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"Added guild ID {guildId} to the allowed guilds list.\n" +
                $"Added by: <@{ctx.User.Id}>",
                DiscordColor.Green);
        }
    }

    internal class RemoveGuild
    {
        [Command("RemoveGuild")]
        [Description("Removes a guild ID from the list of allowed guilds")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The ID of the guild to remove")] string guildId)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /remove_guild but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /remove_guild.");

            if (!Config.CurrentConfig.allowedGuilds.Contains(guildId))
            {
                await ctx.RespondAsync(
                    SimpleMessage(
                        $"Guild ID {guildId} is not in the allowed guilds list.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            // Leave the guild if the bot's currently in it
            await foreach (var guild in ctx.Client.GetGuildsAsync())
            {
                if (guild.Id.ToString() == guildId)
                {
                    try
                    {
                        await guild.LeaveAsync();
                        Print($"Left guild {guild.Name} (ID: {guild.Id}) as it was removed from the allowed guilds list.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Bot failed to leave guild {guild.Name} (ID: {guild.Id}: {ex.Message}");
                    }
                }
            }

            Config.CurrentConfig.allowedGuilds.Remove(guildId);
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                SimpleMessage(
                    $"Successfully removed guild ID {guildId} from the allowed guilds list.",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"Removed guild ID {guildId} from the allowed guilds list.\n" +
                $"Removed by: <@{ctx.User.Id}>",
                DiscordColor.Green);
        }
    }

    internal class AddRole
    {
        [Command("AddRole")]
        [Description("Adds a role ID to the list of allowed roles for admin commands")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The role to add")] DiscordRole role)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /add_role but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /add_role.");

            if (Config.CurrentConfig.allowedRoles.Contains(role.Id.ToString()))
            {
                await ctx.RespondAsync(
                    SimpleMessage(
                        $"{role.Name} is already in the allowed roles list.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            Config.CurrentConfig.allowedRoles.Add(role.Id.ToString());
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                SimpleMessage(
                    $"Successfully added {role.Name} to the allowed roles list.",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"Successfully added {role.Name} to the allowed roles list.\n" +
                $"Added by: <@{ctx.User.Id}>",
                DiscordColor.Green);
        }
    }

    internal class RemoveRole
    {
        [Command("RemoveRole")]
        [Description("Removes a role ID from the list of allowed roles for admin commands")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The role to remove")] DiscordRole role)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /remove_role but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /remove_role.");

            if (!Config.CurrentConfig.allowedRoles.Contains(role.Id.ToString()))
            {
                await ctx.RespondAsync(
                    SimpleMessage(
                        $"{role.Name} is not in the allowed roles list.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            Config.CurrentConfig.allowedRoles.Remove(role.Id.ToString());
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                SimpleMessage(
                    $"Successfully removed role {role.Name} from the allowed roles list.",
                    DiscordColor.Green),
                    ephemeral: true);

            await SendMessageToLogChannel(ctx,
                $"Removed role {role.Name} from the allowed roles list.\n" +
                $"Removed by: <@{ctx.User.Id}>",
                DiscordColor.Green);
        }
    }

    internal class SetLoggingChannel
    {
        [Command("SetLoggingChannel")]
        [Description("Sets the logging channel for bot logs")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The channel to send logs to")] [ChannelTypes(DiscordChannelType.Text)] DiscordChannel channel)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /set_logging_channel but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /set_logging_channel.");

            try
            {
                Config.CurrentConfig.loggingChannelId = channel.Id;
                Config.SaveConfig(Config.CurrentConfig, "config.json");
            }
            catch (Exception ex)
            {
                PrintError($"Failed to set logging channel to {channel.Name} (ID: {channel.Id}). Exception: {ex.Message}");
                await ctx.RespondAsync(
                    SimpleMessage(
                        "An error occurred while trying to set the logging channel. Please contact server admins.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            Print($"Successfully set logging channel to {channel.Name} (ID: {channel.Id})");

            await ctx.RespondAsync(SimpleMessage($"Successfully saved <#{channel.Id}> as the logging channel!", DiscordColor.Green), true);

            await SendMessageToLogChannel(ctx,
                $"Set logging channel to this channel!\n" +
                $"Set by: <@{ctx.User.Id}>",
                DiscordColor.Green);
        }
    }

    internal class RemoveLoggingChannel
    {
        [Command("RemoveLoggingChannel")]
        [Description("Disables the bot's logging on Discord")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /remove_logging_channel but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /remove_logging_channel.");

            try
            {
                Config.CurrentConfig.loggingChannelId = 0;
                Config.SaveConfig(Config.CurrentConfig, "config.json");
            }
            catch (Exception ex)
            {
                PrintError($"Failed to remove logging channel. Exception: {ex.Message}");
                await ctx.RespondAsync(
                    SimpleMessage(
                        "An error occurred while trying to remove the logging channel. Please contact server admins.",
                        DiscordColor.Red),
                    ephemeral: true);
                return;
            }

            Print("Successfully removed logging channel");

            await ctx.RespondAsync(SimpleMessage($"Successfully removed the logging channel!", DiscordColor.Green), true);
        }
    }
    #endregion

    #region Server Management Commands
    internal class StartServer
    {
        [Command("StartServer")]
        [Description("Starts the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /start_server but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /start_server.");

            string exePath = Config.CurrentConfig.serverExecutablePath;
            if (exePath == "ignore" || string.IsNullOrWhiteSpace(exePath))
            {
                await ctx.RespondAsync(
                    SimpleMessage(
                        "Failed to start server. Please contact server admins. More info is in my log!",
                        DiscordColor.Red),
                    ephemeral: true);
                PrintError("Cannot start server because no executable path is set. Please press R at any time to set the executable path.");

                await SendMessageToLogChannel(ctx,
                    $"Failed to start server. No executable path is set.\nOne can be set by pressing R in the bot's console.\n" +
                    $"Ran by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
                return;
            }

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage(), ephemeral: true);

            // Check the server status to see if it's online already
            string statusUrl = $"{Config.CurrentConfig.serverHTTPUrl}/server/stats";
            string token = Config.CurrentConfig.serverAPIToken;
            HttpResponse status = await SendGET("/server/stats", token, true);

            // The server must not be online to start it
            if (status.status != HttpStatusCode.OK)
            {
                // Try to start the server
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath)
                    });
                }
                // Server failed to start, likely do to no perms or invalid path
                catch (Exception ex)
                {
                    await ctx.EditResponseAsync(
                    SimpleMessage(
                        "Failed to start server. Please contact server admins. More info is in my log!",
                        DiscordColor.Red));

                    PrintError("Failed to start server. " + ex.Message);

                    // Don't log to Discord if no logging channel is set
                    if (Config.CurrentConfig.loggingChannelId == 0)
                        return;

                    await SendMessageToLogChannel(ctx,
                        $"Failed to start server. Server is already online.\n" +
                        $"Ran by: <@{ctx.User.Id}>",
                        DiscordColor.Red);

                    return;
                }

                // Success
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        "Successfully started server!",
                        DiscordColor.Green));
                Print("Successfully started server!");

                await SendMessageToLogChannel(ctx,
                        $"Successfully started server!\n" +
                        $"Ran by: <@{ctx.User.Id}>",
                        DiscordColor.Red);
            }
            // Server is already online
            else
            {
                string message = $"Error starting server.\n" +
                    $"The server is already online!";
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        message,
                        DiscordColor.Red));

                PrintError("There was an error starting the server. The server is already online.");

                if (Config.CurrentConfig.loggingChannelId == 0)
                    return;
                
                await SendMessageToLogChannel(ctx,
                        $"Failed to start server. Server is already online.\n" +
                        $"Ran by: <@{ctx.User.Id}>",
                        DiscordColor.Red);
            }
        }
    }

    internal class StopServer
    {
        [Command("StopServer")]
        [Description("Gracefully stops the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /stop_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /stop_server.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage(), ephemeral: true);

            string url = $"{Config.CurrentConfig.serverHTTPUrl}/server/stop";
            string token = Config.CurrentConfig.serverAPIToken;

            HttpResponse response = await SendPOST("/server/stop", token);

            if (response.status == HttpStatusCode.OK)
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        "Successfully stopped server!",
                        DiscordColor.Green));

                Print("Successfully stopped server!");

                await SendMessageToLogChannel(ctx,
                    "Server was stopped.\n" +
                    $"Ran by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else if (response.status == HttpStatusCode.Unauthorized)
            {
                string message = "Error stopping server.\n" +
                    "I'm not configured properly! Please contact server admins. More info is in my log!";
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        message,
                        DiscordColor.Red));

                PrintError("There was an error stopping the server. The request sent to the Minecraft server was unauthorized. Please ensure your API tokens match.");

                await SendMessageToLogChannel(ctx,
                    "Failed to stop server. The request was unauthorized. Please ensure the bot's API token matches the CommandBlock plugin's API token.\n" +
                    $"Ran by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
            else
            {
                string message = $"Error stopping server.\n" +
                    $"The server may already be offline!";
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        message,
                        DiscordColor.Red));

                PrintError("There was an error stopping the server. The server may already be offline. Status code: " + response.status);

                await SendMessageToLogChannel(ctx,
                    $"Failed to stop server. The server may already be offline. Status code: {response.status}\n" +
                    $"Ran by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }
    #endregion

    #region Server Moderation Commands
    internal class KickPlayer
    {
        [Command("KickPlayer")]
        [Description("Kicks a player from the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The username of the player to kick")] string playerName,
            [Description("The reason for kicking the player")] string reason = "")
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /kick_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /kick_player.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage(), ephemeral: true);

            HttpResponse response = await SendPOST($"/server/player/kick?player={WebUtility.UrlEncode(playerName)}&reason={WebUtility.UrlEncode(reason)}", Config.CurrentConfig.serverAPIToken);

            if (response.status == HttpStatusCode.OK)
            {
                string message =
                    string.IsNullOrWhiteSpace(reason) ? $"Successfully kicked player {playerName}"
                    : $"Successfully kicked player {playerName} for reason: {reason}";

                await ctx.EditResponseAsync(
                    SimpleMessage(
                        message,
                        DiscordColor.Green));
                Print(message);

                await SendMessageToLogChannel(ctx,
                    message + $"\nKicked by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Failed to kick player {playerName}. {response.error}",
                        DiscordColor.Red));

                PrintError($"Failed to kick player {playerName}. Status code: {response.status}");

                await SendMessageToLogChannel(ctx,
                    $"Failed to kick player {playerName}. Status code: {response.status}\n" +
                    $"Kicked by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }

    internal class BanPlayer
    {
        [Command("BanPlayer")]
        [Description("Bans a player from the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The username of the player to ban")] string playerName,
            [Description("The reason for banning the player")] string reason = "")
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /ban_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /ban_player.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage(), ephemeral: true);

            HttpResponse response = await SendPOST($"/server/player/ban?player={WebUtility.UrlEncode(playerName)}&reason={WebUtility.UrlEncode(reason)}", Config.CurrentConfig.serverAPIToken);

            if (response.status == HttpStatusCode.OK)
            {
                string message =
                    string.IsNullOrWhiteSpace(reason) ? $"Successfully banned player {playerName}"
                    : $"Successfully banned player {playerName} for reason: {reason}";

                await ctx.EditResponseAsync(
                    SimpleMessage(
                        message,
                        DiscordColor.Green));

                Print(message);

                await SendMessageToLogChannel(ctx,
                    message + $"\nBanned by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Failed to ban player {playerName}. {response.error}",
                        DiscordColor.Red));

                PrintError($"Failed to ban player {playerName}. Status code: {response.status}");

                await SendMessageToLogChannel(ctx,
                    $"Failed to ban player {playerName}. Status code: {response.status}\n" +
                    $"Banned by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }

    internal class UnbanPlayer
    {
        [Command("UnbanPlayer")]
        [Description("Unbans a player from the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The username of the player to unban")] string playerName)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /unban_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /unban_player.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage());

            HttpResponse response = await SendPOST($"/server/player/unban?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Successfully unbanned player {playerName}",
                        DiscordColor.Green));

                Print($"Successfully unbanned player {playerName}");

                await SendMessageToLogChannel(ctx,
                    $"Successfully unbanned player {playerName}\n" +
                    $"Unbanned by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Failed to unban player {playerName}. {response.error}",
                        DiscordColor.Red));

                PrintError($"Failed to unban player {playerName}. Status code: {response.status}");

                await SendMessageToLogChannel(ctx,
                    $"Failed to unban player {playerName}. Status code: {response.status}\n" +
                    $"Unbanned by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }

    internal class WhitelistPlayer
    {
        [Command("WhitelistPlayer")]
        [Description("Adds a player to the Minecraft server's whitelist")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The username of the player to whitelist")] string playerName)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /whitelist_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /whitelist_player.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage());

            HttpResponse response = await SendPOST($"/server/player/whitelist/add?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Successfully whitelisted player {playerName}",
                        DiscordColor.Green));

                Print($"Successfully whitelisted player {playerName}");

                await SendMessageToLogChannel(ctx,
                    $"Successfully whitelisted player {playerName}\n" +
                    $"Whitelisted by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Failed to whitelist player {playerName}. {response.error}",
                        DiscordColor.Red));

                PrintError($"Failed to whitelist player {playerName}. Status code: {response.status}");

                await SendMessageToLogChannel(ctx,
                    $"Failed to whitelist player {playerName}. Status code: {response.status}\n" +
                    $"Whitelisted by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }

    internal class UnwhitelistPlayer
    {
        [Command("UnwhitelistPlayer")]
        [Description("Removes a player from the Minecraft server's whitelist")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The username of the player to unwhitelist")] string playerName)
        {
            if (await CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /unwhitelist_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /unwhitelist_player.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage());

            HttpResponse response = await SendPOST($"/server/player/whitelist/remove?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Successfully unwhitelisted player {playerName}",
                        DiscordColor.Green));

                Print($"Successfully unwhitelisted player {playerName}");

                await SendMessageToLogChannel(ctx,
                    $"Successfully unwhitelisted player {playerName}\n" +
                    $"Unwhitelisted by: <@{ctx.User.Id}>",
                    DiscordColor.Green);
            }
            else
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        $"Failed to unwhitelist player {playerName}.  {response.error}",
                        DiscordColor.Red));

                PrintError($"Failed to unwhitelist player {playerName}. Status code: {response.status}");

                await SendMessageToLogChannel(ctx,
                    $"Failed to unwhitelist player {playerName}. Status code: {response.status}\n" +
                    $"Unwhitelisted by: <@{ctx.User.Id}>",
                    DiscordColor.Red);
            }
        }
    }
    #endregion

    #region Public Commands
    internal class ServerStats
    {
        [Command("ServerStats")]
        [Description("Gets the Minecraft server's statistics")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx)
        {
            // No permission check, anyone can run this command.
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /server_stats.");

            // Tell the user we're on it
            await ctx.RespondAsync(ThinkingMessage());

            HttpResponse response;

            try
            {
                response = await SendGET("/server/stats", Config.CurrentConfig.serverAPIToken);
            }
            catch (Exception ex)
            {
                await ctx.EditResponseAsync(
                    SimpleMessage(
                        "An error occurred while trying to get the server's stats. Please contact server admins.",
                        DiscordColor.Red));
                PrintError("An exception occurred while trying to get the server's stats: " + ex.Message + "\nPlease make sure the CommandBlock HTTP server is set properly. You can run /set_http_server to do this.");
                return;
            }

            if (response.status != HttpStatusCode.OK)
            {
                // For the user, display that the server is offline. Detailed errors are in the log.
                await ctx.EditResponseAsync(
                    ServerStatsMessage(
                        new Commands.ServerStatsData {
                            online = false
                        }));
                PrintError("Failed to retreive the server's stats. " + response.response);
            }
            else
            {
                Commands.ServerStatsData? serverStats = Newtonsoft.Json.JsonConvert.DeserializeObject<Commands.ServerStatsData>(response.response);
                if (serverStats is null)
                {
                    // For the user, display that the server is offline. Detailed errors are in the log.
                    await ctx.EditResponseAsync(
                        ServerStatsMessage(
                            new Commands.ServerStatsData {
                                online = false
                            }));
                    PrintError("Failed to retreive the server's stats. An internal error occurred.");
                    return;
                }            
                
                // This isn't provided by the plugin, so we set it here.
                serverStats.online = true;

                await ctx.EditResponseAsync(ServerStatsMessage(serverStats));
                Print("Server stats: " + response.response);
            }
        }
    }
    #endregion

}