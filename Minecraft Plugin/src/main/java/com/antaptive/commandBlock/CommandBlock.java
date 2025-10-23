package com.antaptive.commandBlock;

import com.destroystokyo.paper.profile.PlayerProfile;
import com.google.gson.Gson;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import io.papermc.paper.ban.BanListType;
import net.kyori.adventure.text.Component;
import net.kyori.adventure.text.format.NamedTextColor;

import org.bukkit.Bukkit;
import org.bukkit.OfflinePlayer;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;
import org.bukkit.plugin.Plugin;
import org.bukkit.plugin.java.JavaPlugin;
import org.jetbrains.annotations.NotNull;

import com.earth2me.essentials.*;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.List;

public final class CommandBlock extends JavaPlugin {

    private HttpServer server;
    private String apiToken;

    private Essentials essentials;

    private long serverStartTime;

    @Override
    public void onEnable() {
        // Plugin startup logic
        saveDefaultConfig();

        // Make sure config is set properly

        // Don't allow the default token "your-secret-token-here"
        if (!getConfig().contains("api-token") || Objects.equals(getConfig().getString("api-token"), "your-secret-token-here")) {
            getLogger().severe("CommandBlock has failed to start: An API token has not been set.");
            getServer().getPluginManager().disablePlugin(this);
            return;
        }

        if (!getConfig().contains("port")) {
            getLogger().severe("CommandBlock has failed to start: A port has not been set.");
            getServer().getPluginManager().disablePlugin(this);
            return;
        }

        apiToken = getConfig().getString("api-token");

        serverStartTime = System.currentTimeMillis();

        Plugin ess = Bukkit.getPluginManager().getPlugin("Essentials");
        if (ess != null && ess.getPluginMeta().getVersion().compareTo("2.21.0") < 0) {
            getLogger().warning("EssentialsX version too old. We expect at least 2.21.0. Some features have been disabled.");
        }
        else if (ess != null) {
            getLogger().info("EssentialsX has been found!");
            boolean hasEssentials = Bukkit.getPluginManager().isPluginEnabled("Essentials");
            essentials = hasEssentials ? (Essentials) Bukkit.getPluginManager().getPlugin("Essentials") : null;
        }
        else {
            getLogger().info("EssentialsX was not found. Some features have been disabled.");
        }

        this.getCommand("restarthttpserver").setExecutor(new RestartHTTPServer());

        StartHTTPServer();
    }

    @Override
    public void onDisable() {
        // Plugin shutdown logic
        StopHTTPServer();
    }

    public String getFormattedUptime() {
        long uptimeMillis = System.currentTimeMillis() - serverStartTime;
        long seconds = uptimeMillis / 1000;

        long days = seconds / 86400;
        long hours = (seconds % 86400) / 3600;
        long minutes = (seconds % 3600) / 60;
        long secs = seconds % 60;

        return String.format("%dd %dh %dm %ds", days, hours, minutes, secs);
    }

