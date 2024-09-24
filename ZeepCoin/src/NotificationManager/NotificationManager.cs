using Steamworks;
using UnityEngine;
using ZeepCoin;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;

public class NotificationManager : MonoBehaviour
{
    public void NotifyNotHost()
    {
        ChatApi.AddLocalMessage("<i>You are not host!</i>");
        Plugin.Logger.LogInfo($"The player is not host");
    }

    public void NotifyStartingPrediction(float duration)
    {  
        ChatApi.AddLocalMessage($"<i>Prediction started for {duration} seconds.</i>");
        ChatApi.SendMessage("Coin? The prediction has started! Use !heads <points> OR !tails <points> to submit your prediction.");
        Plugin.Logger.LogInfo($"Prediction started for {duration} seconds.");
    }

    public void NotifyInvalidCoinArguments()
    {
        Plugin.Logger.LogInfo("Invalid duration.");
        ChatApi.AddLocalMessage("<i>Specify the duration of the prediction in seconds! eg. !coinstart 60</i>");
    }

    public void NotifyCoinOutsideRound()
    {
        Plugin.Logger.LogInfo("coinstart command received outside game state 0");
        ChatApi.AddLocalMessage("<i>You cannot use this command during podium</i>");
    }

    public void NotifyOngoingPrediction()
    {
        Plugin.Logger.LogInfo("A prediction is already active!");
        ChatApi.AddLocalMessage("<i>A prediction is already active!</i>");
    }

    public void NotifyNotConnectedToServer()
    {
        Plugin.Logger.LogInfo("Not connected to server!");
        ChatApi.AddLocalMessage("<i>Currently you are not connected to the server, wait 1 minute before starting a prediction again</i>");
    }

    public void NotifyRefundingWithGlobalDB()
    {
        Plugin.Logger.LogInfo("Trying to refund points while using the global database");
        ChatApi.AddLocalMessage("<i>You can only use this command while using the host's specific database!</i>");
    }

    public void NotifyExceedingCoinTime(){
        ChatApi.AddLocalMessage("<i>Time exceeded 24 hours! Max 86399 seconds</i>");
        Plugin.Logger.LogInfo("Time exceeded 24 hours!");
    }

    public void NotifyNotOngoingPrediction()
    {
        Plugin.Logger.LogInfo("There is no active prediction!");
        ChatApi.AddLocalMessage("<i>There is no active prediction!</i>");
    }

    public void NotifyStopingPrediction(string username)
    {
        ChatApi.SendMessage($"The prediction was stoped. All points will be refunded.");
        Plugin.Logger.LogInfo("Prediction stoped");
    }

    public void NotifyInvalidAddArguments(){
        Plugin.Logger.LogInfo("Invalid duration.");
        ChatApi.AddLocalMessage("<i>Invalid duration. Please provide a valid amount of seconds you want to add, positive or negative! eg. !coinadd 60 or !coinadd -137</i>");
    }

    public void NotifyModPredictionTime(int added_duration)
    {
        ChatApi.AddLocalMessage($"<i>{(added_duration > 0 ? "Adding" : "Subtracting")} {(added_duration > 0 ? added_duration : -added_duration)} seconds to the prediction</i>");
        Plugin.Logger.LogInfo($"{(added_duration > 0 ? "Adding" : "Subtracting")} {(added_duration > 0 ? added_duration : -added_duration)} seconds to the prediction");
    }

    public void NotifySetTimePrediction(uint duration)
    {
        Plugin.Logger.LogInfo($"Setting the prediction to {duration} seconds");
        ChatApi.AddLocalMessage($"<i>Setting the prediction to {duration} seconds</i>"); 
    }

    public void NotifyMixedCommandOutsideRound(string username)
    {
        Plugin.Logger.LogInfo("Mixed command received outside game state 0");
        NotifyPlayerVote(username, "you cannot use this command during podium");
    }

    public void NotifyTailsHeadsNoCoin(string username){
        NotifyPlayerVote(username,"wait for the prediction to start");
        Plugin.Logger.LogInfo($"tails/heads command was received without an ongoing prediction");
    }

    public void NotifyTailsHeadsInvalid(string username, string command, string arguments){
        NotifyPlayerVote(username,$"use a valid amount of points. e.g. !{command} 100");
        Plugin.Logger.LogInfo($"Argument of !{command} command is not valid. Arg: {arguments}");
    }

