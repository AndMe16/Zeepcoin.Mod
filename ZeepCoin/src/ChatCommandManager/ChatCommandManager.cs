using Steamworks;
using UnityEngine;
using ZeepCoin;
using ZeepkistClient;
using ZeepSDK.ChatCommands;

public class ChatCommandManager : MonoBehaviour
{

    private PredictionManager predictionManager;
    private NotificationManager notificationManager;
    private PlayerInfoManager playerInfoManager;
    private PointsManager pointsManager;
    private NetworkingManager networkingManager;

    void Start()
    {
        predictionManager = FindObjectOfType<PredictionManager>();
        notificationManager = FindObjectOfType<NotificationManager>();
        playerInfoManager = FindObjectOfType<PlayerInfoManager>();
        pointsManager = FindObjectOfType<PointsManager>();
        networkingManager = FindObjectOfType<NetworkingManager>();

        // Local commands
        ChatCommandApi.RegisterLocalChatCommand("/", "coinstart", "<duration in seconds>. Starts a new prediction", OnStartPredictionCommand);
        ChatCommandApi.RegisterLocalChatCommand("/", "coinstop", "Stops the current prediction", OnStopPredictionCommand);
        ChatCommandApi.RegisterLocalChatCommand("/", "coinaddtime", "<additional seconds>. Add or subtract seconds to the active prediction", OnAddCommandCommand);
        ChatCommandApi.RegisterLocalChatCommand("/", "coinsettime", "<duration in seconds>. Set the remaining duration time of the active prediction", OnSetCommandCommand);
        ChatCommandApi.RegisterLocalChatCommand("/", "coinrefund", "<points> <username>. Refund points to a player", OnCoinRefundCommand);

        // Mixed
        ChatCommandApi.RegisterMixedChatCommand("!", "heads", "<points>. Vote for heads using a given amount of points", OnHeadsCommand);
        ChatCommandApi.RegisterMixedChatCommand("!", "tails", "<points>. Vote for tails using a given amount of points", OnTailsCommand);
        ChatCommandApi.RegisterMixedChatCommand("!", "coinpoints", "Check the remaining coin points", OnCoinpointsCommand);
    }