    public void StartHTTPServer() {
        if (server != null) {
            server.stop(0);
            getLogger().info("HTTP server has stopped and is being restarted");
        }

        int port = getConfig().getInt("port");
        String bindAddress = getConfig().getString("bind-address", "127.0.0.1");

        try {
            server = HttpServer.create(new InetSocketAddress(bindAddress, port), 0);

            // Server control endpoints
            server.createContext("/server/stop", exchange -> {
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Stop command received from " + exchange.getRemoteAddress());

                Bukkit.getScheduler().runTask(this, () -> {
                    Bukkit.dispatchCommand(Bukkit.getConsoleSender(), "stop");
                });

                sendResponse(exchange, 200, "{\"success\": true}");
            });

            // Player management endpoints
            server.createContext("/server/player/kick", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Kick Player command received from " + exchange.getRemoteAddress());

                // Parse query parameters
                String query = exchange.getRequestURI().getQuery();
                if (query == null || query.isEmpty()) {
                    sendResponse(exchange, 400, "{\"error\": \"Missing query parameters\"}");
                    getLogger().warning("Request was denied for missing query parameters.");
                    return;
                }

                Map<String, String> params = new HashMap<>();
                for (String param : query.split("&")) {
                    String[] pair = param.split("=", 2);
                    if (pair.length == 2) {
                        try {
                            params.put(pair[0], java.net.URLDecoder.decode(pair[1], StandardCharsets.UTF_8));
                        } catch (Exception e) {
                            // If decoding fails, use the raw value
                            params.put(pair[0], pair[1]);
                        }
                    }
                }

                String playerName = params.get("player");
                String reason = params.getOrDefault("reason", "");

                if (playerName == null || playerName.isBlank()) {
                    sendResponse(exchange, 400, "{\"error\": \"A user was not specified. Please specify a 'player' parameter.\"}");
                    getLogger().warning("Request was denied for missing a 'player' parameter.");
                    return;
                }

                try {
                    Player player = Bukkit.getPlayer(playerName);

                    if (player != null) {
                        Bukkit.getScheduler().runTask(this, () -> {
                            Component message = Component.text("You have been kicked.", NamedTextColor.RED);

                            if (!reason.isBlank()) {
                                message = message.append(Component.text("\n" + reason, NamedTextColor.YELLOW));
                            }

                            Bukkit.getPlayer(playerName).kick(message);
                        });

                        getLogger().info(playerName + " was kicked.");
                        sendResponse(exchange, 200, "{\"success\": true}");
                    }
                    else {
                        sendResponse(exchange, 400, "{\"error\": \"The player is not online\"}");
                        getLogger().warning("Request tried to kick " + playerName + " but the player was not online.");
                    }
                }
                catch (Exception e) {
                    sendResponse(exchange, 400, "{\"error\": \"" + e.getMessage() + "\"}");
                    getLogger().severe("Error kicking " + playerName + ": " + e.getMessage());
                }

            });

            server.createContext("/server/player/ban", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Ban Player command received from " + exchange.getRemoteAddress());

                // Parse query parameters
                String query = exchange.getRequestURI().getQuery();
                if (query == null || query.isEmpty()) {
                    sendResponse(exchange, 400, "{\"error\": \"Missing query parameters\"}");
                    getLogger().warning("Request was denied for missing query parameters.");
                    return;
                }

                Map<String, String> params = new HashMap<>();
                for (String param : query.split("&")) {
                    String[] pair = param.split("=", 2);
                    if (pair.length == 2) {
                        try {
                            params.put(pair[0], java.net.URLDecoder.decode(pair[1], StandardCharsets.UTF_8));
                        } catch (Exception e) {
                            // If decoding fails, use the raw value
                            params.put(pair[0], pair[1]);
                        }
                    }
                }

                String playerName = params.getOrDefault("player", "");
                String reason = params.getOrDefault("reason", "");

                if (playerName.isBlank()) {
                    sendResponse(exchange, 400, "{\"error\": \"A user was not specified. Please specify a 'player' parameter.\"}");
                    getLogger().warning("Request was denied for missing a 'player' parameter.");
                    return;
                }

                try {
                    Bukkit.getScheduler().runTask(this, () -> {
                        Component message = Component.text("You have been banned.", NamedTextColor.RED);

                        if (!reason.isBlank()) {
                            message = message.append(Component.text("\n" + reason, NamedTextColor.YELLOW));
                        }

                        Bukkit.getBanList(BanListType.PROFILE).addBan(
                                playerName,
                                reason,
                                null,
                                null
                        );

                        // Adding to the banlist doesn't kick the player, so give them a friendly shove.
                        Bukkit.getPlayer(playerName).kick(message);
                    });

                    sendResponse(exchange, 200, "{\"success\": true}");
                    getLogger().info(playerName + " was banned.");
                }
                catch (Exception e) {
                    sendResponse(exchange, 400, "{\"error\": \"" + e.getMessage() + "\"}");
                    getLogger().severe("Error banning " + playerName + ": " + e.getMessage());
                }

            });

