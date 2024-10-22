using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;
using ZeepCoin.src;
using ZeepCoin.src.ModConfig;
using ZeepSDK.Messaging;

public class Coin_NetworkingManager : MonoBehaviour
{

    private Coin_PredictionManager predictionManager;
    private Coin_PointsManager pointsManager;
    private Coin_NotificationManager notificationManager;

    private bool isFirstCon = true;

    public bool IsFirstCon
    {
        set { isFirstCon = value; }
    }

    private bool isFirstDiscon = true;
    public bool IsFirstDiscon
    {
        set { isFirstDiscon = value; }
    }


    private bool isConnectedToServer = false;

    public bool IsConnectedToServer
    {
        get { return isConnectedToServer; }
    }

    private bool isGlobal;

    public bool IsGlobal
    {
        get { return isGlobal; }
    }

    private bool savedData = true;


    private string jwtToken;

    public async Task<bool> SetIsGlobalAsync(bool value)
    {
        bool canChange = await WaitForConditionAsync();

        if (canChange)
        {
            isGlobal = value;
            pointsManager.ClearPlayerPoints();
            Plugin.Logger.LogInfo($"IsGlobal has been updated to: {value}");
            if (isGlobal)
            {
                Coin_ModConfig.LoadConfigValues();
            }
            notificationManager.NotifyTypeOfDB(isGlobal);
        }
        else
        {
            Plugin.Logger.LogInfo("Cannot change IsGlobal yet.");
        }

        return canChange;
    }

    private Coroutine pingServerCoroutine;



    // Server
    private readonly string baseUrl = "https://zeep-coin.onrender.com";


