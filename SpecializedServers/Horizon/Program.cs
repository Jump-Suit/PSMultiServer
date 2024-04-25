using CustomLogger;
using Horizon.LIBRARY.Database;
using Horizon.PluginManager;
using Newtonsoft.Json.Linq;
using CyberBackendLibrary.GeoLocalization;
using System.Runtime;

public static class HorizonServerConfiguration
{
    public static string PluginsFolder { get; set; } = $"{Directory.GetCurrentDirectory()}/static/medius_plugins";
    public static string DatabaseConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/db.config.json";
    public static string HTTPSCertificateFile { get; set; } = $"{Directory.GetCurrentDirectory()}/static/SSL/HorizonHTTPService.pfx";
    public static string HTTPSCertificatePassword { get; set; } = "qwerty";
    public static bool EnableMedius { get; set; } = true;
    public static bool EnableDME { get; set; } = true;
    public static bool EnableMuis { get; set; } = true;
    public static bool EnableBWPS { get; set; } = true;
    public static bool EnableNAT { get; set; } = true;
    public static string? PlayerAPIStaticPath { get; set; } = $"{Directory.GetCurrentDirectory()}/static/wwwroot";
    public static string? DMEConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/dme.json";
    public static string? MEDIUSConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/medius.json";
    public static string? MUISConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/muis.json";
    public static string? BWPSConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/bwps.json";
    public static string? NATConfig { get; set; } = $"{Directory.GetCurrentDirectory()}/static/nat.json";
    public static string MediusAPIKey { get; set; } = "nwnbiRsiohjuUHQfPaNrStG3moQZH+deR8zIykB8Lbc="; // Base64 only.
    public static string HomeVersionBetaHDK { get; set; } = "01.86";
    public static string HomeVersionRetail { get; set; } = "01.86";
    public static string[]? HTTPSDNSList { get; set; }

    public static DbController Database = new(DatabaseConfig);

    public static List<IPlugin> plugins = PluginLoader.LoadPluginsFromFolder(PluginsFolder);

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
            LoggerAccessor.LogWarn("Could not find the horizon.json file, writing and using server's default.");

            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory() + "/static");

            // Write the JObject to a file
            File.WriteAllText(configPath, new JObject(
                new JProperty("medius", new JObject(
                    new JProperty("enabled", EnableMedius),
                    new JProperty("config", MEDIUSConfig)
                )),
                new JProperty("dme", new JObject(
                    new JProperty("enabled", EnableDME),
                    new JProperty("config", DMEConfig)
                )),
                new JProperty("muis", new JObject(
                    new JProperty("enabled", EnableMuis),
                    new JProperty("config", MUISConfig)
                )),
                new JProperty("nat", new JObject(
                    new JProperty("enabled", EnableNAT),
                    new JProperty("config", NATConfig)
                )),
                new JProperty("bwps", new JObject(
                    new JProperty("enabled", EnableBWPS),
                    new JProperty("config", BWPSConfig)
                )),
                new JProperty("https_dns_list", HTTPSDNSList ?? Array.Empty<string>()),
                new JProperty("certificate_file", HTTPSCertificateFile),
                new JProperty("certificate_password", HTTPSCertificatePassword),
                new JProperty("player_api_static_path", PlayerAPIStaticPath),
                new JProperty("medius_api_key", MediusAPIKey),
                new JProperty("plugins_folder", PluginsFolder),
                new JProperty("database", DatabaseConfig),
                new JProperty("home_version_beta_hdk", HomeVersionBetaHDK),
                new JProperty("home_version_retail", HomeVersionRetail)
            ).ToString().Replace("/", "\\\\"));

            return;
        }

        try
        {
            // Parse the JSON configuration
            dynamic config = JObject.Parse(File.ReadAllText(configPath));

            EnableMedius = GetValueOrDefault(config.medius, "enabled", EnableMedius);
            EnableDME = GetValueOrDefault(config.dme, "enabled", EnableDME);
            EnableMuis = GetValueOrDefault(config.muis, "enabled", EnableMuis);
            EnableNAT = GetValueOrDefault(config.nat, "enabled", EnableNAT);
            EnableBWPS = GetValueOrDefault(config.bwps, "enabled", EnableBWPS);
            HTTPSCertificateFile = GetValueOrDefault(config, "certificate_file", HTTPSCertificateFile);
            HTTPSCertificatePassword = GetValueOrDefault(config, "certificate_password", HTTPSCertificatePassword);
            PlayerAPIStaticPath = GetValueOrDefault(config, "player_api_static_path", PlayerAPIStaticPath);
            HTTPSDNSList = GetValueOrDefault(config, "https_dns_list", HTTPSDNSList);
            DMEConfig = GetValueOrDefault(config.dme, "config", DMEConfig);
            MEDIUSConfig = GetValueOrDefault(config.medius, "config", MEDIUSConfig);
            MUISConfig = GetValueOrDefault(config.muis, "config", MUISConfig);
            NATConfig = GetValueOrDefault(config.nat, "config", NATConfig);
            BWPSConfig = GetValueOrDefault(config.bwps, "config", BWPSConfig);
            MediusAPIKey = GetValueOrDefault(config, "medius_api_key", MediusAPIKey);
            PluginsFolder = GetValueOrDefault(config, "plugins_folder", PluginsFolder);
            DatabaseConfig = GetValueOrDefault(config, "database", DatabaseConfig);
            HomeVersionBetaHDK = GetValueOrDefault(config, "home_version_beta_hdk", HomeVersionBetaHDK);
            HomeVersionRetail = GetValueOrDefault(config, "home_version_retail", HomeVersionRetail);
        }
        catch (Exception ex)
        {
            LoggerAccessor.LogWarn($"horizon.json file is malformed (exception: {ex}), using server's default.");
        }
    }

    // Helper method to get a value or default value if not present
    public static T GetValueOrDefault<T>(dynamic obj, string propertyName, T defaultValue)
    {
        if (obj != null)
        {
            if (obj is JObject jObject)
            {
                if (jObject.TryGetValue(propertyName, out JToken? value))
                {
                    T? returnvalue = value.ToObject<T>();
                    if (returnvalue != null)
                        return returnvalue;
                }
            }
            else if (obj is JArray jArray)
            {
                if (int.TryParse(propertyName, out int index) && index >= 0 && index < jArray.Count)
                {
                    T? returnvalue = jArray[index].ToObject<T>();
                    if (returnvalue != null)
                        return returnvalue;
                }
            }
        }
        return defaultValue;
    }
}

