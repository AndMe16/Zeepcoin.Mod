using BepInEx;
using BepInEx.Logging;
using HarmonyLib;


namespace ZeepCoin;

[BepInPlugin("andme123.zeepcoin", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    internal static new ManualLogSource Logger;

    public static Coin_ModConfig modConfig;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        harmony = new Harmony("andme123.zeepcoin");
        harmony.PatchAll();
        Logger.LogInfo($"Plugin {"andme123.zeepcoin"} is loaded!");
        Coin_ModConfig.Initialize(Config);
        gameObject.AddComponent<Coin_PointsManager>();
        gameObject.AddComponent<Coin_NotificationManager>();
        gameObject.AddComponent<Coin_ServerMessageManager>();
        gameObject.AddComponent<Coin_PlayerInfoManager>();
        gameObject.AddComponent<Coin_ChatCommandManager>();
        gameObject.AddComponent<Coin_PredictionManager>();
        gameObject.AddComponent<Coin_GameEventsManager>();
        gameObject.AddComponent<Coin_ModConfig>();
        gameObject.AddComponent<Coin_NetworkingManager>();
    }


}