    [System.Serializable]
    public class PlayerPointsData(ulong playerId, uint points)
    {
        public ulong playerId = playerId;
        public uint points = points;
    }

#pragma warning disable IDE0051 // Remove unused private members
    void Start()
#pragma warning restore IDE0051 // Remove unused private members
    {
        isGlobal = Coin_ModConfig.useGlobalDatabase.Value;
        predictionManager = FindObjectOfType<Coin_PredictionManager>();
        pointsManager = FindObjectOfType<Coin_PointsManager>();
        notificationManager = FindObjectOfType<Coin_NotificationManager>();
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
        if (isFirstCon)
        {
            //MessengerApi.Log("Connecting to the Coin Server");
            Plugin.Logger.LogInfo("Connecting to the Coin Server");
        }
        using UnityWebRequest webRequest = UnityWebRequest.Get(url);
        webRequest.timeout = 80;
        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            if (isFirstDiscon)
            {
                MessengerApi.LogError("Failed connecting with the Coin Server");
                isFirstDiscon = false;
                isFirstCon = true;
            }
            Plugin.Logger.LogError("Error Pinging Server: " + webRequest.error);
            isConnectedToServer = false;
        }
        else
        {
            if (isFirstCon)
            {
                // MessengerApi.LogSuccess("Connected to the Coin Server!");
                if (isGlobal)
                {
                    Coin_ModConfig.LoadConfigValues();
                }
                isFirstCon = false;
                isFirstDiscon = true;
                StartCoroutine(GetToken(SteamClient.SteamId.ToString(),
                        onTokenReceived: (token) =>
                        {
                            Plugin.Logger.LogInfo($"Received token");
                            jwtToken = token;
                        },
                        onError: (error) =>
                        {
                            Plugin.Logger.LogError($"Failed to get token: {error}");
                        }));
            }
            Plugin.Logger.LogInfo("Server is active, response: " + webRequest.downloadHandler.text);
            isConnectedToServer = true;
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

    public IEnumerator LoadSinglePlayerPoints(ulong playerId, System.Action<int> onSuccess, System.Action<string> onFailure)
    {
        // Plugin.Logger.LogInfo($"Loading points for player ID: {playerId}");

        // Define the URL based on whether it's global or host-specific points
        string url = isGlobal
            ? $"{baseUrl}/points/global/player/{playerId}"
            : $"{baseUrl}/points/host/{SteamClient.SteamId}/player/{playerId}";

        using UnityWebRequest request = UnityWebRequest.Get(url);
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

    public IEnumerator SaveSinglePlayerPoints(ulong playerId, uint points)
    {
        //Plugin.Logger.LogInfo($"Saving data for player {playerId} to server");
        savedData = false;
        // Define the URL based on whether it's global or host-specific points
        string url = isGlobal
            ? $"{baseUrl}/points/global/player/{playerId}"
            : $"{baseUrl}/points/host/{SteamClient.SteamId}/player/{playerId}";

        PlayerPointsData data = new(playerId, points);
        string jsonData = JsonConvert.SerializeObject(data);
        //Plugin.Logger.LogInfo($"Serialized JSON Data: {jsonData}");

        using UnityWebRequest request = new(url, "POST");
        request.timeout = 10;
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(jsonToSend);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Attach JWT token in the Authorization header
        if (!string.IsNullOrEmpty(jwtToken))
        {
            request.SetRequestHeader("Authorization", jwtToken);
        }
        else
        {
            Debug.LogError("No JWT token available!");
            savedData = true;
            yield break;
        }

        // Send the request and wait for the response
        yield return request.SendWebRequest();
        savedData = true;
        if (request.result == UnityWebRequest.Result.Success)
        {

            Plugin.Logger.LogInfo($"Data points from {playerId} saved successfully.");
            //MessengerApi.LogSuccess("All player's points saved successfully!");
        }
        else
        {
            Plugin.Logger.LogError($"Error saving data points from {playerId}: {request.error}");
            MessengerApi.LogError("Failed saving the points to the server!");
        }

    }

    public IEnumerator SavePointsFromDict(Dictionary<ulong, uint> totalPointsDataDictionary)
    {
        //Plugin.Logger.LogInfo("Saving data from dictionary to server");
        savedData = false;
        // Define the URL based on whether it's global or host-specific points
        string url = isGlobal
            ? $"{baseUrl}/points/global"
            : $"{baseUrl}/points/host/{SteamClient.SteamId}";

        string jsonData = JsonConvert.SerializeObject(totalPointsDataDictionary);
        //Plugin.Logger.LogInfo($"Serialized JSON Data: {jsonData}");

        using UnityWebRequest request = new(url, "POST");
        request.timeout = 10;
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(jsonToSend);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Attach JWT token in the Authorization header
        if (!string.IsNullOrEmpty(jwtToken))
        {
            request.SetRequestHeader("Authorization", jwtToken);
        }
        else
        {
            Debug.LogError("No JWT token available!");
            savedData = true;
            yield break;
        }

        // Send the request and wait for the response
        yield return request.SendWebRequest();
        savedData = true;
        if (request.result == UnityWebRequest.Result.Success)
        {

            Plugin.Logger.LogInfo("Data points from dictionary saved successfully.");
            //MessengerApi.LogSuccess("All player's points saved successfully!");
        }
        else
        {
            Plugin.Logger.LogError($"Error saving data points from dictionary: {request.error}");
            MessengerApi.LogError("Failed saving the points to the server!");
        }
    }



    private async Task<bool> WaitForConditionAsync()
    {
        while (predictionManager.PredictionActive || !savedData)
        {
            //Plugin.Logger.LogInfo($"Waiting for the conditions to change PredictionActive: {predictionManager.PredictionActive} savedData:{savedData}");
            await Task.Delay(1000);
        }
        Plugin.Logger.LogInfo($"Not longer waiting for the conditions: {predictionManager.PredictionActive} savedData:{savedData}");
        return true;
    }

    public IEnumerator FetchAllConfigValues(System.Action<Dictionary<string, string>> onSuccess, System.Action<string> onFailure)
    {
        string url = $"{baseUrl}/config_values";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 10;
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string jsonResponse = request.downloadHandler.text;
                var configValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResponse);
                onSuccess(configValues);
            }
            catch (Exception ex)
            {
                onFailure($"Failed to deserialize JSON: {ex.Message}");
            }
        }
        else
        {
            onFailure($"Request error: {request.error}");
        }
    }

    public async Task<(int rechargePoints, int rechargeInterval, int defaultPoints, string error)> LoadServerConfigValuesAsync()
    {
        var tcs = new TaskCompletionSource<(int, int, int, string)>();

        StartCoroutine(FetchAllConfigValues(
            configValues =>
            {
                // Handle the retrieved configuration values
                int rechargePoints = 0;
                int rechargeInterval = 0;
                int defaultPoints = 0;
                string error = null;

                foreach (var kvp in configValues)
                {
                    Plugin.Logger.LogInfo($"Config ID: {kvp.Key}, Value: {kvp.Value}");

                    if (kvp.Key == "rechargePoints" && int.TryParse(kvp.Value, out var rp))
                    {
                        rechargePoints = rp;
                    }
                    else if (kvp.Key == "rechargeInterval" && int.TryParse(kvp.Value, out var ri))
                    {
                        rechargeInterval = ri;
                    }
                    else if (kvp.Key == "defaultPoints" && int.TryParse(kvp.Value, out var dp))
                    {
                        defaultPoints = dp;
                    }
                }

                tcs.SetResult((rechargePoints, rechargeInterval, defaultPoints, error));
            },
            errorMsg =>
            {
                tcs.SetResult((0, 0, 0, errorMsg));
            }
        ));

        return await tcs.Task;
    }

    public IEnumerator GetToken(string playerId, Action<string> onTokenReceived, Action<string> onError)
    {
        string url = $"{baseUrl}/login";
        string jsonData = JsonConvert.SerializeObject(new { player_id = playerId });

        using UnityWebRequest request = new(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);
            if (response.ContainsKey("token"))
            {
                onTokenReceived(response["token"]);
            }
            else
            {
                onError("Token not received");
            }
        }
        else
        {
            onError($"Error fetching token: {request.error}");
        }
    }


}