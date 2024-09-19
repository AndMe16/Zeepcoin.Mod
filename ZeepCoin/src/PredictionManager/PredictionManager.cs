using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZeepCoin;



public class PredictionManager : MonoBehaviour
{

    // Event to notify when the prediction starts and ends
    public static event Action OnPredictionStarted;
    public static event Action OnPredictionEnded;
    public static event Action OnPredictionStoped;
    
    // Control variables
    private bool predictionActive = false;
    public bool PredictionActive
    {
        get {return predictionActive;}
    }
    private uint predictionDuration;
    public uint PredictionDuration
    {
        get {return predictionDuration;}
    }
    uint timeLeft;
    private bool isPaused = false;  // Flag to track if the countdown is paused

    public bool IsPaused
    {
        get {return isPaused;}
        set {isPaused = value;}
    }

    // Predictions
    private Dictionary<ulong, (string username, string commandType, uint totalPointsSent)> playerPredictions = [];
    private List<ulong> voteOrder = [];

    // The result of the coin flip ("heads" or "tails")
    private string result;

    private ServerMessageManager serverMessageManager;
    private PointsManager pointsManager;
    private NotificationManager notificationManager;
    private NetworkingManager networkingManager;

    private Coroutine PredictionCountdownCoroutine;

    void Start()
    {
        serverMessageManager = FindObjectOfType<ServerMessageManager>();
        pointsManager = FindObjectOfType<PointsManager>();
        notificationManager = FindObjectOfType<NotificationManager>();
        networkingManager = FindObjectOfType<NetworkingManager>();
    }



    // Start a new prediction
    public void StartPrediction(uint duration)
    {
        predictionDuration = duration;
        predictionActive = true;
        playerPredictions.Clear(); // Clear previous predictions
        voteOrder.Clear();
        serverMessageManager.ClearServerMessageVars();

        // Notify listeners that prediction has started
        OnPredictionStarted?.Invoke();

        // Start the countdown timer and the servermessage
        timeLeft = predictionDuration;
        serverMessageManager.UpdatePredictionBar();
        PredictionCountdownCoroutine = StartCoroutine(PredictionCountdown());
    }

    public void AddTime(int added_duration)
    {
        uint resulting_duration = (uint)(timeLeft + added_duration);
        if (resulting_duration < 0){
            resulting_duration = 0;
        }
        timeLeft = resulting_duration;
    }

    public void SetPredictionTime(uint duration)
    {
        timeLeft = duration;
    }

    // Submit a prediction for a player
    public void SubmitPrediction(ulong playerId,string username, string choice, uint points, bool isFirstPrediction, uint remainingPoints)
    {
        // Deduct points and store the player's prediction
        pointsManager.DeductPoints(playerId, points);

        if (isFirstPrediction){
            playerPredictions[playerId] = (username,choice,points);
        }
        else{
            uint prevSentPoints = playerPredictions[playerId].totalPointsSent;
            playerPredictions[playerId] = (username,choice,points+prevSentPoints);
        }
        voteOrder.Insert(0, playerId);
        // Update serverMessage voters table
        serverMessageManager.UpdateVotersTable(playerPredictions,remainingPoints-points,voteOrder);        
    }

    // End the prediction and determine the result
    public void EndPrediction()
    {
        predictionActive = false;
        networkingManager.SavedData = false;
        // Get total predicted points
        ulong totalPredictedPoints = TotalPredictedPoints(out ulong totalHeadsPoints, out ulong totalTailsPoints);

        // Check for enough predicted points
        if(totalHeadsPoints==0 || totalTailsPoints==0){
            notificationManager.NotifyLackPoints(totalHeadsPoints,totalTailsPoints);
            serverMessageManager.ShowLackPointsServerMessage();
            RefundPredictedPlayersPoints();
            
            return;
        }

        // Generate a random result (either "heads" or "tails")
        result = UnityEngine.Random.value > 0.5f ? "heads" : "tails";
        Plugin.Logger.LogInfo("Coinflip result: " + result);

        // Notify listeners that prediction has ended
        OnPredictionEnded?.Invoke();

        // Process the results and update points
        CalculateRatios(totalHeadsPoints, totalTailsPoints, out double heads_ratio, out double tails_ratio);
        ProcessResults(heads_ratio, tails_ratio);

        // Update UI with the final result
        serverMessageManager.ShowResultServerMessage(result);

        // Notify players in chat
        notificationManager.NotifyPredictionResults(result,totalPredictedPoints,totalHeadsPoints,totalTailsPoints);
        
    }

