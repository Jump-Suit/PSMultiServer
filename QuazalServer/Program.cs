﻿using BackendProject;
using CustomLogger;
using Newtonsoft.Json.Linq;
using System.Runtime;

public static class QuazalServerConfiguration
{
    public static string ServerBindAddress { get; set; } = MiscUtils.GetLocalIPAddress().ToString();
    public static string ServerPublicBindAddress { get; set; } = MiscUtils.GetPublicIPAddress();
    public static string EdNetBindAddressOverride { get; set; } = string.Empty;
    public static string QuazalStaticFolder { get; set; } = $"{Directory.GetCurrentDirectory()}/static/Quazal";
    public static bool UsePublicIP { get; set; } = true;
    public static bool EnableDiscordPlugin { get; set; } = true;
    public static string DiscordBotToken { get; set; } = string.Empty;
    public static string DiscordChannelID { get; set; } = string.Empty;

    /// <summary>
    /// Tries to load the specified configuration file.
    /// Throws an exception if it fails to find the file.
    /// </summary>
    /// <param name="configPath"></param>
    /// <exception cref="FileNotFoundException"></exception>
    public static void RefreshVariables(string configPath)
    {
        // Make sure the file exists
        if (!File.Exists(configPath))
        {
            LoggerAccessor.LogWarn("Could not find the quazal.json file, using server's default.");
            return;
        }

        try
        {
            // Read the file
            string json = File.ReadAllText(configPath);

            // Parse the JSON configuration
            dynamic config = JObject.Parse(json);

            ServerBindAddress = config.server_bind_address;
            ServerPublicBindAddress = config.server_public_bind_address;
            EdNetBindAddressOverride = config.ednet_bind_address_override;
            QuazalStaticFolder = config.server_static_folder;
            UsePublicIP = config.server_public_ip;
            DiscordBotToken = config.discord_bot_token;
            DiscordChannelID = config.discord_channel_id;
            EnableDiscordPlugin = config.discord_plugin.enabled;
        }
        catch (Exception)
        {
            LoggerAccessor.LogWarn("quazal.json file is malformed, using server's default.");
        }
    }
}

class Program
{
    static void GCCollectTask(object? state)
    {
        // Perform a periodic GC Collect Task.
        LoggerAccessor.LogInfo("GC Collect at - " + DateTime.Now);

        GC.Collect();
    }

    static Task RefreshConfig()
    {
        while (true)
        {
            // Sleep for 5 minutes (300,000 milliseconds)
            Thread.Sleep(5 * 60 * 1000);

            // Your task logic goes here
            LoggerAccessor.LogInfo("Config Refresh at - " + DateTime.Now);

            QuazalServerConfiguration.RefreshVariables($"{Directory.GetCurrentDirectory()}/static/quazal.json");
        }
    }

    static void Main()
    {
        if (!MiscUtils.IsWindows())
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        LoggerAccessor.SetupLogger("QuazalServer");

        QuazalServerConfiguration.RefreshVariables($"{Directory.GetCurrentDirectory()}/static/quazal.json");

        QuazalServer.RDVServices.ServiceFactoryRDV.RegisterRDVServices();

        if (QuazalServerConfiguration.EnableDiscordPlugin && !string.IsNullOrEmpty(QuazalServerConfiguration.DiscordChannelID) && !string.IsNullOrEmpty(QuazalServerConfiguration.DiscordBotToken))
            _ = BackendProject.Discord.CrudDiscordBot.BotStarter(QuazalServerConfiguration.DiscordChannelID, QuazalServerConfiguration.DiscordBotToken);

        QuazalServer.ServerProcessors.BackendServicesServer backend = new();

        QuazalServer.ServerProcessors.RDVServer rendezvous = new();

        _ = Task.Run(() => Parallel.Invoke(
                    () => backend.Start(new List<Tuple<int, string>>
                    {
                        Tuple.Create(30201, "yh64s"), // TDU
                        Tuple.Create(60106, "w6kAtr3T"), // DFSPC
                        Tuple.Create(61111, "QusaPha9"), // DFSPS3
                        Tuple.Create(60116, "OLjNg84Gh"), // HAWX2PS3
                        Tuple.Create(61121, "q1UFc45UwoyI"), // GRFSPS3
                        Tuple.Create(61126, "cYoqGd4f"), // AC3PS3
                        Tuple.Create(61128, "cYoqGd4f"), // AC3MULTPS3
                        Tuple.Create(60001, "cYoqGd4f"), // RB3
                        Tuple.Create(21032, "cYoqGd4f") // GRO
                    }, 2, new CancellationTokenSource().Token),
                    () => rendezvous.Start(new List<Tuple<int, int, string>>
                    {
                        Tuple.Create(30200, 30201, "yh64s"), // TDU
                        Tuple.Create(60105, 60106, "w6kAtr3T"), // DFSPC
                        Tuple.Create(61110, 61111, "QusaPha9"), // DFSPS3
                        Tuple.Create(60115, 60116, "OLjNg84Gh"), // HAWX2PS3
                        Tuple.Create(61120, 61121, "q1UFc45UwoyI"), // GRFSPS3
                        Tuple.Create(61125, 61126, "cYoqGd4f"), // AC3PS3
                        Tuple.Create(61127, 61128, "cYoqGd4f") // AC3MULTPS3
                    }, 2, new CancellationTokenSource().Token),
                    () => RefreshConfig()
                ));

        // Set up a timer that triggers every 10 seconds (In case it still happens).
        // Timer timer = new(GCCollectTask, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

        if (MiscUtils.IsWindows())
        {
            while (true)
            {
                LoggerAccessor.LogInfo("Press any key to shutdown the server. . .");

                Console.ReadLine();

                LoggerAccessor.LogWarn("Are you sure you want to shut down the server? [y/N]");
                char input = char.ToLower(Console.ReadKey().KeyChar);

                if (input == 'y')
                {
                    backend.StopAsync();
                    rendezvous.StopAsync();
                    LoggerAccessor.LogInfo("Shutting down. Goodbye!");
                    Environment.Exit(0);
                }
            }
        }
        else
        {
            LoggerAccessor.LogWarn("\nConsole Inputs are locked while server is running. . .");

            Thread.Sleep(Timeout.Infinite); // While-true on Linux are thread blocking if on main static.
        }
    }
}