            server.createContext("/server/player/unban", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Unban Player command received from " + exchange.getRemoteAddress());

                // Parse query parameters
                String query = exchange.getRequestURI().getQuery();
                if (query == null || query.isEmpty()) {
                    sendResponse(exchange, 400, "{\"error\": \"Missing query parameters\"}");
                    getLogger().warning("Request was denied for missing query parameters.");
                    return;
                }

                Map<String, String> params = new HashMap<>();
                for (String param : query.split("&")) {
                    String[] pair = param.split("=", 2);
                    if (pair.length == 2) {
                        try {
                            params.put(pair[0], java.net.URLDecoder.decode(pair[1], StandardCharsets.UTF_8));
                        } catch (Exception e) {
                            // If decoding fails, use the raw value
                            params.put(pair[0], pair[1]);
                        }
                    }
                }

                String playerName = params.get("player");

                if (playerName == null || playerName.isBlank()) {
                    sendResponse(exchange, 400, "{\"error\": \"A user was not specified. Please specify a 'player' parameter.\"}");
                    getLogger().warning("Request was denied for missing a 'player' parameter.");
                    return;
                }

                var banList = Bukkit.getBanList(BanListType.PROFILE);

                try {
                    OfflinePlayer offlinePlayer = Bukkit.getOfflinePlayer(playerName);
                    PlayerProfile player = Bukkit.createProfile(offlinePlayer.getUniqueId(), playerName);

                    if (banList.isBanned(player)) {
                        Bukkit.getScheduler().runTask(this, () -> {
                            Bukkit.getBanList(BanListType.PROFILE).pardon(player);
                        });

                        sendResponse(exchange, 200, "{\"success\": true}");
                    }
                    else {
                        sendResponse(exchange, 400, "{\"error\": \"Player is not banned.\"}");
                        getLogger().warning("Request was denied since the player '" + playerName + "' is not banned.");
                    }
                }
                catch (Exception e) {
                    sendResponse(exchange, 400, "{\"error\": \"" + e.getMessage() + "\"}");
                    getLogger().severe("Error unbanning " + playerName + ": " + e.getMessage());
                }

            });

            server.createContext("/server/player/whitelist/add", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Whitelist Add Player command received from " + exchange.getRemoteAddress());

                // Parse query parameters
                String query = exchange.getRequestURI().getQuery();
                if (query == null || query.isEmpty()) {
                    sendResponse(exchange, 400, "{\"error\": \"Missing query parameters\"}");
                    getLogger().warning("Request was denied for missing query parameters.");
                    return;
                }

                Map<String, String> params = new HashMap<>();
                for (String param : query.split("&")) {
                    String[] pair = param.split("=", 2);
                    if (pair.length == 2) {
                        try {
                            params.put(pair[0], java.net.URLDecoder.decode(pair[1], StandardCharsets.UTF_8));
                        } catch (Exception e) {
                            // If decoding fails, use the raw value
                            params.put(pair[0], pair[1]);
                        }
                    }
                }

                String playerName = params.get("player");

                if (playerName == null || playerName.isBlank()) {
                    sendResponse(exchange, 400, "{\"error\": \"A user was not specified. Please specify a 'player' parameter.\"}");
                    getLogger().warning("Request was denied for missing a 'player' parameter.");
                    return;
                }

                try {
                    Bukkit.getScheduler().runTask(this, () -> {
                        Bukkit.getOfflinePlayer(playerName).setWhitelisted(true);
                    });

                    getLogger().info(playerName + " was whitelisted.");
                    sendResponse(exchange, 200, "{\"success\": true}");
                }
                catch (Exception e) {
                    sendResponse(exchange, 400, "{\"error\": \"" + e.getMessage() + "\"}");
                    getLogger().severe("Error whitelisting " + playerName + ": " + e.getMessage());
                }

            });

            server.createContext("/server/player/whitelist/remove", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("POST")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use POST\"}");
                    return;
                }

                getLogger().info("Whitelist Remove Player command received from " + exchange.getRemoteAddress());

                // Parse query parameters
                String query = exchange.getRequestURI().getQuery();
                if (query == null || query.isEmpty()) {
                    sendResponse(exchange, 400, "{\"error\": \"Missing query parameters\"}");
                    getLogger().warning("Request was denied for missing query parameters.");
                    return;
                }

                Map<String, String> params = new HashMap<>();
                for (String param : query.split("&")) {
                    String[] pair = param.split("=", 2);
                    if (pair.length == 2) {
                        try {
                            params.put(pair[0], java.net.URLDecoder.decode(pair[1], StandardCharsets.UTF_8));
                        } catch (Exception e) {
                            // If decoding fails, use the raw value
                            params.put(pair[0], pair[1]);
                        }
                    }
                }

                String playerName = params.get("player");

                if (playerName == null || playerName.isBlank()) {
                    sendResponse(exchange, 400, "{\"error\": \"A user was not specified. Please specify a 'player' parameter.\"}");
                    getLogger().warning("Request was denied for missing a 'player' parameter.");
                    return;
                }

                try {
                    Bukkit.getScheduler().runTask(this, () -> {
                        Bukkit.getOfflinePlayer(playerName).setWhitelisted(false);

                        Player player = Bukkit.getPlayer(playerName);

                        if (player != null) {
                            Component message = Component.text("You have been removed from the whitelist and kicked.", NamedTextColor.RED);
                            player.kick(message);
                        }
                    });
                    getLogger().info(playerName + " was removed from the whitelist and kicked.");
                    sendResponse(exchange, 200, "{\"success\": true}");
                }
                catch (Exception e) {
                    sendResponse(exchange, 400, "{\"error\": \"" + e.getMessage() + "\"}");
                    getLogger().severe("Error removing " + playerName + " from whitelist: " + e.getMessage());
                }

            });

            server.createContext("/server/stats", exchange -> {
                // If the request doesn't have proper authentication
                if (!handleAuth(exchange)) return;

                if (!exchange.getRequestMethod().equalsIgnoreCase("GET")) {
                    sendResponse(exchange, 405, "{\"error\": \"Method not allowed. Use GET\"}");
                    return;
                }

                getLogger().info("Server stats request received from " + exchange.getRemoteAddress());

                // Statistic variables
                String gameVersion = Bukkit.getMinecraftVersion();
                int onlinePlayerCount = Bukkit.getOnlinePlayers().size();
                int maxPlayerCount = Bukkit.getMaxPlayers();
                String serverUptime = getFormattedUptime();

                double tps = getServer().getTPS()[0];

                List<String> playerList = new ArrayList<>();

                for (Player p : Bukkit.getOnlinePlayers()) {
                    int ping = p.getPing();
                    String name = p.getName();

                    boolean isAfk = false;
                    if (essentials != null) {
                        com.earth2me.essentials.User user = essentials.getUser(p);
                        if (user != null) isAfk = user.isAfk();
                    }

                    String display = name + (isAfk ? " (AFK)" : "") + " [" + ping + "ms]";
                    playerList.add(display);
                }

                Gson gson = new Gson();
                Map<String, Object> stats = new HashMap<>();
                stats.put("gameVersion", gameVersion);
                stats.put("uptime", serverUptime);
                stats.put("onlinePlayers", onlinePlayerCount);
                stats.put("maxPlayers", maxPlayerCount);
                stats.put("tps", tps);
                stats.put("playerList", playerList);

                String json = gson.toJson(stats);

                exchange.getResponseHeaders().set("Content-Type", "application/json");
                exchange.sendResponseHeaders(200, json.getBytes().length);
                exchange.getResponseBody().write(json.getBytes());
                exchange.close();
            });

            server.setExecutor(null);
            server.start();
            getLogger().info("HTTP server started on " + bindAddress + ":" + port);

        } catch (Exception e) {
            getLogger().severe("Failed to start HTTP Server: " + e.getMessage());
        }
    }

    public void StopHTTPServer() {
        if (server != null) {
            server.stop(0);
        }
    }

    private boolean handleAuth(@NotNull HttpExchange exchange) throws IOException {
        // Check HTTP method
        String method = exchange.getRequestMethod();
        if (!method.equalsIgnoreCase("POST") && !method.equalsIgnoreCase("GET")) {
            sendResponse(exchange, 405, "{\"error\": \"Method not allowed\"}");
            getLogger().info(exchange.getRemoteAddress() + " failed authentication: Attempted a " + method + " request.");
            return false;
        }

        // Check authorization
        if (!isAuthorized(exchange)) {
            sendResponse(exchange, 401, "{\"error\": \"Unauthorized\"}");
            getLogger().info(exchange.getRemoteAddress() + " failed authentication: Incorrect API token.");
            return false;
        }

        return true;
    }

    // Check if request has valid authorization token
    private boolean isAuthorized(@NotNull HttpExchange exchange) {
        String authHeader = exchange.getRequestHeaders().getFirst("Authorization");
        if (authHeader == null) return false;

        // Expected format: "Bearer TOKEN"
        if (authHeader.startsWith("Bearer ")) {
            String token = authHeader.substring(7);
            return token.equals(apiToken);
        }

        return false;
    }

    // Send HTTP response
    private void sendResponse(@NotNull HttpExchange exchange, int statusCode, @NotNull String response) throws IOException {
        exchange.getResponseHeaders().set("Content-Type", "application/json");
        byte[] bytes = response.getBytes(StandardCharsets.UTF_8);
        exchange.sendResponseHeaders(statusCode, bytes.length);

        try (OutputStream os = exchange.getResponseBody()) {
            os.write(bytes);
        }
    }

    class RestartHTTPServer implements CommandExecutor {
        public boolean onCommand(@NotNull CommandSender sender, @NotNull Command command, @NotNull String label, String @NotNull [] args) {
            Player plr = (Player) sender;
            // TODO: Test the permissions with LuckPerms
            if (!plr.hasPermission("commandblock.restarthttpserver")) {
                sender.sendMessage("§cYou don't have permission to use this command.");
                return true;
            }

            // This method also handles restarting if necessary
            StartHTTPServer();
            sender.sendMessage("§aHTTP server restarted successfully!");

            return true;
        }
    }
}
