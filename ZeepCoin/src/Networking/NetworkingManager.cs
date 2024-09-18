using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using ZeepCoin;
using ZeepSDK.Messaging;

public class NetworkingManager : MonoBehaviour
{

    private bool isFirstCon = true;

    public bool IsFirstCon
    {
        set {isFirstCon = value;}
    }

    private bool isFirstDiscon = true;
    public bool IsFirstDiscon
    {
        set {isFirstDiscon = value;}
    }


    private bool isConnectedToServer = false;

    public bool IsConnectedToServer
    {
        get {return isConnectedToServer;}
    }

    private Coroutine pingServerCoroutine;

    // Server
    private readonly string baseUrl = "https://zeep-coin.onrender.com";

    private IEnumerator PingServerRoutine()
    {
        while (true)
        {
            yield return PingServer();
            yield return new WaitForSecondsRealtime(30); // 600 seconds = 10 minutes
        }
    }
    
    private IEnumerator PingServer()
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
                if (isFirstDiscon){
                    MessengerApi.LogError("Failed connecting with the Coin Server");
                    isFirstDiscon = false;
                    isFirstCon = true;
                }
                Plugin.Logger.LogError("Error Pinging Server: " + webRequest.error);
                isConnectedToServer = false;
            }
            else
            {
                if(isFirstCon){
                    MessengerApi.LogSuccess("Connected to the Coin Server!");
                    isFirstCon = false;
                    isFirstDiscon = true;
                }
                Plugin.Logger.LogInfo("Server is active, response: " + webRequest.downloadHandler.text);
                isConnectedToServer = true;
            }
        }
        
    }
    public void StartPingServer()
    {
        pingServerCoroutine = StartCoroutine(PingServerRoutine());
    }
    
    public void StopPingServer()
    {
        StopCoroutine(pingServerCoroutine);
        isFirstCon = true;
        isFirstDiscon = true;
    }

    public IEnumerator Load_Single_Player_Points(ulong playerId, System.Action<int> onSuccess, System.Action<string> onFailure)
    {
        Plugin.Logger.LogMessage($"Loading points for player ID: {playerId}");

        // Define the URL for fetching points of a single player
        string url = $"{baseUrl}/points/player/{playerId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 2;
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
                    Plugin.Logger.LogInfo($"Points for player {playerId}: {points}");
                    onSuccess(points);
                }
                else if (response.ContainsKey("error"))
                {
                    string error = response["error"].ToString();
                    Plugin.Logger.LogWarning($"Error loading points for player {playerId}: {error}");
                    onFailure(error);
                }
            }
            else
            {
                string error = request.error;
                Plugin.Logger.LogError($"Error loading points for player {playerId}: {error}");
                onFailure(error);
            }
        }
    }

    public IEnumerator Save_Data_Server(Dictionary<ulong, uint> totalPointsDataDictionary)
    {
        Plugin.Logger.LogInfo($"Saving data to server");
        string url = $"{baseUrl}/points/all";

        string jsonData = JsonConvert.SerializeObject( totalPointsDataDictionary );
        Plugin.Logger.LogInfo($"Serialized JSON Data: {jsonData}");
        using (UnityWebRequest request = new(url, "POST"))
        {
            request.timeout = 5; 
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(jsonToSend);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Plugin.Logger.LogInfo("All points data saved successfully.");
                //MessengerApi.LogSuccess("All player's points saved successfully!");
            }
            else
            {
                Plugin.Logger.LogError($"Error saving points data: {request.error}");
                MessengerApi.LogError("Failed saving the points to the server!");
            }
        }
        
    }

    


}