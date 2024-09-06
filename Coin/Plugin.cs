using BepInEx;
using HarmonyLib;
using ZeepSDK.ChatCommands;
using Steamworks;
using ZeepkistClient;
using ZeepSDK.Chat;
using System;
using System.Timers;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepSDK.Storage;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine.Networking;
using ZeepSDK.Messaging;
using UnityEngine;
using System.Linq;



namespace Coin;

[BepInPlugin("andme123.coin", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]

[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{   
    private Harmony harmony;
    private ConfigEntry<int> configPointsRechargeInterval;
    private ConfigEntry<int> configPointsRechargePoints;
    private ConfigEntry<bool> configUseServer; 
    
    private event EventHandler<SettingChangedEventArgs> OnSettingChanged;
    

    private string baseUrl = "https://zeep-coin.onrender.com"; 

    private void Awake()
    {
        harmony = new Harmony("andme123.coin");
        harmony.PatchAll();

        // Plugin startup logic
        Logger.LogInfo($"Plugin andme123.coin is loaded!");
        Configuration_Init();
        CoinFlow();

    }

    private Timer countdownTimer; 
    private bool coinStarted = false;
    private int countdownLeft;
    private bool coinPaused = false;
    private Dictionary<ulong, (string username, string commandType, int totalPointsSent)> userVotes = new Dictionary<ulong, (string, string, int)>();
    private Dictionary<ulong, int > totalPointsDataDictionary = new Dictionary<ulong, int>();
    private List<ulong> voteOrder = new List<ulong>();
    private List<string> rowTexts = new List<string> {"<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>"}; 
    private List<ulong> rowPlayerIds = new List<ulong> {0,0,0,0,0};
    private int totalTailsPoints = 0;
    private int totalHeadsPoints = 0;

    private string predictionBar;
    private string heads_lines;
    private string tails_lines;
    private double heads_ratio;
    private double tails_ratio;
    private string heads_ratio_str;
    private string tails_ratio_str;
    private int heads_voters = 0;
    private int tails_voters = 0;
    private Timer asignPointsTimer;
    private bool pointsPaused = false;
    private string serverMessage;
    private bool usingLocal=true;
    private bool isFirstCon = true;
    private Coroutine pingCoroutine; // Reference to the running coroutine

   
                                    
    private void Configuration_Init(){
        
        configPointsRechargeInterval = Config.Bind("Points Recharge",    
                                         "Time interval (sec)", 
                                         120, 
                                         new ConfigDescription("Min.60 Max.3600 Time interval for recharging points", new AcceptableValueRange<int>(60, 3600))); 
        configPointsRechargePoints = Config.Bind("Points Recharge",    
                                         "Points added per recharge", 
                                         10, 
                                         new ConfigDescription("Min.5 Max. 100 Amount of points that will be added per recharge for each player", new AcceptableValueRange<int>(5, 100)));
        configUseServer = Config.Bind("Server",
                                        "Use server storage",
                                        false,
                                        "Use centralized server to save the remaining points of each player. If false they will be saved in a local file");
        configPointsRechargeInterval.SettingChanged += SettingChangedHandler;
        configUseServer.SettingChanged += SettingChangedHandler;
    }
    private void CoinFlow()
    {
        IModStorage modStorage = StorageApi.CreateModStorage(this);
        System.Random random = new();

        RegistercoinCommand();
        RegistercoinStopCommand();
        RegistercoinRefundCommand();
        RegisterTailsCommand();
        RegisterHeadsCommand();
        RegisterCheckPointsCommand();
        SubscribeToEvents();

        void RegistercoinCommand()
        {
            LocalChatCommandCallbackDelegate coinCommandCallback = new(CoinCommandCallback);
            ChatCommandApi.RegisterLocalChatCommand("/", "coin", "/coin <duration of prediction in seconds>. Start a coin flip prediction", coinCommandCallback);
        }

        void RegistercoinStopCommand()
        {
            LocalChatCommandCallbackDelegate coinStopCommandCallback = new(OnStopCommand);
            ChatCommandApi.RegisterLocalChatCommand("/", "coin_stop", "Stop the prediction", coinStopCommandCallback);
        }

        void RegistercoinRefundCommand(){
            LocalChatCommandCallbackDelegate coinRefundCommandCallback = new(OnRefundCommand);
            ChatCommandApi.RegisterLocalChatCommand("/", "coin_refund", "/coin_refund <points> <username>. Refund points to a player", coinRefundCommandCallback);
        }

        void RegisterTailsCommand(){
            MixedChatCommandCallbackDelegate tailsCommandCallback = new(OnTailsCommand);
            ChatCommandApi.RegisterMixedChatCommand("!","tails","!tails <points>. Choose tails using <poins>",tailsCommandCallback);
        }

        void RegisterHeadsCommand(){
            MixedChatCommandCallbackDelegate headsCommandCallback = new(OnHeadsCommand);
            ChatCommandApi.RegisterMixedChatCommand("!","heads","!heads <points>. Choose heads using <poins>",headsCommandCallback);
        }

        void RegisterCheckPointsCommand(){
            MixedChatCommandCallbackDelegate checkPointsCommandCallback = new(OnCheckPointsCommand);
            ChatCommandApi.RegisterMixedChatCommand("!","check_points","Check remaining coin points",checkPointsCommandCallback);
        }

        void SubscribeToEvents()
        {
            
            MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
            RacingApi.LevelLoaded += OnRoundStarted;
            RacingApi.RoundEnded += OnRoundEnded;
            MultiplayerApi.JoinedRoom += OnJoinedRoom;
            OnSettingChanged += OnSettingsChanged;
            ZeepkistNetwork.MasterChanged += OnMasterChanged;
            MultiplayerApi.PlayerJoined += OnPlayerJoined;
            
        }

        void CoinCommandCallback(string arguments)
        {
            Logger.LogMessage($"Received a coin command from {SteamClient.Name} with arguments {arguments}");
            if (!ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                NotifyNotHost();
                return;
            }

            if (pointsPaused){
                Logger.LogInfo($"/coin command received in between rounds");
                return;
            }

            if (!int.TryParse(arguments, out int args_int) || args_int == 0 ||(args_int < 0 && !coinStarted))
            {
                ChatApi.AddLocalMessage("Specify the duration of the prediction in seconds! e.g. /coin 60");
                Logger.LogInfo($"Argument of /coin command is not valid. Arg: {arguments}");
                return;
            }

            if (args_int > 86399) // Max. 23:59:59
            {
                ChatApi.AddLocalMessage("Time in seconds is too large!");
                Logger.LogInfo($"Argument of /coin command is too big. args_int: {args_int}");
                return;
            }

            if (coinStarted)
            {
                if ((args_int + countdownLeft) > 86399){ // Max. 23:59:59
                    ChatApi.AddLocalMessage("Time got too large!");
                    Logger.LogInfo($"Total countdown time is too large. args_int: {args_int}. countdownLeft: {countdownLeft}");
                    return;
                }
                if ((args_int + countdownLeft) <= 0){
                    args_int = -countdownLeft;
                }
                ChatApi.AddLocalMessage($"{(args_int>0? "Adding":"Subtracting")} {(args_int>0? args_int:-args_int)} seconds to the prediction!");
                countdownLeft += args_int;
                Logger.LogInfo($"{(args_int>0? "Adding":"Subtracting")} {(args_int>0? args_int:-args_int)} seconds to the prediction. countdownLeft: {countdownLeft}");
                return;
            }
            countdownLeft = args_int;
            StartPrediction();
        }

        void StartPrediction()
        {
            coinStarted = true;
            ChatApi.SendMessage("coin? The prediction has started! Use !heads <points> OR !tails <points> to submit your prediction.");
            Logger.LogMessage($"Starting prediction. countdownLeft: {countdownLeft}");
            StartCountdown();
        }

        void StartCountdown()
        {
            Logger.LogInfo("Starting countdown");
            countdownTimer = new Timer(1000); 
            countdownTimer.Elapsed += OnTimedEvent;
            countdownTimer.AutoReset = true;
            countdownTimer.Enabled = true;
            UpdatePredictionBar();
        }

        void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (countdownLeft > 0 && ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                UpdateCountdown();
            }
            else
            {
                EndPrediction();
            }
        }

        void UpdateCountdown()
        {
            TimeSpan timeLeft = TimeSpan.FromSeconds(countdownLeft);
            serverMessage = $"/servermessage white 2 <size=+7><voffset=-9em><align=\"left\"><line-height=0%><pos=3em>Prediction time left: <#F0F071>{timeLeft.ToString(@"hh\:mm\:ss")}"
                                +predictionBar;
            ChatApi.SendMessage(serverMessage);
            countdownLeft--;
        }

        void UpdatePredictionBar(){
            Logger.LogInfo("Updating servermessage PredictionBar");
            DefineLinesBar();
            predictionBar= $"<br><line-height=90%><pos=5em><#FFFFFF><u><alpha=#00>|<pos=22.5em>|</u>" //Stats
                          +$"<br><line-height=90%><#FFFFFF><u><alpha=#00>|<pos=5em><#FFFFFF>|<#21B2ED>Heads <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>Tails<pos=22.5em><#FFFFFF>|</u>" //Stats                        
                          +$"<br><line-height=90%><#FFFFFF><u>|Voters<pos=5em>|<#21B2ED>{heads_voters} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{tails_voters}<pos=22.5em><#FFFFFF>|</u>"                         
                          +$"<br><line-height=90%><#FFFFFF><u>|Points<pos=5em>|<#21B2ED>{totalHeadsPoints} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{totalTailsPoints}<pos=22.5em><#FFFFFF>|</u>"                          
                          +$"<br><line-height=90%><#FFFFFF><u>|Ratio<pos=5em>|<#21B2ED>{heads_ratio_str} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{tails_ratio_str}<pos=22.5em><#FFFFFF>|</u>"                  
                          +$"<br><line-height=90%>Vote using <#21B2ED>!heads <#F0F071><points> <#FFFFFF>or <#FD4F06>!tails <#F0F071><points>"
                          +$"<br><#FFFFFF>Use <#F0F071>!check_points <#FFFFFF>to check your remaining points"
                          +$"<br>Recharging <#F0F071>{configPointsRechargePoints.Value} points <#FFFFFF>every <#F0F071>{configPointsRechargeInterval.Value} seconds"
                          +((heads_voters!=0||tails_voters!=0)?$"<br><line-height=90%><#FFFFFF><u><alpha=#00>|<pos=8em><pos=25em>|</u>":"<br><line-height=90%>") 
                          +((heads_voters!=0||tails_voters!=0)? $"<br><line-height=90%><#FFFFFF><u>|<#F0F071>User <pos=8em><#FFFFFF>|<#F0F071>Side <pos=12em><#FFFFFF>|<#F0F071>Sent<pos=18em><#FFFFFF>|<#F0F071>Remaining<pos=25em><#FFFFFF>|</u>" : "<br><line-height=90%>") //Voters
                          +rowTexts[0] //Voters
                          +rowTexts[1] //Voters
                          +rowTexts[2] //Voters
                          +rowTexts[3]; //Voters
                          
        }


        void DefineLinesBar(){
            int totalPoints = totalHeadsPoints+totalTailsPoints;
            
            if(totalPoints>0){
                double headsPercentage = (double)(totalHeadsPoints* 10) / (totalHeadsPoints+totalTailsPoints);
                int heads_lines_count = (int)Math.Round(headsPercentage, MidpointRounding.AwayFromZero);
                
                double tailsPercentage = (double)(totalTailsPoints* 10)/(totalHeadsPoints+totalTailsPoints);
                int tails_lines_count = (int)Math.Round(tailsPercentage, MidpointRounding.AwayFromZero);

                heads_lines= new string('-',heads_lines_count);
                tails_lines = new string('-',tails_lines_count);
            } else{
                heads_lines= new string('-',5);
                tails_lines = new string('-',5);
            }
            
            // Winner ratio -> (WinnerBets+LoserBets)/(WinnerBets)
            
            if (totalHeadsPoints>0){
                heads_ratio = (double)(totalHeadsPoints + totalTailsPoints) / totalHeadsPoints;
                heads_ratio = Math.Round(heads_ratio, 2, MidpointRounding.AwayFromZero);

                heads_ratio_str = "1:"+heads_ratio.ToString();
            } else{
                heads_ratio_str = "-:-";
            }
            
            if (totalTailsPoints>0){
                tails_ratio = (double)(totalHeadsPoints + totalTailsPoints) / totalTailsPoints;
                tails_ratio = Math.Round(tails_ratio , 2, MidpointRounding.AwayFromZero);
    
                tails_ratio_str = "1:"+tails_ratio.ToString();
            } else{
                tails_ratio_str = "-:-";
            }
            Logger.LogInfo($"The ratios where defined. heads_ratio {heads_ratio}. tails_ratio: {tails_ratio}. totalHeadsPoints{totalHeadsPoints}. ");
        }

        void UpdateVotersTable(){
            
            int count = Math.Min(voteOrder.Count, 4);

            ulong playerId = voteOrder[0];

            var vote = userVotes[playerId];
            string truncatedUsername = TruncateUsername(vote.username, 10);
            int totalPointsRemaining = totalPointsDataDictionary[playerId];

            if(rowPlayerIds[0] ==playerId){
                rowTexts[0] = $"<br><line-height=90%><#FFFFFF><u>|{truncatedUsername}<pos=8em>|{(vote.commandType== "heads" ? "<#21B2ED>":"<#FD4F06>")}{vote.commandType}<#FFFFFF><pos=12em>|{vote.totalPointsSent}<pos=18em>|{totalPointsRemaining}<pos=25em>|</u>";
                rowPlayerIds[0] = playerId; // Update the player ID for the top row
                return;
            }

            for (int i = count; i>0; i--)
            {  
                if (rowPlayerIds[i]==playerId){
                    for(int j=i+1; j<count;j++){
                        rowTexts[j]=rowTexts[j+1];
                        rowPlayerIds[j] = rowPlayerIds[j+1];
                    }

                }
                //Logger.LogInfo($"Updating row {i} with {rowTexts[i-1]}");  
                rowTexts[i] = rowTexts[i-1];
                rowPlayerIds[i] = rowPlayerIds[i-1];
                
                
            }
            
            rowTexts[0] = $"<br><line-height=90%><#FFFFFF><u>|{truncatedUsername}<pos=8em>|{(vote.commandType== "heads" ? "<#21B2ED>":"<#FD4F06>")}{vote.commandType}<#FFFFFF><pos=12em>|{vote.totalPointsSent}<pos=18em>|{totalPointsRemaining}<pos=25em>|</u>";
            rowPlayerIds[0] = playerId; // Update the player ID for the top row
        }

        string TruncateUsername(string username, int maxLength)
        {
            if (username.Length > maxLength)
            {
                return username.Substring(0, maxLength) + "...";
            }
            return username;
        }

        void EndPrediction()
        {
            countdownTimer?.Stop();
            countdownTimer?.Dispose();    
            if(!ZeepkistNetwork.LocalPlayerHasHostPowers()){
                ChatApi.AddLocalMessage("You lost host during the prediction! Prediction stoped.");
                ChatApi.SendMessage($"{SteamClient.Name} lost host during the prediction! All the points will be refunded");
                Logger.LogMessage("Ending Prediction because of host lost");
                RefundPoints();
            } else if(totalTailsPoints==0 || totalHeadsPoints==0){
                ChatApi.SendMessage("/servermessage white 20 There are not enough coins! Refunding points");
                ChatApi.SendMessage("Prediction ended");
                Logger.LogMessage($"Ending Prediction with NOT enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints} tails {totalTailsPoints}");
                RefundPoints();
            }

            else {
                string result = random.Next(0, 2) == 0 ? "Heads" : "Tails";
                ChatApi.SendMessage($"/servermessage {(result == "Heads" ? "blue" : "red")} 20 The coin has been flipped! It's {result}!");
                ChatApi.SendMessage($":party: It's {result}! :money:{totalTailsPoints+totalHeadsPoints} points in total were sent to the winners. Use !check_points to check your points.");
                Logger.LogMessage($"Ending Prediction WITH enough points. tails: {totalTailsPoints} heads: {totalHeadsPoints}. result: {result}");
                Pay(result);
            }
            ClearVars();
            
        }

        void NotifyNotHost()
        {
            ChatApi.AddLocalMessage("You are not host! No coin for you :(");
            Logger.LogInfo($"The player is no host. Need it for /coin command");
        }

        void OnDisconnectedFromGame()
        {
            Logger.LogMessage($"Player disconnected from game. coinStarted {coinStarted}");
            if (coinStarted)
            {
                countdownTimer?.Stop();
                countdownTimer?.Dispose(); 
                RefundPoints();
                ClearVars();
                
            }
            
            stopAsignPointsTimer();
            pointsPaused = false;
            StopPingingServer();

            
        }
        void OnStopCommand(string argument)
        {
            Logger.LogMessage($"Received a coin_stop command from {SteamClient.Name}. coinStarted {coinStarted}");
            if (coinStarted && ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                Logger.LogMessage("Stoping ongoing prediction");
                countdownTimer?.Stop();
                countdownTimer?.Dispose(); 
                RefundPoints();
                ClearVars();
                ChatApi.SendMessage($"{SteamClient.Name} canceled the prediction. All the points will be refunded");
                ChatApi.SendMessage($"/servermessage white 20 Bad Host!");
            }
            else if (ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                ChatApi.AddLocalMessage("Please start a prediction first!");
            }
        }

        void OnTailsCommand(bool isLocal, ulong playerId, string arguments){
            if (!ZeepkistNetwork.LocalPlayerHasHostPowers()) return;
            handleHeadsTailsCommand(isLocal,playerId,arguments,"tails");
        }

        void OnHeadsCommand(bool isLocal, ulong playerId, string arguments){
            if (!ZeepkistNetwork.LocalPlayerHasHostPowers()) return;
            handleHeadsTailsCommand(isLocal,playerId,arguments,"heads");
        }

        void handleHeadsTailsCommand(bool isLocal,ulong playerId, string arguments, string command){
            List<ZeepkistNetworkPlayer> playerList = ZeepkistNetwork.PlayerList;
            string username ="";
            if(isLocal){
                username = SteamClient.Name;
                playerId = SteamClient.SteamId;
            } 
            else{
                foreach (var player in playerList)
                {
                    if (player.SteamID==playerId){
                        username = player.Username;
                        break;
                    }
                }
            }
            

            Logger.LogMessage($"A !{command} was received from player {playerId} {username} using {arguments} points");
            
            if (!coinStarted)
            {
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, wait for the prediction to start!");
                return;
            }
            
            if (!int.TryParse(arguments, out int args_int) || args_int<=0)
            {
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, use a valid amount of points. e.g. !{command} 100");
                Logger.LogInfo($"Argument of !{command} command is not valid. Arg: {arguments}");
                return;
            }

            HandlePrediction(playerId, username,command, args_int);
        }

        void OnCheckPointsCommand(bool isLocal, ulong playerId, string arguments){
            if (!ZeepkistNetwork.LocalPlayerHasHostPowers()) return;
            if(isLocal){
                playerId = SteamClient.SteamId;
            } 
            List<ZeepkistNetworkPlayer> playerList = ZeepkistNetwork.PlayerList;
            string username ="";
            foreach (var player in playerList)
            {
                if (player.SteamID==playerId){
                    username = player.Username;
                    break;
                }
            }
            Logger.LogMessage($"A check_points command was received from player {playerId} {username}");
            ShowRemainingPoints(playerId,username);
        }

        void HandlePrediction(ulong playerId,string username, string commandType, int pointsSent)
        {
            bool IsFirstVote = true;
            int totalPointsSent = pointsSent;

            if (coinPaused)
            {
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, wait for the prediction to be resumed!");
                Logger.LogInfo($"HandlingPrediction. {username} used the command in between rounds.");
                return;
            }

            int currentPoints = GetUserPoints(playerId,username);
            
            if (userVotes.TryGetValue(playerId, out var existingVote))
            {
                if (!string.Equals(existingVote.commandType, commandType, StringComparison.OrdinalIgnoreCase))
                {
                    //ChatApi.SendMessage($"{username}, don't change your prediction from {existingVote.commandType} to {commandType}!");
                    ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, don't change your prediction!");
                    Logger.LogInfo($"HandlingPrediction. {username} is trying to change from {existingVote.commandType} to {commandType}");
                    return;
                }
                totalPointsSent = existingVote.totalPointsSent + pointsSent;
                IsFirstVote = false;
            }
            
            
            int pointsRemaining = currentPoints - pointsSent;
            if (pointsRemaining < 0)
            {
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, not enough points! Remaining: {currentPoints}");
                Logger.LogInfo($"HandlingPrediction. {username} don't have enough points. pointsSent: {pointsSent}. currentPoints: {currentPoints}");
                return;
            }

            Logger.LogInfo($"HandlingPrediction. Storing {username} vote info. commandType: {commandType}. totalPointsSent: {totalPointsSent}. pointsRemaining: {pointsRemaining}");
            voteOrder.Insert(0,playerId);
            userVotes[playerId] = (username, commandType, totalPointsSent);
            totalPointsDataDictionary[playerId] = pointsRemaining;
            if (commandType == "tails"){
                totalTailsPoints += pointsSent;
            } else {
                totalHeadsPoints += pointsSent;
            }
            if (IsFirstVote){
                if (commandType == "tails"){
                    tails_voters += 1;
                } else {
                    heads_voters += 1;
                }
                 
                //ChatApi.SendMessage($"{username} chose {commandType}. Total points used: {totalPointsSent}. Remaining points: {pointsRemaining}");
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, registered vote");
            } else{
                //ChatApi.SendMessage($"{username}, adding {pointsSent} points. Total points used: {totalPointsSent}. Remaining points: {pointsRemaining}");
                ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)} adding points");
            }
            UpdateVotersTable();
            UpdatePredictionBar();
        }

        void ShowRemainingPoints(ulong playerId, string username)
        {
            Logger.LogInfo($"Showing remaning points to the user {playerId} {username}");
            int remainingPoints = GetUserPoints(playerId, username);
            ChatApi.SendMessage($"{((username.StartsWith("/")||username.StartsWith("!"))? (" "+username):username)}, remaining points: {remainingPoints}");
        }

        int GetUserPoints(ulong playerId,string username)
        {
                if (totalPointsDataDictionary != null)
                {
                    if (totalPointsDataDictionary.TryGetValue(playerId, out int totalPointsData)){
                        Logger.LogInfo($"Got {username} points from dictionary. {totalPointsData} points");
                        return totalPointsData; 
                    }
                }
            Logger.LogInfo($"Loaded default value because user is not found");
            // Return a default value if the user is not found
            return 1000;
        }

        void OnRoundStarted()
        {
            Logger.LogMessage($"Round started. countdownLeft: {countdownLeft} coinStarted: {coinStarted} coinPaused: {coinPaused}");
            if (countdownLeft > 0 && coinStarted && ZeepkistNetwork.LocalPlayerHasHostPowers() && coinPaused) // Check if there's an ongoing prediction
            {
                StartCountdown(); // Resume the countdown
                coinPaused = false;
                ChatApi.SendMessage("Round started. Prediction has been resumed.");
            }
            if(pointsPaused&&ZeepkistNetwork.LocalPlayerHasHostPowers()){
                Logger.LogInfo($"Resuming asignPointsTimer");
                asignPointsTimer?.Start();
                pointsPaused = false;
            }
            
        }

        void OnRoundEnded()
        {
            Logger.LogMessage($"Round ended. countdownLeft: {countdownLeft} coinStarted: {coinStarted}");
            if (countdownLeft > 0 &&coinStarted && ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                countdownTimer?.Stop(); 
                countdownTimer?.Dispose();
                coinPaused = true;
                ChatApi.SendMessage("Round ended. Prediction has been paused.");
                Logger.LogInfo("Stoping countdownTimer");
            } 
            asignPointsTimer?.Stop();
            pointsPaused = true;
            Logger.LogInfo("Stoping asignPointsTimer");   

        }

        void RefundPoints(){
            Logger.LogMessage("Refunding all points");

            // Refund Points per entry
            foreach (var entry in userVotes)
            {
                totalPointsDataDictionary[entry.Key] = totalPointsDataDictionary[entry.Key] + entry.Value.totalPointsSent;
            }
            SaveAllData();
            
        }

        void Pay(string result) {
            Logger.LogMessage($"Returning points to the winners {result}. heads_ratio: {heads_ratio} tails_ratio: {tails_ratio}");
            foreach (var entry in userVotes) {
                string result_lower = result.ToLower();
                if (entry.Value.commandType == result_lower) {
                    double addedPoints;
                    int addedPointsRounded;

                    if (result_lower == "heads") {
                        addedPoints = entry.Value.totalPointsSent * heads_ratio;
                        addedPointsRounded = (int)Math.Round(addedPoints, MidpointRounding.AwayFromZero);
                    } else {
                        addedPoints = entry.Value.totalPointsSent * tails_ratio;
                        addedPointsRounded = (int)Math.Round(addedPoints, MidpointRounding.AwayFromZero);
                    }

                    Logger.LogInfo($"{entry.Key} {entry.Value.username} totalPointsSent: {entry.Value.totalPointsSent} " +
                                $"{result}_ratio: {(result_lower == "heads" ? heads_ratio : tails_ratio)} " +
                                $"addedPoints: {addedPoints} addedPointsRounded: {addedPointsRounded}");

                    totalPointsDataDictionary[entry.Key] += addedPointsRounded;
                }
            }
            SaveAllData();
        }


        void OnJoinedRoom(){
            Logger.LogMessage("Joining Room");
            if(ZeepkistNetwork.LocalPlayerHasHostPowers()){
                startAsignPointsTimer();
                LoadAllData();
                if(configUseServer.Value){
                    pingCoroutine = StartCoroutine(PingServerRoutine());
                }
            }
            
            
        }

        void startAsignPointsTimer ()
        {
            Logger.LogInfo($"Starting startAsignPointsTimer. Interval {configPointsRechargeInterval.Value}");
            try{
                asignPointsTimer = new Timer(configPointsRechargeInterval.Value*1000); 
                asignPointsTimer.Elapsed += OnPointsTimedEvent;
                asignPointsTimer.AutoReset = true;
                asignPointsTimer.Enabled = true;
            } catch (Exception ex){
                Logger.LogError($"Failed to start the timer: {ex.Message}");
            }  

        }

        void stopAsignPointsTimer(){
            Logger.LogMessage("Stoping startAsignPointsTimer");
            asignPointsTimer?.Stop(); 
            asignPointsTimer?.Dispose();
        }

        void OnPointsTimedEvent(object source, ElapsedEventArgs e)
        {
            if (ZeepkistNetwork.LocalPlayerHasHostPowers()){
                Logger.LogMessage($"Recharging {configPointsRechargePoints.Value} points per player");
                int remainingPoints;
                List<ZeepkistNetworkPlayer> playerList = ZeepkistNetwork.PlayerList;
                foreach (var player in playerList)
                {
                    remainingPoints = totalPointsDataDictionary[player.SteamID] + configPointsRechargePoints.Value;
                    if (totalPointsDataDictionary.ContainsKey(player.SteamID)){
                        totalPointsDataDictionary[player.SteamID] = remainingPoints;
                    }
                    
                }
                UpdateVotersTable();
                modStorage.SaveToJson("LocalPlayersPoints", totalPointsDataDictionary);
            }
        }

        void OnRefundCommand(string argument){
            if(!ZeepkistNetwork.LocalPlayerHasHostPowers()){
                ChatApi.AddLocalMessage("You are not host!");
                return;
            }
       
            string[] arguments = argument.Split(" ");
            if(arguments.Length <= 1){
                ChatApi.AddLocalMessage("You are missing arguments. /coin_refund <points> <username>");
                return;
            }
            
            string points_str = arguments[0];
            string username = arguments[1];
            if(!(int.TryParse(points_str, out int points) && points >0)){
                ChatApi.AddLocalMessage("Please use a valid amount of points. /coin_refund <points> <username>");
                return;
            }
            
            List<ZeepkistNetworkPlayer> playerList = ZeepkistNetwork.PlayerList;
            List<ulong> playerIDs = new List<ulong>();
            foreach (var player in playerList)
            {
                if (player.Username==username){
                    playerIDs.Add(player.SteamID);
                }
            }
            if (playerIDs.Count == 0)
            {
                ChatApi.AddLocalMessage($"No players found with username {username}. Ensure the player is in the lobby.");
            }
            else{
                foreach (var playerID in playerIDs)
                {
                    if (totalPointsDataDictionary.ContainsKey(playerID))
                    {
                        totalPointsDataDictionary[playerID] = totalPointsDataDictionary[playerID] + points;
                        ChatApi.AddLocalMessage($"Refunding {points} to {username}");
                    }
                    else{
                        ChatApi.AddLocalMessage($"{username} has never participated in a coin prediction!");
                    }

                }
            }
            
        }

        void ClearVars(){
            coinStarted = false;
            countdownLeft = 0;
            coinPaused = false;
            countdownTimer?.Stop();
            countdownTimer?.Dispose(); 
            userVotes = new Dictionary<ulong, (string, string, int)>();
            voteOrder = new List<ulong>();
            rowTexts = new List<string> {"<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>"}; 
            rowPlayerIds = new List<ulong> {0,0,0,0,0};
            totalTailsPoints = 0;
            totalHeadsPoints = 0;
            heads_voters = 0;
            tails_voters = 0;
        }

        void SaveAllData(){
        if(configUseServer.Value){
                if(!usingLocal){
                    StartCoroutine(Save_Data_Server(totalPointsDataDictionary));
                    return;
                }
        }
        modStorage.SaveToJson("LocalPlayersPoints", totalPointsDataDictionary);
        }

        void LoadAllData()
        {
            // Extract player IDs from the current lobby
            List<ulong> playerIdsInLobby = new List<ulong>();
            foreach (var player in ZeepkistNetwork.PlayerList)
            {
                playerIdsInLobby.Add(player.SteamID); // Assuming 'playerId' is the ulong identifier
            }

            if (configUseServer.Value)
            {
                // Only load data for players in the lobby from the server
                StartCoroutine(Load_Data_Server(playerIdsInLobby, (loadedData) =>
                {
                    if (loadedData != null && loadedData.Count > 0)
                    {
                        totalPointsDataDictionary = loadedData;
                        usingLocal = false;
                        Logger.LogInfo("Loaded points from the server for players in the lobby.");
                    }
                    else
                    {
                        LoadFromLocalFile(playerIdsInLobby); // Fall back to local if server fails
                        usingLocal = true;
                    }
                }));
            }
            else
            {
                // If not using server, load from local storage for players in the lobby
                LoadFromLocalFile(playerIdsInLobby);
                usingLocal = true;
            }
        }

        void LoadFromLocalFile(List<ulong> playerIdsInLobby)
        {
            if (modStorage.JsonFileExists("LocalPlayersPoints"))
            {
                var allPointsData = modStorage.LoadFromJson<Dictionary<ulong, int>>("LocalPlayersPoints");
                
                // Filter to only include players in the lobby
                totalPointsDataDictionary = allPointsData
                    .Where(entry => playerIdsInLobby.Contains(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
                
                Logger.LogInfo("Loaded points from local file for players in the lobby.");
            }
            else
            {
                Logger.LogInfo("Local file LocalPlayersPoints.json not found, no points loaded.");
            }
        }


        void OnSettingsChanged(object sender, SettingChangedEventArgs e)
        {
            Logger.LogInfo($"Setting changed: {e.ChangedSetting.Definition.Key}");
            if((e.ChangedSetting == configPointsRechargeInterval) && MultiplayerApi.IsPlayingOnline ){
                stopAsignPointsTimer();
                startAsignPointsTimer();
            }
            else if((e.ChangedSetting == configUseServer)&&MultiplayerApi.IsPlayingOnline&&configUseServer.Value){
                pingCoroutine = StartCoroutine(PingServerRoutine());
            } 
        }
        void OnMasterChanged(ZeepkistNetworkPlayer player)
        {
            Logger.LogInfo($"The new master of the lobby is: {player.Username}");
            if(player.IsLocal){
                startAsignPointsTimer();
                LoadAllData();
                if(configUseServer.Value){
                    pingCoroutine = StartCoroutine(PingServerRoutine());
                }
            }
        }

        // Method to stop pinging the server
        void StopPingingServer()
        {
            if (pingCoroutine != null)
            {
                StopCoroutine(pingCoroutine); // Stop the running ping coroutine
                Debug.Log("Stopped pinging the server.");
            }
        }

        void OnPlayerJoined(ZeepkistNetworkPlayer player)
        {
            if(ZeepkistNetwork.LocalPlayerHasHostPowers()&&configUseServer.Value&&!usingLocal){
                GetUserPointsServer(player.SteamID);
            }
        }

        void GetUserPointsServer(ulong playerId)
        {
            StartCoroutine(Load_Single_Player_Points(playerId, 
                    (points) => {
                        totalPointsDataDictionary[playerId] = points;
                    }, 
                    (error) => {
                        Logger.LogWarning($"Failed to load points for player {playerId}: {error}");
                    }));
        }


    }


    IEnumerator Load_Data_Server(List<ulong> playerIdsInLobby, System.Action<Dictionary<ulong, int>> onSuccess)
    {
        Logger.LogMessage($"Loading data from server for players in the lobby");

        // Define the URL for the API endpoint that handles player filtering
        string url = $"{baseUrl}/points/players"; 

        // Create a JSON payload with the list of player IDs to send to the server
        string jsonPayload = JsonConvert.SerializeObject(new { playerIds = playerIdsInLobby });

        // Create a UnityWebRequest for a POST request to send the player IDs
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = new System.Text.UTF8Encoding().GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Send the request and wait for the response
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Deserialize the response into a dictionary
                string jsonResponse = request.downloadHandler.text;
                Dictionary<ulong, int> totalPointsDataDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, int>>(jsonResponse);
                
                Logger.LogInfo("Data loaded from server:");
                foreach (var kvp in totalPointsDataDictionary)
                {
                    Logger.LogInfo($"Player ID: {kvp.Key}, Points: {kvp.Value}");
                }

                // Call the success callback with the loaded data
                onSuccess(totalPointsDataDictionary);
            }
            else
            {
                Logger.LogWarning($"Error loading points data: {request.error}");
                // If there is an error, call the callback with an empty dictionary
                onSuccess(new Dictionary<ulong, int>());
            }
        }
    }

    IEnumerator Save_Data_Server(Dictionary<ulong, int> totalPointsDataDictionary)
    {
        foreach (var kvp in totalPointsDataDictionary)
        {
            Logger.LogInfo($"Player ID: {kvp.Key}, Points: {kvp.Value}");
        }
        Logger.LogInfo($"Saving data to server");
        string url = $"{baseUrl}/points/all";

        string jsonData = JsonConvert.SerializeObject( totalPointsDataDictionary );
        Logger.LogInfo($"Serialized JSON Data: {jsonData}");
        using (UnityWebRequest request = new(url, "POST"))
        {
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(jsonToSend);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Logger.LogInfo("All points data saved successfully.");
            }
            else
            {
                Logger.LogError($"Error saving points data: {request.error}");
                ChatApi.AddLocalMessage("There was an error while saving the points to the server!");
            }
        }
        
    }

    IEnumerator PingServer()
    {
        string url = $"{baseUrl}/ping";
        if(isFirstCon){
            MessengerApi.Log("Connecting to the Coin Server");
        }
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
                webRequest.timeout = 80;  
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                MessengerApi.LogError("Failed connecting with the Coin Server");
                Logger.LogError("Error Pinging Server: " + webRequest.error);
            }
            else
            {
                if(isFirstCon){
                    MessengerApi.LogSuccess("Connected to the Coin Server!");
                    isFirstCon = false;
                }
            
                Logger.LogInfo("Server is active, response: " + webRequest.downloadHandler.text);
            }
        }
    }

    // Handler for the SettingChanged event
    private void SettingChangedHandler(object sender, EventArgs e)
    {
        ConfigEntryBase changedSetting = sender as ConfigEntryBase;

        // Trigger the custom event
        OnSettingChanged?.Invoke(this, new SettingChangedEventArgs(changedSetting));
    }

    // Coroutine to ping the server every 10 minutes (600 seconds)
    private IEnumerator PingServerRoutine()
    {
        while (true)
        {
            yield return PingServer();
            yield return new WaitForSeconds(600); // 600 seconds = 10 minutes
        }
    }

    IEnumerator Load_Single_Player_Points(ulong playerId, System.Action<int> onSuccess, System.Action<string> onFailure)
    {
        Logger.LogMessage($"Loading points for player ID: {playerId}");

        // Define the URL for fetching points of a single player
        string url = $"{baseUrl}/points/player/{playerId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Send the request and wait for the response
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Deserialize the response JSON
                string jsonResponse = request.downloadHandler.text;
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

                if (response.ContainsKey("points"))
                {
                    int points = Convert.ToInt32(response["points"]);
                    Logger.LogInfo($"Points for player {playerId}: {points}");
                    onSuccess(points);
                }
                else if (response.ContainsKey("error"))
                {
                    string error = response["error"].ToString();
                    Logger.LogError($"Error loading points for player {playerId}: {error}");
                    onFailure(error);
                }
            }
            else
            {
                string error = request.error;
                Logger.LogError($"Error loading points for player {playerId}: {error}");
                onFailure(error);
            }
        }
    }
  

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }
}




