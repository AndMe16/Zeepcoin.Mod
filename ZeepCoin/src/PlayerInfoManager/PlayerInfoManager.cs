using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using ZeepkistClient;

public class PlayerInfoManager : MonoBehaviour
{
    public string GetPlayerUsername(bool isLocal, ulong playerId, out ulong playerId_out)
    {
        if (isLocal)
        {
            playerId_out = SteamClient.SteamId;
            return SteamClient.Name;
        }

        var player = ZeepkistNetwork.PlayerList.FirstOrDefault(p => p.SteamID == playerId);
        playerId_out = playerId;
        return player?.Username ?? "Unknown Player";
    }

    public bool IsPlayerInLobby(string username, out ZeepkistNetworkPlayer player)
    {
        List<ZeepkistNetworkPlayer> playerList = ZeepkistNetwork.PlayerList;

        player = playerList.FirstOrDefault(p => p.Username == username);
        if (player == null)
        {
            return false;
        }
        return true;
    }   
}