using UnityEngine;
using ZeepCoin.src;
using ZeepkistClient;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

public class Coin_GameEventsManager : MonoBehaviour
{
    private Coin_PointsManager pointsManager;
    private Coin_PredictionManager predictionManager;
    private Coin_NotificationManager notificationManager;
    private Coin_NetworkingManager networkingManager;

    private Coroutine rechargePointsCoroutine;


    private bool wasHost = false;

#pragma warning disable IDE0051 // Remove unused private members
    void Start()
#pragma warning restore IDE0051 // Remove unused private members
    {
        pointsManager = FindObjectOfType<Coin_PointsManager>();
        predictionManager = FindObjectOfType<Coin_PredictionManager>();
        notificationManager = FindObjectOfType<Coin_NotificationManager>();
        networkingManager = FindObjectOfType<Coin_NetworkingManager>();

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
        if (wasHost)
        {
            if (predictionManager.PredictionActive)
            {
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
        if (MultiplayerApi.IsPlayingOnline)
        {
            if (ZeepkistNetwork.IsMasterClient)
            {
                if (predictionManager.PredictionActive)
                {
                    predictionManager.ResumeCountdown();
                }

                if (pointsManager.IsRechargingPaused == false)
                {
                    rechargePointsCoroutine = StartCoroutine(pointsManager.RechargingPoints());
                }
                else
                {
                    pointsManager.ResumeRecharge();
                }
            }
        }
    }

    private void OnRoundEnded()
    {
        if (MultiplayerApi.IsPlayingOnline)
        {
            if (ZeepkistNetwork.IsMasterClient)
            {
                if (predictionManager.PredictionActive)
                {
                    predictionManager.PauseCountdown();
                }
                if (pointsManager.IsRechargingPaused == false)
                {
                    pointsManager.PauseRecharge();
                }
            }
        }
    }

    private void OnMasterChanged(ZeepkistNetworkPlayer player)
    {
        Plugin.Logger.LogInfo($"The new master of the lobby is: {player.Username}");
        if (player.IsLocal)
        {
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