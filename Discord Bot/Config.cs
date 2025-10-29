using DSharpPlus.Entities;
using Newtonsoft.Json;
using static CommandBlock.Utils;

namespace CommandBlock
{
    internal class Config
    {
        public List<string> allowedGuilds { get; set; } = new();
        public List<string> allowedRoles { get; set; } = new();

        /// <summary>
        /// The address of the CommandBlock HTTP API server.
        /// </summary>
        public string serverHTTPUrl { get; set; } = "";
        /// <summary>
        /// The IP address of the Minecraft server displayed to users on Discord.
        /// </summary>
        public string serverIp { get; set; } = "Not set :P";

        public string serverExecutablePath { get; set; } = "";

        public string serverAPIToken { get; set; } = "";
        public string botToken { get; set; } = "";

        public ulong loggingChannelId { get; set; } = 0;

        internal static Config CurrentConfig { get; private set; }

        public static string AskForInput(string message, bool optional = false)
        {
            Console.Write($"\n{message}: ");

            var input = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(input)) return input;
            else if (!optional) return AskForInput(message);
            else return "";
        }

        public static void SaveConfig(Config config, string path)
        {
            string json = JsonConvert.SerializeObject(config);
            File.WriteAllText(path, json);
        }

        public static void AskForExecutablePath(Config items)
        {
            string filePath = AskForInput("Please enter the full path to your Minecraft server's executable. (Optional, but /start_server will not work)\nOr type \"ignore\" to never ask this again", true)
                    .Replace("\"", "");

            // Check if the file exists. If the user entered "ignore", skip the check.
            if (!File.Exists(filePath) && !string.IsNullOrWhiteSpace(filePath) && filePath.ToLower() != "ignore")
            {
                PrintError("File does not exist at the specified path.");
                AskForExecutablePath(items);
                return;
            }

            items.serverExecutablePath = filePath;
        }

        public static async Task<Config> LoadConfigAsync()
        {
            string exePath = Path.GetDirectoryName(Environment.ProcessPath);
            string jsonFilePath = Path.Combine(exePath, "config.json");

            if (!File.Exists(jsonFilePath))
            { // Initial setup
                PrintWarn("The config file \"config.json\" does not exist and one will be created in the same folder as the bot's executable.");

                Config newConfig = new();

                newConfig.botToken = AskForInput("Please enter your Discord bot's token");

                // There needs to be at least one allowed guild
                newConfig.allowedGuilds.Add(AskForInput("Please enter the ID of the primary server the bot will be in"));

                newConfig.serverAPIToken = AskForInput("Please enter your CommandBlock plugin API token (set in your plugin's config.yml)");

                string userInputServerIp = AskForInput("Please enter the IP address of your Minecraft server (this is displayed to users on Discord)", true);
                newConfig.serverIp = string.IsNullOrWhiteSpace(userInputServerIp) ? "Not set :P" : userInputServerIp;

                AskForExecutablePath(newConfig);

                Print("Initial setup complete! Please add the bot to your Discord server and run /set_http_server to connect the bot to your Minecraft server.");

                SaveConfig(newConfig, jsonFilePath);

                CurrentConfig = newConfig;
                return newConfig;
            }
            else
            {
                using (StreamReader r = new(jsonFilePath))
                {
                    string json = await r.ReadToEndAsync();
                    Config items = JsonConvert.DeserializeObject<Config>(json);

                    r.Close();

                    if (items == null)
                        throw new Exception("Config file is malformed.");

                    // Validate required fields
                    if (items.allowedGuilds.Count == 0)
                    {
                        items.allowedGuilds.Add(AskForInput("Please enter the ID of the primary server the bot will be in"));
                    }

                    if (string.IsNullOrWhiteSpace(items.botToken))
                    {
                        items.botToken = AskForInput("Please enter your Discord bot's token");
                    }

                    if (string.IsNullOrWhiteSpace(items.serverAPIToken))
                    {
                        items.serverAPIToken = AskForInput("Please enter your CommandBlock plugin API token (set in your plugin's config.yml)");
                    }

                    // This is optional but we still ask the user if it isn't set
                    if (string.IsNullOrWhiteSpace(items.serverExecutablePath))
                    {
                        AskForExecutablePath(items);
                    }
                    // If it's set, check if the file exists
                    else if (!File.Exists(items.serverExecutablePath) && items.serverExecutablePath != "ignore")
                    {
                        PrintError("The Minecraft server executable path set in the config file does not exist.");
                        AskForExecutablePath(items);
                    }

                    SaveConfig(items, jsonFilePath);

                    CurrentConfig = items;
                    return items;
                }
            }
        }
    }
}
