using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ZeepCoin.src.ModConfig;


namespace ZeepCoin.src;

[BepInPlugin("andme123.zeepcoin", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    internal static new ManualLogSource Logger;

    public static Coin_ModConfig modConfig;

#pragma warning disable IDE0051 // Remove unused private members
    private void Awake()
#pragma warning restore IDE0051 // Remove unused private members
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