    // Process the predictions and award points based on the result
    private void ProcessResults(double heads_ratio, double tails_ratio)
    {
        uint addedPoints;
        foreach (var prediction in playerPredictions)
        {
            ulong playerId = prediction.Key;
            string playerChoice = prediction.Value.commandType;
            uint playerPredictedPoints = prediction.Value.totalPointsSent;

            if (playerChoice == result)
            {
                // Player guessed correctly, award points
                addedPoints = (uint)Math.Round(playerPredictedPoints *(result=="heads"?heads_ratio:tails_ratio),MidpointRounding.AwayFromZero);
                pointsManager.AddPoints(playerId, addedPoints);
            }
        }
        pointsManager.SaveData();
    }

    public void StopPrediction()
    {
        StopCoroutine(PredictionCountdownCoroutine);
        networkingManager.SavedData = false;
        RefundPredictedPlayersPoints();
        predictionActive = false;
        OnPredictionStoped?.Invoke();
        serverMessageManager.ShowStopServerMessage();
    }

    private void RefundPredictedPlayersPoints()
    {
        foreach (var prediction in playerPredictions)
        {
            ulong playerId = prediction.Key;
            uint playerPredictedPoints = prediction.Value.totalPointsSent;
            // Refunding all points
            pointsManager.AddPoints(playerId, playerPredictedPoints); 
        }
        pointsManager.SaveData();
    }

    public bool CheckChangePrediction(ulong playerId, string act_choice)
    {
        if(playerPredictions.TryGetValue(playerId, out var prevPrediction)){
            string prev_choice = prevPrediction.commandType;
            if (act_choice != prev_choice){
                return true;
            }
        }
        return false;
    }

    public bool CheckExistingPrediction(ulong playerId)
    {
        if(playerPredictions.ContainsKey(playerId))
        {
            return true;
        }
        return false;
    }

    private ulong TotalPredictedPoints(out ulong totalHeadsPoints, out ulong totalTailsPoints)
    {
        ulong totalPredictedPoints = 0;
        totalHeadsPoints = 0;
        totalTailsPoints = 0;
        foreach (var prediction in playerPredictions)
        {
            if (prediction.Value.commandType == "heads")
            {
                totalHeadsPoints += prediction.Value.totalPointsSent;
            }
            else
            {
                totalTailsPoints += prediction.Value.totalPointsSent;
            }
            uint playerPredictedPoints = prediction.Value.totalPointsSent;
            totalPredictedPoints += playerPredictedPoints;
        }
        return totalPredictedPoints;
    }
    
    private void CalculateRatios(ulong totalHeadsPoints, ulong totalTailsPoints, out double heads_ratio, out double tails_ratio)
    {
        if (totalHeadsPoints>0){
            heads_ratio = (double)(totalHeadsPoints + totalTailsPoints) / totalHeadsPoints;
            heads_ratio = Math.Round(heads_ratio, 2, MidpointRounding.AwayFromZero);
        } else{
            heads_ratio = 0;
        }

        if (totalTailsPoints>0){
            tails_ratio = (double)(totalHeadsPoints + totalTailsPoints) / totalTailsPoints;
            tails_ratio = Math.Round(tails_ratio, 2, MidpointRounding.AwayFromZero);
        } else{
            tails_ratio = 0;
        }
    }

    private void TotalVoters(out int heads_voters, out int tails_voters)
    {
        heads_voters = 0;
        tails_voters = 0;
        foreach (var prediction in playerPredictions)
        {
            if (prediction.Value.commandType == "heads")
            {
                heads_voters += 1;
            }
            else
            {
                tails_voters += 1;
            }
        }
    }

    // Coroutine for countdown during the prediction phase
    private IEnumerator PredictionCountdown()
    {
        while (timeLeft > 0)
        {
            if (!isPaused)  // Only decrease time if not paused
            {
                serverMessageManager.UpdatePredictionServerMessage(timeLeft);
                yield return new WaitForSecondsRealtime(1f);
                timeLeft--;
                //Plugin.Logger.LogInfo("Time left for prediction: " + timeLeft);
            }
            else
            {
                yield return null;  // When paused, just wait until it's resumed
            }
        }

        // Time's up, end the prediction
        EndPrediction();
    }

    // Method to pause the countdown
    public void PauseCountdown()
    {
        isPaused = true;
        Plugin.Logger.LogInfo("Prediction countdown paused.");
    }

    // Method to resume the countdown
    public void ResumeCountdown()
    {
        isPaused = false;
        Plugin.Logger.LogInfo("Prediction countdown resumed.");
    }

    public void GetPredictionStats(out int heads_voters, out int tails_voters, out ulong totalHeadsPoints, out ulong totalTailsPoints, out double heads_ratio, out double tails_ratio)
    {
        TotalPredictedPoints(out totalHeadsPoints, out totalTailsPoints);
        TotalVoters(out heads_voters, out tails_voters);
        CalculateRatios(totalHeadsPoints, totalTailsPoints, out heads_ratio, out tails_ratio);
    }
    
    
}
