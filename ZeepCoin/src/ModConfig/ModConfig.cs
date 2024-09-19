using BepInEx.Configuration;
using UnityEngine;
using ZeepSDK.Messaging;

namespace ZeepCoin;

public class ModConfig : MonoBehaviour
{
    private static PointsManager pointsManager;
    private static ServerMessageManager serverMessageManager;
    private static NetworkingManager networkingManager;
    public static ConfigEntry<bool> useGlobalDatabase;
    public static ConfigEntry<int> rechargePoints;
    public static ConfigEntry<int> rechargeInterval;
    public static ConfigEntry<int> defaultPoints;

    // Constructor that takes a ConfigFile instance from the main class
    public static void Initialize(ConfigFile config)
    {
        useGlobalDatabase = config.Bind("Database", "Use Global Database", true,
                                        "Player's points are getting stored in a global database");

        rechargePoints = config.Bind("User Database",    
                                    "Points added per recharge", 
                                    10, 
                                    new ConfigDescription("Not aplicable for global database! Min.5 Max. 100 Amount of points that will be added per recharge for each player", new AcceptableValueRange<int>(5, 100)));

        rechargeInterval = config.Bind("User Database",    
                                        "Recharge Time interval (sec)", 
                                        300, 
                                        new ConfigDescription("Not aplicable for global database! Min.60 Max.3600 Time interval for recharging points", new AcceptableValueRange<int>(60, 3600))); 

        defaultPoints = config.Bind("User Database",    
                                        "Default initial points", 
                                        1000, 
                                        new ConfigDescription("Not aplicable for global database! Min.100 Max.2000 The default initial points that players get", new AcceptableValueRange<int>(100, 2000)));
    }

    void Start()
    {
        useGlobalDatabase.SettingChanged += OnSettingsChanged;
        rechargePoints.SettingChanged += OnSettingsChanged;
        rechargeInterval.SettingChanged += OnSettingsChanged;
        defaultPoints.SettingChanged += OnSettingsChanged;

        pointsManager = FindObjectOfType<PointsManager>();
        serverMessageManager = FindObjectOfType<ServerMessageManager>();
        networkingManager = FindObjectOfType<NetworkingManager>();
    }


    private static void OnSettingsChanged(object sender, System.EventArgs e)
    {
        var configEntry = sender as ConfigEntryBase;

        Plugin.Logger.LogInfo($"Setting changed: {configEntry.Definition.Key}");

        if (configEntry == useGlobalDatabase)
        {
            // Update the networking manager based on the new value of useGlobalDatabase
            if (useGlobalDatabase.Value)
            {
                MessengerApi.LogWarning("The other setting will reset since you are using the global database",10); 
            } 
            _ = networkingManager.SetIsGlobalAsync(useGlobalDatabase.Value);

        }
        else if (configEntry == rechargePoints)
        {
            if (!useGlobalDatabase.Value) // Check if useGlobalDatabase is false before updating
            {
                pointsManager.RechargePoints = (uint)rechargePoints.Value;
                serverMessageManager.UpdateRechargeInfo();  
            }
            else
            {
                Plugin.Logger.LogInfo("Global database is enabled; rechargePoints setting is ignored.");
                MessengerApi.LogWarning("\"Points added per recharge\" will have no effect, you are using the global database",10);
            }
        }
        else if (configEntry == rechargeInterval)
        {
            if (!useGlobalDatabase.Value) // Check if useGlobalDatabase is false before updating
            {
                pointsManager.RechargeInterval = rechargeInterval.Value;
                serverMessageManager.UpdateRechargeInfo();
            }
            else
            {
                Plugin.Logger.LogInfo("Global database is enabled; rechargeInterval setting is ignored.");
                MessengerApi.LogWarning("\"Recharge Time interval\" will have no effect, you are using the global database",10);
            }
        }
        else if (configEntry == defaultPoints)
        {
            if (!useGlobalDatabase.Value) // Check if useGlobalDatabase is false before updating
            {
                pointsManager.DefaultInitialPoints = (uint)defaultPoints.Value;
            }
            else
            {
                Plugin.Logger.LogInfo("Global database is enabled; defaultPoints setting is ignored.");
                MessengerApi.LogWarning("\"Default initial\" points will have no effect, you are using the global database",10);
            }
        }
    }

    public static async void LoadConfigValues()
    {
        var (rechargePoints_, rechargeInterval_, defaultPoints_, error) = await networkingManager.LoadServerConfigValuesAsync();

        if (string.IsNullOrEmpty(error))
        {
            Plugin.Logger.LogInfo($"Recharge Points: {rechargePoints}, Recharge Interval: {rechargeInterval}, Default Points: {defaultPoints}");
            rechargePoints.Value = rechargePoints_;
            rechargeInterval.Value = rechargeInterval_;
            defaultPoints.Value = defaultPoints_;
            pointsManager.RechargePoints = (uint)rechargePoints.Value;
            serverMessageManager.UpdateRechargeInfo();
            pointsManager.RechargeInterval = rechargeInterval.Value;
            serverMessageManager.UpdateRechargeInfo();
            pointsManager.DefaultInitialPoints = (uint)defaultPoints.Value;  
        }
        else
        {
            Plugin.Logger.LogError($"Failed to load configuration values: {error}");
        }
    }
}