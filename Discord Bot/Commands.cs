using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using static CommandBlock.HttpService;
using static CommandBlock.Utils;

namespace CommandBlock.Commands
{
    // TODO: For any commands that rely on code to finish before sending a
    // response, the command should immediately respond with a waiting message
    // and edit its message later.
    internal static class Commands
    {
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
                await ctx.RespondAsync(Commands.SimpleMessage(
                        "You do not have permission to run this command.",
                        DiscordColor.Red,
                        true));
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

        public static DiscordInteractionResponseBuilder SimpleMessage(string message, DiscordColor color, bool ephemeral = false)
        {
            DiscordEmbedBuilder embed = new()
            {
                Description = message,
                Color = color
            };

            var response = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(ephemeral);
            return response;
        }

        public static DiscordInteractionResponseBuilder ServerStatsMessage(ServerStatsData data)
        {
            DiscordInteractionResponseBuilder response = new();
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

            response.AddEmbed(embed);
            return response;
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /set_http_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /set_http_server.");

            if (!Uri.IsWellFormedUriString(serverIp, UriKind.Absolute))
            {
                PrintError($"Failed to set {serverIp} as the HTTP server IP since it's not a valid URI.");
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        "The server IP provided is not a valid URL. Please ensure it starts with `http://` or `https://`.",
                        DiscordColor.Red,
                        true));
                return;
            }


