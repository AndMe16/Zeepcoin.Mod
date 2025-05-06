using Steamworks;
using UnityEngine;
using ZeepCoin.src;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;
using ZeepkistClient;
using System;

public class Coin_NotificationManager : MonoBehaviour
{
    private static void NotifyPlayerVote(ulong targetSteamID, string message)
    {
        ZeepkistNetwork.SendCustomChatMessage(sendToEveryone: false, optionalTargetSteamID: targetSteamID, message: message, hostnamePREFERREDCAPS: "COIN");
    }

    private static void NotifyBroadcast(string message)
    {
        ZeepkistNetwork.SendCustomChatMessage(sendToEveryone: true, optionalTargetSteamID: 0, message: "<#FFFFFF>" + message + "</color>", hostnamePREFERREDCAPS: "COIN");
    }

    public void NotifyNotHost()
    {
        ChatApi.AddLocalMessage("<i>You are not host!</i>");
        Plugin.Logger.LogInfo($"The player is not host");
    }

    public void NotifyStartingPrediction(float duration)
    {
        ChatApi.AddLocalMessage($"<i>Prediction started for {duration} seconds.</i>");
        NotifyBroadcast("Coin? The prediction has started! Use <#21B2ED>!heads</color> <#F0F071><points></color> <#FFFFFF>OR</color> <#FD4F06>!tails</color> <#F0F071><points></color> <#FFFFFF>to submit your prediction.</color>");
        Plugin.Logger.LogInfo($"Prediction started for {duration} seconds.");
    }

