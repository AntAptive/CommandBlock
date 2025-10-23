![CommandBlock Banner](/readme/banner.jpg)
# CommandBlock
An open-source Discord bot to control a Minecraft server.

*This project is not affiliated with, sponsored, or endorsed by Mojang or Discord.*

### [Check out my other projects!](https://antaptive.com/projects)

### Consider supporting the creator!
[Patreon](https://www.patreon.com/c/antaptive) | [Kofi](https://ko-fi.com/antaptive) | [Merch Store](http://shop.antaptive.com)

## License

   This repository contains two separate projects with different licenses:
   
   - **Minecraft Plugin**: GPL v3 (see `Minecraft Plugin/LICENSE`)
   - **Discord Bot**: MIT (see `Discord Bot/LICENSE`)

## Initial Setup
### Minecraft Plugin
1. Install the .jar file into your server's `plugins` folder.
2. Start the server to generate the `config.yml` file which you can find in `plugins/CommandBlock` and open with a text editor.<br>
*It is expected to see CommandBlock fail to start in your server's log.*
3. Change `api-token` in the `config.yml` file.<br>
**Choose something VERY secure. Anyone with this token can ban, kick, or whitelist any player!**
4. Save changes to `config.yml` and restart your Minecraft server.
### Discord Bot
1. Upon opening the bot's program, you will be prompted to enter the following information:
    * Discord bot token
        * If you don't know how to get your Discord bot token, please check out this tutorial: *(coming soon! for now, please look up how to make a Discord bot)*
    * Bot's primary Discord server ID
    * CommandBlock plugin API token
    * Minecraft server IP
        * Optional but highly recommended
    * Minecraft server executable path
        * Optional but /start_server won't work
        * **WARNING:** This can be set to open ANY program on your machine. Set this with caution.
2. Invite your bot to your primary Discord server and run `/set_http_server` on Discord
    * The HTTP server is **NOT** your Minecraft IP. CommandBlock should have printed the address to your Minecraft server's log. The default is `http://127.0.0.1:25580`. If the Minecraft server and Discord bot are running on two separate machines, you may need to configure your firewall.
    * You will need to be an administrator in your server to run this command. If you're not an admin, get one to run `/add_role` with the ID of a role you want to have permission to run CommandBlock's admin commands.
    * If you do not see any commands available once the bot has started, press `Ctrl + R` in Discord to refresh your Discord client. Re-invite the bot to your server if you still see no commands.

## Commands
### Minecraft
| Command | Purpose |
|:---|:---|
| `/restarthttpserver` | Restarts the CommandBlock HTTP REST API server<br>Useful if the server ever misbehaves |
### Discord
| Command | Purpose | Usage |
|:---|:---|:---|
| `/server_stats` | Gets the Minecraft's server's statistics, showing the server version, player list & count, uptime, and last TPS. <br>**NOTE:** If something is improperly configured, this command will show the server as "offline" when it may not be. All info will be sent to the Discord bot's log. | Everyone
| `/add_guild` | Adds a guild ID to the list of allowed guilds/servers the bot can be in | Admin
| `/add_role` | Adds a role ID to the list of allowed roles for admin commands | Admin
| `/ban_player` | Bans a player from the minecraft server | Admin
| `/unban_player` | Unbans a player from the minecraft server | Admin
| `/kick_player` | Kicks a player from the minecraft server | Admin
| `/set_http_server` | Sets the CommandBlock HTTP server to communicate to | Admin
| `/set_mc_server` | Sets the IP the bot will display to users on Discord<br>This is **only** for display purposes | Admin
| `/start_server` | Starts the Minecraft server | Admin
| `/stop_server` | Gracefully stops the Minecraft server | Admin
| `/whitelist_player` | Adds a player to the Minecraft server's whitelist | Admin
| `/unwhitelist_player` | Removes a player from the Minecraft server's whitelist | Admin

## API
CommandBlock starts an HTTP REST API server that can control your Minecraft server.<br>
All requests to any endpoint must include an API token in the following format:
```
Authorization: Bearer <api_token>
```
| Endpoint | Purpose |
|:---|:---|
| `/server/player/ban` | Bans a user from the Minecraft server
| `/server/player/kick` | Kicks a user from the Minecraft server
| `/server/player/unban` | Unbans a user from the Minecraft server
| `/server/player/whitelist/add` | Whitelists a user from the Minecraft server
| `/server/player/whitelist/remove` | Un-whitelists a user from the Minecraft server
| `/server/stats` | Returns a JSON object of the server's current stats<br>(see below for schema)
| `/server/stop` | Stops the Minecraft server
### `/server/stats` Schema
```scheme
{
    "maxPlayers": "integer",
    "onlinePlayers": "integer",
    "tps": "number",
    "playerList": ["string"],
    "gameVersion": "string",
    "uptime": "string"
}
```