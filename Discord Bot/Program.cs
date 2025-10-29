using CommandBlock.Commands;
using DSharpPlus;
using DSharpPlus.Commands;
using System.Runtime.InteropServices;
using static CommandBlock.Utils;

namespace CommandBlock
{
    internal class Program
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

        private delegate bool HandlerRoutine(int ctrlType);

        private static string version = "1.1.1";

        private static DiscordClientBuilder builder;
        private static DiscordClient client;

        private static Config config;

        private static bool isEscapePressed = false;
        private static DateTime lastEscapePress = DateTime.MinValue;
        private static bool isCPressed = false;
        private static DateTime lastCPress = DateTime.MinValue;
        private static bool isRPressed = false;
        private static DateTime lastRPress = DateTime.MinValue;

        private static bool lastEscapeKeyReady = false;

        static async Task Main(string[] args)
        {
            Console.Title = $"CommandBlock Discord Bot v{version}";

            // Start check for ESC and C key presses
            _ = Task.Run(MonitorForKeypresses);

            PrintRaw("\n\nCommandBlock Discord Bot");
            PrintRaw($"Version {version}\n\n");

            PrintRaw("Press ESC twice quickly to shut down the bot. ");
            PrintRaw("Press C twice quickly to clear the log");
            PrintRaw("Press R to change the server executable path.\n\n");

            Print("Loading config...");

            try
            {
                config = await Config.LoadConfigAsync();
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load config: {ex.Message}");
                PrintRaw("Make sure that your config is accessible and not malformed.");
                PrintRaw("Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(1);
            }

            // Start building the bot
            builder = DiscordClientBuilder.CreateDefault(config.botToken, DiscordIntents.AllUnprivileged)
                .ConfigureEventHandlers(eventHandlers =>
                {
                    // When the bot joins a guild
                    eventHandlers.HandleGuildCreated(async (client, e) =>
                    {
                        Print($"Joined guild: {e.Guild.Name} (ID: {e.Guild.Id})");

                        if (!config.allowedGuilds.Contains(e.Guild.Id.ToString()))
                        {
                            try
                            {
                                await e.Guild.LeaveAsync();
                                PrintWarn($"Guild {e.Guild.Name} (ID: {e.Guild.Id}) is not in the allowedGuilds list and the bot left it successfully.");
                            }
                            catch (Exception ex)
                            {
                                PrintError($"Guild {e.Guild.Name} (ID: {e.Guild.Id}) is not in the allowedGuilds list and the bot failed to leave successfully.");
                            }
                        }
                    });
                });

            // Setup the commands extension
            builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
            {
                // holy goliath
                extension.AddCommands([
                    typeof(StartServer),
                    typeof(StopServer),
                    typeof(SetHttpServer),
                    typeof(SetMcServer),
                    typeof(ServerStats),
                    typeof(AddGuild),
                    typeof(RemoveGuild),
                    typeof(AddRole),
                    typeof(RemoveRole),
                    typeof(SetLoggingChannel),
                    typeof(RemoveLoggingChannel),
                    typeof(KickPlayer),
                    typeof(BanPlayer),
                    typeof(UnbanPlayer),
                    typeof(WhitelistPlayer),
                    typeof(UnwhitelistPlayer),
                    ]);
            });

            client = builder.Build();
            Print("Built Discord client.");
            Print("Connecting to Discord...");

            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                PrintError("Failed to connect to Discord.");
                
                while (true)
                {
                    Console.Write("Would you like to update the bot token? (y/n): ");
                    string input = Console.ReadLine()?.Trim().ToLower();

                    if (input == "y" || input == "yes")
                    {
                        Config.CurrentConfig.botToken = Config.AskForInput("Please enter your Discord bot's token");
                        Config.SaveConfig(Config.CurrentConfig, "config.json");
                        PrintRaw("Your bot token has been saved. Please restart CommandBlock.\nPress any key to continue.");
                        Console.ReadKey();
                        Environment.Exit(1);
                    }
                    else if (input == "n" || input == "no")
                        Environment.Exit(1);
                    else
                        Console.WriteLine("Invalid input. Please enter 'y' or 'n'.");
                }
            }

            Print("Connected.");

            // Check if the bot is in any guilds it shouldn't be in
            await foreach (var guild in client.GetGuildsAsync())
            {
                if (!config.allowedGuilds.Contains(guild.Id.ToString()))
                {
                    try
                    {
                        await guild.LeaveAsync();
                        PrintWarn($"Guild {guild.Name} (ID: {guild.Id}) is not in the allowedGuilds list and the bot left it successfully.");
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Guild {guild.Name} (ID: {guild.Id}) is not in the allowedGuilds list and the bot failed to leave successfully.");
                    }
                }
                else
                {
                    Print($"Verified membership in allowed guild: {guild.Name} (ID: {guild.Id})");
                }
            }

            // Wait indefinitely
            await Task.Delay(-1);
        }

        private static async Task MonitorForKeypresses()
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        if (lastEscapeKeyReady)
                        {
                            Print("Exiting...");
                            await Task.Delay(500);
                            Environment.Exit(0);
                        }
                        else if (!isEscapePressed)
                        {
                            isEscapePressed = true;
                            lastEscapePress = DateTime.Now;
                            PrintWarn("Press ESC again to shut down CommandBlock Discord Bot.");
                        }
                        else if ((DateTime.Now - lastEscapePress).TotalSeconds <= 1)
                        {
                            PrintWarn("Shutting down CommandBlock Discord Bot...");

                            await client.DisconnectAsync();

                            Print("CommandBlock Discord Bot has successfully shut down. Press ESC again to exit.");
                            lastEscapeKeyReady = true;
                        }
                    }
                    else if (key.Key == ConsoleKey.C)
                    {
                        if (!isCPressed)
                        {
                            isCPressed = true;
                            lastCPress = DateTime.Now;
                            PrintWarn("Press C again to clear this log.");
                        }
                        else if ((DateTime.Now - lastCPress).TotalSeconds <= 1)
                        {
                            Console.Clear();
                            
                            PrintRaw("Press ESC twice quickly to shut down the bot. ");
                            PrintRaw("Press C twice quickly to clear the log");
                            PrintRaw("Press R to change the server executable path.\n\n");

                            Print("Log has been cleared.");
                        }
                    }
                    else if (key.Key == ConsoleKey.R)
                    {
                        if (!isRPressed)
                        {
                            isRPressed = true;
                            lastRPress = DateTime.Now;
                            PrintWarn("WARNING: This will temporarily take the bot offline! Press R again twice quickly to change the server executable path.");
                        }
                        else if ((DateTime.Now - lastRPress).TotalSeconds <= 1)
                        {
                            PrintWarn("Disconnecting from Discord...");
                            await client.DisconnectAsync();
                            Print("Bot disconnected.");
                            Config.AskForExecutablePath(Config.CurrentConfig);
                            await client.ConnectAsync();
                        }
                    }
                }

                // Reset escape flag if more than 1 second has passed
                if (isEscapePressed && (DateTime.Now - lastEscapePress).TotalSeconds > 1)
                {
                    isEscapePressed = false;
                }

                if (isCPressed && (DateTime.Now - lastCPress).TotalSeconds > 1)
                {
                    isCPressed = false;
                }

                if (isRPressed && (DateTime.Now - lastRPress).TotalSeconds > 1)
                {
                    isRPressed = false;
                }

                await Task.Delay(100); // Reduce CPU usage
            }
        }
    }
}
