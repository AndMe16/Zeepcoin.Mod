using System;
using System.Collections.Generic;
using UnityEngine;
using ZeepCoin;
using ZeepSDK.Chat;

public class Coin_ServerMessageManager : MonoBehaviour
{
    private string serverMessage;
    private string predictionBar;
    private string heads_lines;
    private string tails_lines;
    private int heads_voters;
    private int tails_voters;
    private string heads_ratio_str;
    private string tails_ratio_str;
    private List<string> rowTexts = new List<string> {"<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>"};
    private List<ulong> rowPlayerIds = new List<ulong> {0,0,0,0,0};
    private int rechargePoints;
    private int rechargeInterval; 

    private Coin_PredictionManager predictionManager;


    void Start()
    {
        predictionManager = FindObjectOfType<Coin_PredictionManager>();
        rechargePoints = Coin_ModConfig.rechargePoints.Value;
        rechargeInterval = Coin_ModConfig.rechargeInterval.Value;

    }

    public void ShowResultServerMessage(string result)
    {
        ChatApi.SendMessage($"/servermessage white 20 The coin has been flipped! It's {(result == "heads" ? "<#21B2ED>" : "<#FD4F06>")}{result}<#FFFFFF>!");
    }

    public void ShowStopServerMessage()
    {
        ChatApi.SendMessage($"/servermessage white 20 Bad Host!");
    }

    public void ShowLackPointsServerMessage()
    {
        ChatApi.SendMessage("/servermessage white 20 There are not enough coins! Refunding points");
    }

    public void UpdatePredictionServerMessage(uint countdownLeft)
    {
        TimeSpan timeLeft = TimeSpan.FromSeconds(countdownLeft);
        serverMessage = $"/servermessage white 2 <size=+7><voffset=-9em><align=\"left\"><line-height=0%><pos=3em>Prediction time left: <#F0F071>{timeLeft.ToString(@"hh\:mm\:ss")}"
                                +predictionBar;
        ChatApi.SendMessage(serverMessage);
    }

    public void UpdatePredictionBar(){
        Plugin.Logger.LogInfo("Updating servermessage PredictionBar");
        predictionManager.GetPredictionStats(out heads_voters, out tails_voters, out ulong totalHeadsPoints, out ulong totalTailsPoints, out double heads_ratio, out double tails_ratio);
        DefineLinesBar(totalHeadsPoints,totalTailsPoints);
        DefineStringRatios(heads_ratio, tails_ratio);
        predictionBar= $"<br><line-height=90%><pos=5em><#FFFFFF><u><alpha=#00>|<pos=22.5em>|</u>" //Stats
                        +$"<br><line-height=90%><#FFFFFF><u><alpha=#00>|<pos=5em><#FFFFFF>|<#21B2ED>Heads <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>Tails<pos=22.5em><#FFFFFF>|</u>" //Stats                        
                        +$"<br><line-height=90%><#FFFFFF><u>|Voters<pos=5em>|<#21B2ED>{heads_voters} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{tails_voters}<pos=22.5em><#FFFFFF>|</u>"                         
                        +$"<br><line-height=90%><#FFFFFF><u>|Points<pos=5em>|<#21B2ED>{totalHeadsPoints} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{totalTailsPoints}<pos=22.5em><#FFFFFF>|</u>"                          
                        +$"<br><line-height=90%><#FFFFFF><u>|Ratio<pos=5em>|<#21B2ED>{heads_ratio_str} <pos=10em>{heads_lines}<#F0F071>|<#FD4F06>{tails_lines} <pos=18em>{tails_ratio_str}<pos=22.5em><#FFFFFF>|</u>"                  
                        +$"<br><line-height=90%>Vote using <#21B2ED>!heads <#F0F071><points> <#FFFFFF>or <#FD4F06>!tails <#F0F071><points>"
                        +$"<br><#FFFFFF>Use <#F0F071>!coinpoints <#FFFFFF>to check your remaining points"
                        +$"<br>Recharging <#F0F071>{rechargePoints} points <#FFFFFF>every <#F0F071>{rechargeInterval} seconds"
                        +((heads_voters!=0||tails_voters!=0)?$"<br><line-height=90%><#FFFFFF><u><alpha=#00>|<pos=8em><pos=25em>|</u>":"<br><line-height=90%>") 
                        +((heads_voters!=0||tails_voters!=0)? $"<br><line-height=90%><#FFFFFF><u>|<#F0F071>User <pos=8em><#FFFFFF>|<#F0F071>Side <pos=12em><#FFFFFF>|<#F0F071>Sent<pos=18em><#FFFFFF>|<#F0F071>Remaining<pos=25em><#FFFFFF>|</u>" : "<br><line-height=90%>") //Voters
                        +rowTexts[0] //Voters
                        +rowTexts[1] //Voters
                        +rowTexts[2] //Voters
                        +rowTexts[3]; //Voters              
    }
    private void DefineLinesBar(ulong totalHeadsPoints, ulong totalTailsPoints){
        ulong totalPoints = totalHeadsPoints+totalTailsPoints;
        
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
    }

    private void DefineStringRatios(double heads_ratio, double tails_ratio)
    {
        // Winner ratio -> (WinnerBets+LoserBets)/(WinnerBets)
        
        if (!(heads_ratio==0)){
            heads_ratio_str = "1:"+heads_ratio.ToString();
        } else{
            heads_ratio_str = "-:-";
        }
        
        if (!(tails_ratio==0)){
            tails_ratio_str = "1:"+tails_ratio.ToString();
        } else{
            tails_ratio_str = "-:-";
        }
    }

    public void UpdateVotersTable(Dictionary<ulong, (string username, string commandType, uint totalPointsSent)> playerPredictions, uint totalPointsRemaining, List<ulong> voteOrder){
        Plugin.Logger.LogInfo("Updating voters table servermessage");   
        int count = Math.Min(voteOrder.Count, 4);

        ulong playerId = voteOrder[0];

        var (username, commandType, totalPointsSent) = playerPredictions[playerId];
        string truncatedUsername = TruncateUsername(username, 10);

        if(rowPlayerIds[0] ==playerId){
            rowTexts[0] = $"<br><line-height=90%><#FFFFFF><u>|{truncatedUsername}<pos=8em>|{(commandType== "heads" ? "<#21B2ED>":"<#FD4F06>")}{commandType}<#FFFFFF><pos=12em>|{totalPointsSent}<pos=18em>|{totalPointsRemaining}<pos=25em>|</u>";
            rowPlayerIds[0] = playerId; // Update the player ID for the top row
            UpdatePredictionBar();
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
        
        rowTexts[0] = $"<br><line-height=90%><#FFFFFF><u>|{truncatedUsername}<pos=8em>|{(commandType== "heads" ? "<#21B2ED>":"<#FD4F06>")}{commandType}<#FFFFFF><pos=12em>|{totalPointsSent}<pos=18em>|{totalPointsRemaining}<pos=25em>|</u>";
        rowPlayerIds[0] = playerId; // Update the player ID for the top row

        UpdatePredictionBar();
    }

    private string TruncateUsername(string username, int maxLength)
    {
        if (username.Length > maxLength)
        {
            return username[..maxLength] + "...";
        }
        return username;
    }
    
    public void UpdateRechargeInfo()
    {
        rechargePoints = Coin_ModConfig.rechargePoints.Value;
        rechargeInterval = Coin_ModConfig.rechargeInterval.Value;
    }

    public void ClearServerMessageVars(){
        rowTexts = new List<string> {"<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>","<br><line-height=90%>"}; 
        rowPlayerIds = new List<ulong> {0,0,0,0,0};
        heads_voters = 0;
        tails_voters = 0;
    }
}