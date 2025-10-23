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

        private static string version = "1.0.0";

        private static DiscordClientBuilder builder;
        private static DiscordClient client;

        private static Config config;

        private static bool isEscapePressed = false;
        private static DateTime lastEscapePress = DateTime.MinValue;

        private static bool isCPressed = false;
        private static DateTime lastCPress = DateTime.MinValue;

        private static bool lastEscapeKeyReady = false;

        static async Task Main(string[] args)
        {
            Console.Title = $"CommandBlock Discord Bot v{version}";

            // Start check for ESC and C key presses
            _ = Task.Run(MonitorForKeypresses);

            PrintRaw("\n\nCommandBlock Discord Bot");
            PrintRaw($"Version {version}\n\n");

            PrintRaw("Press ESC twice quickly to shut down the bot. ");
            PrintRaw("Press C twice quickly to clear the log.\n\n");

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
                            }
                            catch (Exception ex)
                            {
                                PrintError($"Guild {e.Guild.Name} ({e.Guild.Id}) is not in the allowedGuilds list and the bot failed to leave successfully.");
                                return;
                            }
                            PrintWarn($"Guild {e.Guild.Name} ({e.Guild.Id}) is not in the allowedGuilds list and the bot left it successfully.");
                        }
                    });
                });

            // Setup the commands extension
            builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
            {
                extension.AddCommands([
                    typeof(StartServer),
                    typeof(StopServer),
                    typeof(SetHttpServer),
                    typeof(SetMcServer),
                    typeof(ServerStats),
                    typeof(AddGuild),
                    typeof(AddRole),
                    typeof(KickPlayer),
                    typeof(BanPlayer),
                    typeof(UnbanPlayer),
                    typeof(WhitelistPlayer),
                    typeof(UnwhitelistPlayer)
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
                            Print("Log has been cleared.");
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

                await Task.Delay(100); // Reduce CPU usage
            }
        }
    }
}
