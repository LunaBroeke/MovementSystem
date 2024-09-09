using System;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
    public string playerName;
    public Vector3 position;
    public int health;
    public int puppetID = -1; // Puppet ID assigned by the server

    public PlayerInfo localPlayerInfo; // Reference to PlayerInfo

    private void Start()
    {
        // Ensure PlayerInfo is set up from NetworkManager
        if (localPlayerInfo == null)
        {
            Debug.LogError("LocalPlayerInfo is not set.");
            return;
        }

        // Initialize the player's properties based on localPlayerInfo
        playerName = localPlayerInfo.playerName;
        health = localPlayerInfo.health;
        puppetID = localPlayerInfo.puppetID;
    }

    private void Update()
    {
        if (puppetID != -1) // Only update if we have a valid puppetID
        {
            // Update player info from the GameObject's transform
            localPlayerInfo.position = transform.position;
        }
    }

    // This method should be called by NetworkManager to update the localPlayerInfo
    public void SetPlayerInfo(PlayerInfo playerInfo)
    {
        localPlayerInfo = playerInfo;
        playerName = playerInfo.playerName;
        health = playerInfo.health;
        puppetID = playerInfo.puppetID;
    }
}