            Config.CurrentConfig.serverHTTPUrl = serverIp;
            Config.SaveConfig(Config.CurrentConfig, "config.json");
            await ctx.RespondAsync(
                Commands.SimpleMessage(
                    $"CommandBlock HTTP server set to: {serverIp}",
                    DiscordColor.Green,
                    true));

        }
    }

    internal class SetMcServer
    {
        [Command("SetMcServer")]
        [Description("Sets the IP the bot will display to users on Discord")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("IP address of your Minecraft server")] string serverIp)
        {
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /set_mc_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /set_mc_server.");

            Config.CurrentConfig.serverIp = serverIp;
            Config.SaveConfig(Config.CurrentConfig, "config.json");
            await ctx.RespondAsync(
                Commands.SimpleMessage(
                    $"Minecraft IP address set to: {serverIp}",
                    DiscordColor.Green,
                    true));

        }
    }

    internal class AddGuild
    {
        [Command("AddGuild")]
        [Description("Adds a guild ID to the list of allowed guilds")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The ID of the guild to add")] string guildId)
        {
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /add_guild but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /add_guild.");

            if (Config.CurrentConfig.allowedGuilds.Contains(guildId))
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Guild ID {guildId} is already in the allowed guilds list.",
                        DiscordColor.Red,
                        true));
                return;
            }

            Config.CurrentConfig.allowedGuilds.Add(guildId);
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                Commands.SimpleMessage(
                    $"Successfully added guild ID {guildId} to the allowed guilds list.",
                    DiscordColor.Green,
                    true));
        }
    }

    internal class AddRole
    {
        [Command("AddRole")]
        [Description("Adds a role ID to the list of allowed roles for admin commands")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx,
            [Description("The ID of the role to add")] string roleId)
        {
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /add_role but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /add_role.");

            if (Config.CurrentConfig.allowedRoles.Contains(roleId))
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Role ID {roleId} is already in the allowed roles list.",
                        DiscordColor.Red,
                        true));
                return;
            }

            Config.CurrentConfig.allowedRoles.Add(roleId);
            Config.SaveConfig(Config.CurrentConfig, "config.json");

            await ctx.RespondAsync(
                Commands.SimpleMessage(
                    $"Successfully added role ID {roleId} to the allowed roles list.",
                    DiscordColor.Green,
                    true));
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /start_server but didn't have permission.");
                return;
            }
            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /start_server.");

            // Check the server status to see if it's online already
            string statusUrl = $"{Config.CurrentConfig.serverHTTPUrl}/server/stats";
            string token = Config.CurrentConfig.serverAPIToken;
            HttpResponse status = await SendGET("/server/stats", token);

            // The server must not be online to start it
            if (status.status != HttpStatusCode.OK)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Config.CurrentConfig.serverExecutablePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(Config.CurrentConfig.serverExecutablePath)
                    });
                }
                catch (Exception ex)
                {
                    await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        "Failed to start server. Please contact server admins. More info is in my log!",
                        DiscordColor.Red,
                        true));
                    PrintError("Failed to start server. " + ex.Message);
                    return;
                }

                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        "Successfully started server!",
                        DiscordColor.Green,
                        true));
                Print("Successfully started server!");
            }
            else
            {
                string message = $"Error starting server.\n" +
                    $"The server is already online!";
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        message,
                        DiscordColor.Red,
                        true));
                PrintError("There was an error starting the server. The server is already online.");
            }
        }
    }

    internal class StopServer
    {
        [Command("StopServer")]
        [Description("Gracefully stops the Minecraft server")]
        public static async ValueTask SlashOnlyAsync(SlashCommandContext ctx)
        {
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /stop_server but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /stop_server.");

            string url = $"{Config.CurrentConfig.serverHTTPUrl}/server/stop";
            string token = Config.CurrentConfig.serverAPIToken;

            HttpResponse response = await SendPOST("/server/stop", token);

            if (response.status == HttpStatusCode.OK)
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        "Successfully stopped server!",
                        DiscordColor.Green,
                        true));
                Print("Successfully stopped server!");
            }
            else if (response.status == HttpStatusCode.Unauthorized)
            {
                string message = "Error stopping server.\n" +
                    "I'm not configured properly! Please contact server admins. More info is in my log!";
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        message,
                        DiscordColor.Red,
                        true));
                PrintError("There was an error stopping the server. The request sent to the Minecraft server was unauthorized. Please ensure your API tokens match.");
            }
            else
            {
                string message = $"Error stopping server.\n" +
                    $"The server may already be offline!";
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        message,
                        DiscordColor.Red,
                        true));
                PrintError("There was an error stopping the server. The server may already be offline. Status code: " + response.status);
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /kick_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /kick_player.");

            HttpResponse response = await SendPOST($"/server/player/kick?player={WebUtility.UrlEncode(playerName)}&reason={WebUtility.UrlEncode(reason)}", Config.CurrentConfig.serverAPIToken);

            if (response.status == HttpStatusCode.OK)
            {
                string message =
                    string.IsNullOrWhiteSpace(reason) ? $"Successfully kicked player {playerName}"
                    : $"Successfully kicked player {playerName} for reason: {reason}";

                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        message,
                        DiscordColor.Green,
                        true));
                Print(message);
            }
            else
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Failed to kick player {playerName}. {response.error}",
                        DiscordColor.Red,
                        true));
                PrintError($"Failed to kick player {playerName}. Status code: {response.status}");
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /ban_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /ban_player.");

            HttpResponse response = await SendPOST($"/server/player/ban?player={WebUtility.UrlEncode(playerName)}&reason={WebUtility.UrlEncode(reason)}", Config.CurrentConfig.serverAPIToken);

            if (response.status == HttpStatusCode.OK)
            {
                string message =
                    string.IsNullOrWhiteSpace(reason) ? $"Successfully banned player {playerName}"
                    : $"Successfully banned player {playerName} for reason: {reason}";

                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        message,
                        DiscordColor.Green,
                        true));
                Print(message);
            }
            else
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Failed to ban player {playerName}. {response.error}",
                        DiscordColor.Red,
                        true));
                PrintError($"Failed to ban player {playerName}. Status code: {response.status}");
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /unban_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /unban_player.");

            HttpResponse response = await SendPOST($"/server/player/unban?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Successfully unbanned player {playerName}",
                        DiscordColor.Green,
                        true));
                Print($"Successfully unbanned player {playerName}");
            }
            else
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Failed to unban player {playerName}. {response.error}",
                        DiscordColor.Red,
                        true));
                PrintError($"Failed to unban player {playerName}. Status code: {response.status}");
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /whitelist_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /whitelist_player.");

            HttpResponse response = await SendPOST($"/server/player/whitelist/add?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Successfully whitelisted player {playerName}",
                        DiscordColor.Green,
                        true));
                Print($"Successfully whitelisted player {playerName}");
            }
            else
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Failed to whitelist player {playerName}. {response.error}",
                        DiscordColor.Red,
                        true));
                PrintError($"Failed to whitelist player {playerName}. Status code: {response.status}");
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
            if (await Commands.CanUserRunCommand(ctx) == false)
            {
                Print($"{ctx.User.Username} (ID: {ctx.User.Id}) tried to run /unwhitelist_player but didn't have permission.");
                return;
            }

            Print($"{ctx.User.Username} (ID: {ctx.User.Id}) is running /unwhitelist_player.");

            HttpResponse response = await SendPOST($"/server/player/whitelist/remove?player={WebUtility.UrlEncode(playerName)}", Config.CurrentConfig.serverAPIToken);
            if (response.status == HttpStatusCode.OK)
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Successfully unwhitelisted player {playerName}",
                        DiscordColor.Green,
                        true));
                Print($"Successfully unwhitelisted player {playerName}");
            }
            else
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        $"Failed to unwhitelist player {playerName}.  {response.error}",
                        DiscordColor.Red,
                        true));
                PrintError($"Failed to unwhitelist player {playerName}. Status code: {response.status}");
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

            HttpResponse response;

            try
            {
                response = await SendGET("/server/stats", Config.CurrentConfig.serverAPIToken);
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync(
                    Commands.SimpleMessage(
                        "An error occurred while trying to get the server's stats. Please contact server admins.",
                        DiscordColor.Red,
                        true));
                PrintError("An exception occurred while trying to get the server's stats: " + ex.Message + "\nPlease make sure the CommandBlock HTTP server is set properly. You can run /set_http_server to do this.");
                return;
            }

            if (response.status != HttpStatusCode.OK)
            {
                // For the user, display that the server is offline. Detailed errors are in the log.
                await ctx.RespondAsync(
                    Commands.ServerStatsMessage(
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
                    await ctx.RespondAsync(
                        Commands.ServerStatsMessage(
                            new Commands.ServerStatsData {
                                online = false
                            }));
                    PrintError("Failed to retreive the server's stats. An internal error occurred.");
                    return;
                }            
                
                // This isn't provided by the plugin, so we set it here.
                serverStats.online = true;

                await ctx.RespondAsync(Commands.ServerStatsMessage(serverStats));
                Print("Server stats: " + response.response);
            }
        }
    }
    #endregion

}