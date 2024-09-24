using UnityEngine;
using ZeepCoin;
using ZeepkistClient;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

public class GameEventsManager : MonoBehaviour
{
    private PointsManager pointsManager;
    private PredictionManager predictionManager;
    private NotificationManager notificationManager;
    private NetworkingManager networkingManager;

    private Coroutine rechargePointsCoroutine;
    

    private bool wasHost = false;

    void Start()
    {
        pointsManager = FindObjectOfType<PointsManager>();
        predictionManager = FindObjectOfType<PredictionManager>();
        notificationManager = FindObjectOfType<NotificationManager>();
        networkingManager = FindObjectOfType<NetworkingManager>();

        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.RoundEnded += OnRoundEnded;
        ZeepkistNetwork.MasterChanged += OnMasterChanged;
        MultiplayerApi.CreatedRoom += OnCreatedRoom;      
    }

    private void OnCreatedRoom()
    {
        wasHost = true;
        networkingManager.StartPingServer(); 
    }

    private void OnDisconnectedFromGame()
    {
        if (wasHost){
            if (predictionManager.PredictionActive){
                predictionManager.StopPrediction();
            }
            StopCoroutine(rechargePointsCoroutine);
            networkingManager.StopPingServer();
            predictionManager.IsPaused = false;
            pointsManager.IsRechargingPaused = false;
            wasHost = false;
            
        }
        
    }

    private void OnLevelLoaded()
    {
        Plugin.Logger.LogInfo("Level Loaded");
        if (MultiplayerApi.IsPlayingOnline){
            if (ZeepkistNetwork.LocalPlayerHasHostPowers())
            {   
                if (predictionManager.PredictionActive){
                    predictionManager.ResumeCountdown();
                }

                if (pointsManager.IsRechargingPaused == false){
                    rechargePointsCoroutine = StartCoroutine(pointsManager.RechargingPoints());
                }
                else{
                    pointsManager.ResumeRecharge();
                }
            }
        }
    }

    private void OnRoundEnded()
    {
        if (MultiplayerApi.IsPlayingOnline){
            if (ZeepkistNetwork.LocalPlayerHasHostPowers())
            { 
                if (predictionManager.PredictionActive){
                    predictionManager.PauseCountdown();
                }
                if (pointsManager.IsRechargingPaused == false){
                    pointsManager.PauseRecharge();
                }
            }
        }
    }

    private void OnMasterChanged(ZeepkistNetworkPlayer player)
    {
        Plugin.Logger.LogInfo($"The new master of the lobby is: {player.Username}");
        if(player.IsLocal){
            rechargePointsCoroutine = StartCoroutine(pointsManager.RechargingPoints());
            networkingManager.StartPingServer();
            wasHost = true;
        }
        else if (wasHost)
        {
            if (predictionManager.PredictionActive)
            {
                predictionManager.StopPrediction();
                notificationManager.NotifyHostLost();
            }
            StopCoroutine(rechargePointsCoroutine);
            predictionManager.IsPaused = false;
            pointsManager.IsRechargingPaused = false;
            wasHost = false;
        }
    }

}