    // Local Commands
    private void OnStartPredictionCommand(string arguments)
    {
        // Check for host
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            notificationManager.NotifyNotHost();
            return;
        }
        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0)){
            notificationManager.NotifyCoinOutsideRound();
            return;
        }
        // Check for active prediction
        if (predictionManager.PredictionActive){
            notificationManager.NotifyOngoingPrediction();
            return;
        }
        if (!networkingManager.IsConnectedToServer){
            notificationManager.NotifyNotConnectedToServer();
            return;
        }
        // Try to parse the duration from the arguments
        uint duration;
        if (!uint.TryParse(arguments, out duration) || (duration<=0))
        {
            notificationManager.NotifyInvalidCoinArguments();
            return;
        }
        if (duration>(60*60*24)-1) // Max 23:59:59
        {
            notificationManager.NotifyExceedingCoinTime();
            return;
        }
        // Start the prediction with the specified duration
        predictionManager.StartPrediction(duration);
        notificationManager.NotifyStartingPrediction(duration);
    }


    private void OnStopPredictionCommand(string arguments)
    {
        // Check for host
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            notificationManager.NotifyNotHost();
            return;
        }
        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0)){
            notificationManager.NotifyCoinOutsideRound();
            return;
        }
        // Check for active prediction
        if (!predictionManager.PredictionActive){
            notificationManager.NotifyNotOngoingPrediction();
            return;
        }
        // Stop the prediction
        predictionManager.StopPrediction();
        notificationManager.NotifyStopingPrediction(SteamClient.Name);
    }

    private void OnAddCommandCommand(string arguments)
    {
        // Check for host
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            notificationManager.NotifyNotHost();
            return;
        }
        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0)){
            notificationManager.NotifyCoinOutsideRound();
            return;
        }
        // Check for active prediction
        if (!predictionManager.PredictionActive){
            notificationManager.NotifyNotOngoingPrediction();
            return;
        }
        int added_duration;
        if (!int.TryParse(arguments, out added_duration) || (added_duration==0))
        {
            notificationManager.NotifyInvalidAddArguments();
            return;
        }
        if (predictionManager.PredictionDuration + added_duration>(60*60*24)-1) // Max 23:59:59
        {
            notificationManager.NotifyExceedingCoinTime();
            return;
        }
        // Adding/Substracting time from the prediction
        predictionManager.AddTime(added_duration);
        notificationManager.NotifyModPredictionTime(added_duration);
    }

    private void OnSetCommandCommand(string arguments)
    {
        // Check for host
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            notificationManager.NotifyNotHost();
            return;
        }
        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0)){
            notificationManager.NotifyCoinOutsideRound();
            return;
        }
        // Check for active prediction
        if (!predictionManager.PredictionActive){
            notificationManager.NotifyNotOngoingPrediction();
            return;
        }
        // Try to parse the duration from the arguments
        uint duration;
        if (!uint.TryParse(arguments, out duration) || (duration<=0))
        {
            notificationManager.NotifyInvalidCoinArguments();
            return;
        }
        if (duration>(60*60*24)-1) // Max 23:59:59
        {
            notificationManager.NotifyExceedingCoinTime();
            return;
        }
        // Set the new prediction time
        predictionManager.SetPredictionTime(duration);
        notificationManager.NotifySetTimePrediction(duration);
    }

    private void OnCoinRefundCommand(string argument)
    {
        // Check for host
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            notificationManager.NotifyNotHost();
            return;
        }
        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0)){
            notificationManager.NotifyCoinOutsideRound();
            return;
        }
        if (!networkingManager.IsConnectedToServer){
            notificationManager.NotifyNotConnectedToServer();
            return;
        }
        // Check for both arguments
        string[] arguments = argument.Split(" ");
        if(arguments.Length <= 1){
            notificationManager.NotifyRefundMissingArguments(arguments);
            return;
        }
        
        string points_str = arguments[0];
        string username = arguments[1];

        if(!(uint.TryParse(points_str, out uint points) && points >0)){
            notificationManager.NotifyInvalidRefundArguments(points_str);
            return;
        }

        // Check for the player in the lobby
        if(!playerInfoManager.IsPlayerInLobby(username, out ZeepkistNetworkPlayer player))
        {
            notificationManager.NotifyRefundNoPlayerInLobby(username);
            return;
        }

        // Adding the refunded points to player
        notificationManager.NotifyRefundingPlayerPoints(username,points,player.SteamID);
        pointsManager.AddPoints(player.SteamID,points);
        pointsManager.SaveData();
    }


    // Mixed commands
    private void OnHeadsCommand(bool isLocal, ulong playerId, string arguments)
    {
        HandleHeadsTailsCommand(isLocal,playerId,arguments,"heads"); 
    }
    private void OnTailsCommand(bool isLocal, ulong playerId, string arguments)
    {
        HandleHeadsTailsCommand(isLocal,playerId,arguments,"tails");
    }

    private void HandleHeadsTailsCommand(bool isLocal,ulong playerId, string arguments, string command)
    {
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers()) return;

        // Get player username
        string username = playerInfoManager.GetPlayerUsername(isLocal,playerId,out playerId);

        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0))
        {
            notificationManager.NotifyMixedCommandOutsideRound(username);
            return;
        }
        // Check for active prediction
        if (!predictionManager.PredictionActive)
        {
            notificationManager.NotifyTailsHeadsNoCoin(username);
            return;
        }
        // Check for a change in the prediction choice
        if(predictionManager.CheckChangePrediction(playerId, command))
        {
            notificationManager.NotifyTailsHeadsChange(username, command == "heads"?"tails":"heads", command);
            return;
        }
        // Try to parse the points from the arguments
        if (!uint.TryParse(arguments, out uint points) || (points <= 0))
        {
            notificationManager.NotifyTailsHeadsInvalid(username,command,arguments);
            return;
        }
        // Check remaining points
        pointsManager.GetPoints(playerId, (remainingPoints) => {
            // Now you have the points after they were fetched (or defaulted in case of error)
            Plugin.Logger.LogInfo($"The player has {remainingPoints} points.");
            if (points>remainingPoints)
            {
                notificationManager.NotifyNotEnoughPoints(username,remainingPoints,points);
                return;
            }

            // Notify depending on the existing vote
            bool isFirstPrediction;
            if(predictionManager.CheckExistingPrediction(playerId))
            {
                isFirstPrediction = false;
            } else{
                isFirstPrediction = true;
            }
            notificationManager.NotifySubmittedPrediction(username,isFirstPrediction);

            // SubmitPrediction
            predictionManager.SubmitPrediction(playerId,username, command,points, isFirstPrediction, remainingPoints);
        });
        
        
    }
     private void OnCoinpointsCommand(bool isLocal, ulong playerId, string arguments)
    {
        if (!ZeepkistNetwork.LocalPlayerHasHostPowers()) return;

        // Get player username
        string username = playerInfoManager.GetPlayerUsername(isLocal,playerId,out playerId);

        // Check for command outside game round
        if (!(ZeepkistNetwork.CurrentLobby.GameState==0))
        {
            notificationManager.NotifyMixedCommandOutsideRound(username);
            return;
        }
        if (!networkingManager.IsConnectedToServer){
            notificationManager.NotifyNotConnectedToServer();
            return;
        }
        // Check remaining points
        pointsManager.GetPoints(playerId, (remainingPoints) => {
            // Now you have the points after they were fetched (or defaulted in case of error)
            Plugin.Logger.LogInfo($"The player has {remainingPoints} points.");
            notificationManager.NotifyRemainingPoints(username,remainingPoints);    
        });
        
        
    }

}
