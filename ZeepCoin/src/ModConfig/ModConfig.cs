using BepInEx.Configuration;
using UnityEngine;

namespace ZeepCoin;

public class ModConfig : MonoBehaviour
{
    private static PointsManager pointsManager;
    private static ServerMessageManager serverMessageManager;
    public static ConfigEntry<bool> useGlobalDatabase;
    public static ConfigEntry<int> rechargePoints;
    public static ConfigEntry<int> rechargeInterval;
    public static ConfigEntry<int> defaultPoints;

    // Constructor that takes a ConfigFile instance from the main class
    public static void Initialize(ConfigFile config)
    {
        useGlobalDatabase = config.Bind("Database", "Use Global Database", false,
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
    }


    private static void OnSettingsChanged(object sender, System.EventArgs e)
    {
        var configEntry = sender as ConfigEntryBase;

        Plugin.Logger.LogInfo($"Setting changed: {configEntry .Definition.Key}");
        if (configEntry  == useGlobalDatabase)
        {
            Plugin.Logger.LogInfo($"Setting changed: {configEntry .Definition.Key}");
        }
        else if (configEntry  == rechargePoints)
        {
            pointsManager.RechargePoints = (uint)rechargePoints.Value;
            serverMessageManager.UpdateRechargeInfo();  
        }
        else if (configEntry  == rechargeInterval)
        {
            pointsManager.RechargeInterval = rechargeInterval.Value;
            serverMessageManager.UpdateRechargeInfo();
            
        }
        else if (configEntry  == defaultPoints)
        {
            pointsManager.DefaultInitialPoints = (uint)defaultPoints.Value;
        }
    }
}