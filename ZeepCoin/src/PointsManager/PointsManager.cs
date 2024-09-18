using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZeepCoin;
using ZeepkistClient;

public class PointsManager : MonoBehaviour
{

    private NetworkingManager networkingManager;

    private uint defaultInitialPoints;

    public uint DefaultInitialPoints
    {
        set {defaultInitialPoints = value;}
    }

    private int rechargeInterval;

    public int RechargeInterval
    {
        set {rechargeInterval = value;}
    }

    private uint rechargePoints;

    public uint RechargePoints
    {
        set {rechargePoints = value;}
    }

    // Dictionary to store player points
    private Dictionary<ulong, uint> playerPoints = new Dictionary<ulong, uint>();


    private bool isRechargingPaused = false;  // Flag to track if the countdown is paused

    public bool IsRechargingPaused
    {
        get {return isRechargingPaused;}
        set {isRechargingPaused = value;}
    }

    // Initialize the values in Start to ensure ModConfig is ready
    private void Start()
    {
        defaultInitialPoints = (uint)ModConfig.defaultPoints.Value;
        rechargeInterval = ModConfig.rechargeInterval.Value;
        rechargePoints = (uint)ModConfig.rechargePoints.Value;

        networkingManager = FindObjectOfType<NetworkingManager>();
    }

    // Add points to a player
    public void AddPoints(ulong playerId, uint points)
    {
        if (playerPoints.ContainsKey(playerId))
        {
            playerPoints[playerId] += points;
            Plugin.Logger.LogInfo($"Added {points} points to {playerId}. Total: {playerPoints[playerId]} points.");
        }
        else
        {
            StartCoroutine(networkingManager.Load_Single_Player_Points(playerId, 
            (remainingpoints) => {
                Plugin.Logger.LogInfo($"Player {playerId} has {remainingpoints} points.");
                playerPoints[playerId] = (uint)remainingpoints + points;
                Plugin.Logger.LogInfo($"Added {points} points to {playerId}. Total: {playerPoints[playerId]} points.");
            },
            (error) => {
                Plugin.Logger.LogWarning($"Failed to retrieve points for player {playerId}: {error}");
                if (error.Contains("404"))
                {
                    playerPoints[playerId] = defaultInitialPoints + points;  // Initialize and add points if player doesn't exist
                    Plugin.Logger.LogInfo($"Player {playerId} does not have any points. Assign {defaultInitialPoints} default points to player");
                    Plugin.Logger.LogInfo($"Added {points} points to {playerId}. Total: {playerPoints[playerId]} points.");
                }
                else{
                    Plugin.Logger.LogInfo($"Not loading default points of player {playerId} since the error {error} is not related to a missing player in DB");
                }
                
                
            }));
        }
    }

    // Deduct points from a player
    public void DeductPoints(ulong playerId, uint points)
    {
        if (playerPoints.ContainsKey(playerId))
        {
            if (playerPoints[playerId] >= points)
            {
                playerPoints[playerId] -= points;
                Plugin.Logger.LogInfo($"Deducted {points} points from {playerId}. Remaining: {playerPoints[playerId]} points.");
            }
            else
            {
                Plugin.Logger.LogInfo($"Cannot deduct {points} points from {playerId} as they only have {playerPoints[playerId]} points.");
            }
        }
        else
        {
            Plugin.Logger.LogInfo($"Player {playerId} does not exist in the points system.");
        }
    }

    // Get points for a player
    public void GetPoints(ulong playerId, System.Action<uint> onPointsRetrieved)
    {
        if (playerPoints.ContainsKey(playerId))
        {
            uint points = playerPoints[playerId];
            Plugin.Logger.LogInfo($"Player {playerId} has {points} points.");
            onPointsRetrieved?.Invoke(points);
        }
        else
        {
            Plugin.Logger.LogInfo("Trying to load player points from server");
            StartCoroutine(networkingManager.Load_Single_Player_Points(playerId, 
            (points) => {
                Plugin.Logger.LogInfo($"Player {playerId} has {points} points.");
                playerPoints[playerId] = (uint)points;
                onPointsRetrieved?.Invoke((uint)points);  // Pass the points to the callback
            },
            (error) => {
                Plugin.Logger.LogWarning($"Failed to retrieve points for player {playerId}: {error}");
                if (error.Contains("404"))
                {
                    playerPoints[playerId] = defaultInitialPoints;
                    Plugin.Logger.LogInfo($"Player {playerId} does not have any points. Assign {defaultInitialPoints} default points to player");
                    onPointsRetrieved?.Invoke(defaultInitialPoints);  // Pass default points to the callback in case of error
                }
                else{
                    Plugin.Logger.LogInfo($"Not loading default points of player {playerId} since the error {error} is not related to a missing player in DB");
                }
            }));
        }
    }


    // Coroutine for recharging points while playing
    public IEnumerator RechargingPoints()
    {
        while (true)
        {
            if (!isRechargingPaused && networkingManager.IsConnectedToServer){
                yield return new WaitForSecondsRealtime(rechargeInterval);
                foreach (var player in ZeepkistNetwork.PlayerList)
                {
                    AddPoints(player.SteamID, rechargePoints);
                }
                Plugin.Logger.LogInfo($"Recharging {rechargePoints} points each {rechargeInterval} seconds");
                SaveData();
            }
            else
            {
                yield return null;  // When paused, just wait until it's resumed
            }
            
        }
    }

    public void PauseRecharge()
    {
        isRechargingPaused = true;
        Plugin.Logger.LogInfo("Recharging paused.");
    }

    // Method to resume the countdown
    public void ResumeRecharge()
    {
        isRechargingPaused = false;
        Plugin.Logger.LogInfo("Recharging resumed.");
    }


    public void SaveData()
    {
        StartCoroutine(networkingManager.Save_Data_Server(playerPoints));
    }
}