    public void NotifyTailsHeadsChange(string username, string prevCommand, string actCommand){
        NotifyPlayerVote(username, "don't change your prediction!");
        Plugin.Logger.LogInfo($"HandlingPrediction. {username} is trying to change from {prevCommand} to {actCommand}");
    }

    public void NotifyNotEnoughPoints(string username, uint currentPoints, uint pointsSent){
        NotifyPlayerVote(username,$"not enough points! Remaining: {currentPoints}");
        Plugin.Logger.LogInfo($"HandlingPrediction. {username} don't have enough points. pointsSent: {pointsSent}. currentPoints: {currentPoints}");
    }
    
    public void NotifySubmittedPrediction(string username, bool isFirstPrediction)
    {
        if (isFirstPrediction){
            NotifyPlayerVote(username, "registered vote");
        }
        else
        {
            NotifyPlayerVote(username, "adding points");
        }
        Plugin.Logger.LogInfo($"Prediction received from {username}");
    }

    public void NotifyRemainingPoints(string username, uint remainingPoints)
    {
        NotifyPlayerVote(username,$"remaining points: {remainingPoints}");
        Plugin.Logger.LogInfo($"Showing {username} remaining points: {remainingPoints}");
    }

    private static void NotifyPlayerVote(string username, string message)
    {
        string formattedUsername = (username.StartsWith("/") || username.StartsWith("!")) ? " " + username : username;
        ChatApi.SendMessage($"{formattedUsername}, {message}");
    }

    public void NotifyRefundMissingArguments(string[] arguments)
    {
        ChatApi.AddLocalMessage("<i>You are missing arguments. /coinrefund <points> <username></i>");
        Plugin.Logger.LogInfo($"Command is missing arguments. Arguments: {arguments}");
    }

    public void NotifyRefundNoPlayerInLobby(string username)
    {
        ChatApi.AddLocalMessage($"<i>No players found with username {username}. Ensure the player is in the lobby.</i>");
        Plugin.Logger.LogInfo($"No players found with username {username}");
    }

    public void NotifyInvalidRefundArguments(string points_str)
    {
        ChatApi.AddLocalMessage("<i>Please use a valid amount of points. /coinrefund <points> <username></i>");
        Plugin.Logger.LogInfo($"Command has an invalid amount of points {points_str}");
    }

    public void NotifyRefundingPlayerPoints(string username, uint points, ulong steamId)
    {
        ChatApi.AddLocalMessage($"Refunding {points} points to {username}");
        Plugin.Logger.LogInfo($"Refunding {points} points to {username}. PlayerID: {steamId}");
    }

    public void NotifyPredictionResults(string result, ulong totalPoints, ulong totalHeadsPoints, ulong totalTailsPoints){
        ChatApi.SendMessage($":party: It's {result}! :money:{totalPoints} points in total were sent to the winners. Use !coinpoints to check your points.");
        Plugin.Logger.LogInfo($"Ending Prediction WITH enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints}. result: {result}");
    }

    public void NotifyLackPoints(ulong totalHeadsPoints, ulong totalTailsPoints){
        ChatApi.SendMessage("Prediction ended with not enough predictions, redunding all the points!");
        Plugin.Logger.LogInfo($"Ending Prediction with NOT enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints}");
    }

    public void NotifyHostLost(){
        ChatApi.AddLocalMessage("<i>You lost host during the prediction! Prediction stoped.</i>");
        ChatApi.SendMessage($" {SteamClient.Name} lost host during the prediction! All the points will be refunded");
        Plugin.Logger.LogInfo("Ending Prediction because of host lost");
    }

    public void NotifyTypeOfDB(bool isGlobal){
        if (isGlobal)
            {
                Color textColor = new Color(1, 1, 1);
                Color backgroundColor = new Color((float)79/255, (float)238/255, 1, (float)0.7);
                MessengerApi.LogCustomColors("[ZeepCoin] Using global database", textColor, backgroundColor, 2.5f);
            }
            else
            {
                Color textColor = new Color(1, 1, 1);
                Color backgroundColor = new Color((float)112/255, 1, (float)93/255, (float)0.7);
                MessengerApi.LogCustomColors($"[ZeepCoin] Using host's specific database ({SteamClient.Name} DB)", textColor,backgroundColor,2.5f);
            }
    }
}