class Program
{
    static Task RefreshConfig()
    {
        while (true)
        {
            // Sleep for 5 minutes (300,000 milliseconds)
            Thread.Sleep(5 * 60 * 1000);

            // Your task logic goes here
            LoggerAccessor.LogInfo("Config Refresh at - " + DateTime.Now);

            HorizonServerConfiguration.RefreshVariables($"{Directory.GetCurrentDirectory()}/static/horizon.json");
        }
    }

    static Task HorizonStarter()
    {
        if (HorizonServerConfiguration.EnableMedius)
        {
            GeoIP.Initialize();

            Task.Run(() => { new Horizon.MUM.MumServerHandler("*", 10076).StartServer(); });

            Horizon.MEDIUS.MediusClass.MediusMain();

            _ = Task.Run(() => Parallel.Invoke(
                   () => new Horizon.HTTPSERVICE.CrudServerHandler("*", 61920).StartServer(),
                   () => new Horizon.HTTPSERVICE.CrudServerHandler("0.0.0.0", 8443).StartServer(HorizonServerConfiguration.HTTPSCertificateFile, HorizonServerConfiguration.HTTPSCertificatePassword)
               ));
        }

        if (HorizonServerConfiguration.EnableNAT)
            Horizon.NAT.NATClass.NATMain();

        if (HorizonServerConfiguration.EnableBWPS)
            Horizon.BWPS.BWPSClass.BWPSMain();

        if (HorizonServerConfiguration.EnableMuis)
            Horizon.MUIS.MuisClass.MuisMain();

        if (HorizonServerConfiguration.EnableDME)
            Horizon.DME.DmeClass.DmeMain();

        return Task.CompletedTask;
    }

    static void Main()
    {
        bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows;

        if (!IsWindows)
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        LoggerAccessor.SetupLogger("Horizon");

        HorizonServerConfiguration.RefreshVariables($"{Directory.GetCurrentDirectory()}/static/horizon.json");

        CyberBackendLibrary.SSL.SSLUtils.InitCerts(HorizonServerConfiguration.HTTPSCertificateFile, HorizonServerConfiguration.HTTPSCertificatePassword, HorizonServerConfiguration.HTTPSDNSList);

        _ = Task.Run(() => Parallel.Invoke(
                    () => HorizonStarter(),
                    () => RefreshConfig()
                ));

        if (IsWindows)
        {
            while (true)
            {
                LoggerAccessor.LogInfo("Press any key to shutdown the server. . .");

                Console.ReadLine();

                LoggerAccessor.LogWarn("Are you sure you want to shut down the server? [y/N]");

                if (char.ToLower(Console.ReadKey().KeyChar) == 'y')
                {
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