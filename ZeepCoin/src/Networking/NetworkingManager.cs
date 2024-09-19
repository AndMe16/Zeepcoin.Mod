using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Steamworks;
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

    private bool isGlobal;

    private bool savedData = false;

    public bool SavedData
    {
        set {savedData = value;}
    }

    public async Task<bool> SetIsGlobalAsync(bool value)
    {
        bool canChange = await WaitForConditionAsync();
        
        if (canChange)
        {
            isGlobal = value;
            pointsManager.ClearPlayerPoints();
            Plugin.Logger.LogInfo($"IsGlobal has been updated to: {value}");
        }
        else
        {
            Plugin.Logger.LogInfo("Cannot change IsGlobal yet.");
        }

        return canChange; 
    }

    private Coroutine pingServerCoroutine;

    private PredictionManager predictionManager;
    private PointsManager pointsManager;

    // Server
    private readonly string baseUrl = "https://zeep-coin.onrender.com";

    void Start()
    {
        isGlobal = ModConfig.useGlobalDatabase.Value;
        predictionManager = FindObjectOfType<PredictionManager>();
        pointsManager = FindObjectOfType<PointsManager>();
    }

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

        // Define the URL based on whether it's global or host-specific points
        string url = isGlobal 
            ? $"{baseUrl}/points/global/player/{playerId}" 
            : $"{baseUrl}/points/host/{SteamClient.SteamId}/player/{playerId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
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
        Plugin.Logger.LogInfo("Saving data to server");

        // Define the URL based on whether it's global or host-specific points
        string url = isGlobal 
            ? $"{baseUrl}/points/global" 
            : $"{baseUrl}/points/host/{SteamClient.SteamId}";

        string jsonData = JsonConvert.SerializeObject(totalPointsDataDictionary);
        Plugin.Logger.LogInfo($"Serialized JSON Data: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.timeout = 10; 
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(jsonToSend);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Send the request and wait for the response
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {

                Plugin.Logger.LogInfo("Points data saved successfully.");
                //MessengerApi.LogSuccess("All player's points saved successfully!");
                savedData = true;
            }
            else
            {
                Plugin.Logger.LogError($"Error saving points data: {request.error}");
                MessengerApi.LogError("Failed saving the points to the server!");
            }
        }
    }


    private async Task<bool> WaitForConditionAsync()
    {
       // Espera activa hasta que la condición se cumpla
        while (predictionManager.PredictionActive && savedData)
        {
            await Task.Delay(1000);  // Espera 1 segundo antes de volver a verificar
        }

        return true;  // Cambia esto según la condición real que quieras verificar
    }


}