    public void NotifyInvalidCoinArguments()
    {
        Plugin.Logger.LogInfo("Invalid duration.");
        ChatApi.AddLocalMessage("<i>Specify the duration of the prediction in seconds! eg. /coinstart 60</i>");
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

    public void NotifyExceedingCoinTime()
    {
        ChatApi.AddLocalMessage("<i>Time exceeded 24 hours! Max 86399 seconds</i>");
        Plugin.Logger.LogInfo("Time exceeded 24 hours!");
    }

    public void NotifyNotOngoingPrediction()
    {
        Plugin.Logger.LogInfo("There is no active prediction!");
        ChatApi.AddLocalMessage("<i>There is no active prediction!</i>");
    }

    public void NotifyStopingPrediction()
    {
        NotifyBroadcast($"<#e33434>The prediction was stoped. All points will be refunded.</color>");
        Plugin.Logger.LogInfo("Prediction stoped");
    }

    public void NotifyInvalidAddArguments()
    {
        Plugin.Logger.LogInfo("Invalid duration.");
        ChatApi.AddLocalMessage("<i>Invalid duration. Please provide a valid amount of seconds you want to add, positive or negative! eg. /coinaddtime 60 or /coinaddtime -137</i>");
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

    public void NotifyMixedCommandOutsideRound(ulong targetSteamID, string username)
    {
        Plugin.Logger.LogInfo("Mixed command received outside game state 0");
        NotifyPlayerVote(targetSteamID, "<#e33434>You cannot use this command during podium</color>");
    }

    public void NotifyTailsHeadsNoCoin(ulong targetSteamID, string username)
    {
        NotifyPlayerVote(targetSteamID, "<#e33434>Wait for the prediction to start</color>");
        Plugin.Logger.LogInfo($"tails/heads command was received without an ongoing prediction");
    }

    public void NotifyTailsHeadsInvalid(ulong targetSteamID, string username, string command, string arguments)
    {
        NotifyPlayerVote(targetSteamID, $"<#e33434>Use a valid amount of points.</color> <#67db5e>e.g.</color> !{(command == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{command}</color> <#ffffff>100</color>");
        Plugin.Logger.LogInfo($"Argument of !{command} command is not valid. Arg: {arguments}");
    }

    public void NotifyTailsHeadsChange(ulong targetSteamID, string username, string prevCommand, string actCommand)
    {
        NotifyPlayerVote(targetSteamID, $"<#e33434>Don't change your prediction!</color> <#ffffff>Vote only for</color> {(prevCommand == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{prevCommand}</color>");
        Plugin.Logger.LogInfo($"HandlingPrediction. {username} is trying to change from {prevCommand} to {actCommand}");
    }

    public void NotifyNotEnoughPoints(ulong targetSteamID, string username, uint currentPoints, uint pointsSent)
    {
        NotifyPlayerVote(targetSteamID, $"<#e33434>Not enough points!</color> <#F0F071>Remaining: {currentPoints}</color>");
        Plugin.Logger.LogInfo($"HandlingPrediction. {username} don't have enough points. pointsSent: {pointsSent}. currentPoints: {currentPoints}");
    }

    public void NotifySubmittedPrediction(ulong targetSteamID, string username, bool isFirstPrediction, string command, uint points, uint remainingPoints)
    {
        if (isFirstPrediction)
        {
            NotifyPlayerVote(targetSteamID, $"<#67db5e>Registered vote</color> -> {(command == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{command}</color> <#ffffff>{points}</color>");
        }
        else
        {
            NotifyPlayerVote(targetSteamID, $"<#67db5e>Adding</color> <#ffffff>{points}</color> <#67db5e>points to</color> {(command == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{command}</color>");
        }
        Plugin.Logger.LogInfo($"Prediction received from {username}");
    }

    public void NotifyRemainingPoints(ulong targetSteamID, string username, uint remainingPoints)
    {
        NotifyPlayerVote(targetSteamID, $"Remaining points: <#F0F071>{remainingPoints}</color>");
        Plugin.Logger.LogInfo($"Showing {username} remaining points: {remainingPoints}");
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

    public void NotifyPredictionResults(string result, ulong totalPoints, ulong totalHeadsPoints, ulong totalTailsPoints)
    {
        NotifyBroadcast($":party: It's {(result == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{result}</color><#FFFFFF>! :money:</color><#f0f071>{totalPoints}</color> <#FFFFFF>points in total were sent to the winners. Use</color> <#f0f071>!coinpoints</color> <#FFFFFF>to check your points</color>");
        Plugin.Logger.LogInfo($"Ending Prediction WITH enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints}. result: {result}");
    }

    public void NotifyLackPoints(ulong totalHeadsPoints, ulong totalTailsPoints)
    {
        NotifyBroadcast("<#e33434>Prediction ended with not enough predictions <br>Refunding all the points!</color>");
        Plugin.Logger.LogInfo($"Ending Prediction with NOT enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints}");
    }

    public void NotifyHostLost()
    {
        ChatApi.AddLocalMessage("<i>You lost host during the prediction! Prediction stoped.</i>");
        NotifyBroadcast($" <#e33434>{SteamClient.Name} lost host during the prediction! <br>Refunding all the points!</color>");
        Plugin.Logger.LogInfo("Ending Prediction because of host lost");
    }

    public void NotifyTypeOfDB(bool isGlobal)
    {
        if (isGlobal)
        {
            Color textColor = new(1, 1, 1);
            Color backgroundColor = new((float)79 / 255, (float)238 / 255, 1, (float)0.7);
            MessengerApi.LogCustomColors("[ZeepCoin] Using global database", textColor, backgroundColor, 2.5f);
        }
        else
        {
            Color textColor = new(1, 1, 1);
            Color backgroundColor = new((float)112 / 255, 1, (float)93 / 255, (float)0.7);
            MessengerApi.LogCustomColors($"[ZeepCoin] Using host's specific database ({SteamClient.Name} DB)", textColor, backgroundColor, 2.5f);
        }
    }

    internal void ListCoinCommands()
    {
        ChatApi.AddLocalMessage("<i>Coin commands:</i>" +
            "<br>/coinstart <#F0F071><duration in seconds></color>" +
            "<br>/coinstop" +
            "<br>/coinaddtime <#F0F071><additional seconds></color>" +
            "<br>/coinsettime <#F0F071><duration in seconds></color>" +
            "<br>/coinrefund <#F0F071><points> <username></color>" +
            "<br>!coinpoints");
        Plugin.Logger.LogInfo("Listing coin commands");
    }
}