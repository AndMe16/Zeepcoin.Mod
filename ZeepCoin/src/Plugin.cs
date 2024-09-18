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

    public static ModConfig modConfig;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        harmony = new Harmony("andme123.zeepcoin");
        harmony.PatchAll();
        Logger.LogInfo($"Plugin {"andme123.zeepcoin"} is loaded!");
        ModConfig.Initialize(Config);
        gameObject.AddComponent<PointsManager>();
        gameObject.AddComponent<NotificationManager>();
        gameObject.AddComponent<ServerMessageManager>();
        gameObject.AddComponent<PlayerInfoManager>();
        gameObject.AddComponent<ChatCommandManager>();
        gameObject.AddComponent<PredictionManager>();
        gameObject.AddComponent<GameEventsManager>();
        gameObject.AddComponent<ModConfig>();
        gameObject.AddComponent<NetworkingManager>();
    }


}
