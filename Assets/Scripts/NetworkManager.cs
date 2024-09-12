using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public class PlayerInfo
{
    public string playerName;
    public Vector3 position;
    public int health;
    public int puppetID;
}

public class NetworkManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public string address;
    public int port;

    private TcpClient client;
    private NetworkStream stream;
    private Thread clientThread;
    private Thread sendThread;
    private bool isConnected = false;

    public List<GameObject> playerObjects = new List<GameObject>();
    public PlayerInfo localPlayerInfo { get; set; }
    public NetworkClient localPlayerObject;
    public int localPuppetID = -1;
    public int sleep = 100;

    public string localName;
    public TMPro.TMP_InputField nameField;
    public TMPro.TMP_InputField addressField;
    public TMPro.TextMeshProUGUI screenLog;

    public int expectedBytes = 1024;

    public List<ScreenLogger> loggers = new List<ScreenLogger>();
    private void Start()
    {
        UnityMainThreadDispatcher.Instance();
        //Connect(address, port);
    }

    public void ButtonListener()
    {
        if (!isConnected)
        {
            localName = nameField.text;
            address = addressField.text;
            Connect(address, port);
        }
        else
        {
            Disconnect();
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    void Connect(string address, int port)
    {
        try
        {
            client = new TcpClient(address, port);
            stream = client.GetStream();
            isConnected = true;

            clientThread = new Thread(ReceivePlayerData)
            {
                IsBackground = true
            };
            clientThread.Start();

            sendThread = new Thread(SendPlayerDataLoop)
            {
                IsBackground = true
            };
            sendThread.Start();

            Debug.Log("Connected to server");

            // Receive the puppet ID assigned by the server
            byte[] buffer = new byte[512];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                string puppetIDString = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                if (CheckConnectionError(puppetIDString)) { return; }
                localPuppetID = int.Parse(puppetIDString);
                localPlayerInfo = new PlayerInfo
                {
                    puppetID = localPuppetID,
                    playerName = localName,
                    health = 100
                };
                Debug.Log($"Assigned Puppet ID: {localPuppetID}");
                if (localPlayerObject == null)
                {
                    Debug.LogError("No local player assigned");
                }
                else
                {
                    // Update existing localPlayerObject with new localPlayerInfo
                    NetworkClient localNetworkClient = localPlayerObject.GetComponent<NetworkClient>();
                    if (localNetworkClient != null)
                    {
                        localNetworkClient.localPlayerInfo = localPlayerInfo;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection error: {e.Message}");
            Log($"Connection error: {e.Message}");
            Disconnect(true);
        }
    }

    void ReceivePlayerData()
    {
        while (isConnected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string jsonData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        if (CheckConnectionError(jsonData)) { return; }
                        string[] strings = jsonData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string str in strings)
                        {
                            ProcessMessage(str);
                            stream.Flush();
                        }
                    }
                }

                // Sleep to reduce CPU usage
                //Thread.Sleep(sleep); // Reduced sleep time for better responsiveness
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving data: {e.ToString()}");
                continue;
            }
        }
    }

    void ProcessMessage(string m)
    {
        PlayerInfoList playerInfoList = JsonUtility.FromJson<PlayerInfoList>(m);
        if (playerInfoList == null)
        {
            Debug.LogWarning("Received invalid player data.");
            return;
        }
        List<PlayerInfo> players = playerInfoList.players;

        // Invoke on main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            ProcessPlayerInfoList(playerInfoList);
        });
    }

    void ProcessPlayerInfoList(PlayerInfoList playerInfoList)
    {
        foreach (PlayerInfo playerInfo in playerInfoList.players)
        {
            if (!playerObjects.Contains(FindGoByID(playerInfo)))
            {
                GameObject playerObject = Instantiate(playerPrefab);
                playerObject.name = $"Player_{playerInfo.puppetID}";
                PlayerInfo pi = new()
                {
                    playerName = playerInfo.playerName,
                    puppetID = playerInfo.puppetID,
                };
                NetworkPuppet np = playerObject.GetComponent<NetworkPuppet>();
                np.playerInfo = pi;
                playerObjects.Add(playerObject);
            }

            GameObject playerToUpdate = FindGoByID(playerInfo);
            NetworkPuppet npu = playerToUpdate.GetComponent<NetworkPuppet>();
            npu.newPos = playerInfo.position;
            npu.inactiveTimer = 0f;
            //playerToUpdate.transform.position = playerInfo.position;
        }
    }

    static PlayerInfo FindPlayerByID(List<PlayerInfo> players, int playerID)
    {
        foreach (PlayerInfo player in players) { if (player.puppetID == playerID) { return player; } }
        return null;
    }

    GameObject FindGoByID(PlayerInfo playerInfo)
    {
        NetworkPuppet[] nps = FindObjectsOfType<NetworkPuppet>();
        foreach (NetworkPuppet np in nps)
        {
            PlayerInfo pi = np.playerInfo;
            if (pi.puppetID == playerInfo.puppetID) { return np.gameObject; }
        }
        return null;
    }

    private void Update()
    {
        try
        {
            if (isConnected) localPlayerInfo.position = localPlayerObject.transform.position;
        }
        catch { Disconnect(); Connect(address, port); }

        screenLog.text = TrackLog();
    }

    void SendPlayerDataLoop()
    {
        try
        {
            while (isConnected)
            {
                if (localPuppetID != -1)
                {
                    string json = JsonUtility.ToJson(localPlayerInfo);
                    byte[] data = Encoding.ASCII.GetBytes(json + '\n');
                    //byte[] data = Encoding.ASCII.GetBytes("dam");
                    stream.Write(data, 0, data.Length);
                    //Debug.Log($"Sent player data: {json}");
                }

                Thread.Sleep(sleep);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending data: {e.Message}");
        }
    }

    void Disconnect()
    {
        byte[] send = Encoding.ASCII.GetBytes("Disconnect\n");
        stream.Write(send, 0, send.Length);
        Thread.Sleep(30);
        isConnected = false;
        if (clientThread != null && clientThread.IsAlive)
        {
            clientThread.Join();
        }

        if (sendThread != null && sendThread.IsAlive)
        {
            sendThread.Join();
        }

        if (stream != null) stream.Close();
        if (client != null) client.Close();

        Debug.Log("Disconnected from server");
    }
    void Disconnect(bool reconnect)
    {
        isConnected = false;
        if (clientThread != null && clientThread.IsAlive)
        {
            clientThread.Join();
        }

        if (sendThread != null && sendThread.IsAlive)
        {
            sendThread.Join();
        }

        if (stream != null) stream.Close();
        if (client != null) client.Close();

        Debug.Log("Disconnected from server");

        if (reconnect) { StartCoroutine(DelayConnect(5)); }
    }
    IEnumerator DelayConnect(float t)
    {
        float f = 0;
        Log($"Reconnecting in {t} seconds");
        while (!isConnected)
        {
            f += Time.deltaTime;
            if (f > t) { Connect(address, port); yield break; }
            yield return null;
        }
    }

    bool CheckConnectionError(string message)
    {
        switch (message)
        {
            case "Read failure":
                Disconnect();
                Connect(address, port);
                return true;
            case "Server full":
                Disconnect();
                Thread.Sleep(100);
                return true;
            default:
                return false;
        }
    }

    private void Log(string message)
    {
        ScreenLogger sl = new() { message = message };
        loggers.Add(sl);
    }

    public string TrackLog()
    {
        string s = "";
        foreach (ScreenLogger sl in loggers)
        {
            s += $"{sl.message} \n";
            sl.timer -= Time.deltaTime;
            if (sl.timer < 0)
            {
                loggers.Remove(sl); continue;
            }
        }
        return s;
    }
}

// Helper class for deserializing a list of PlayerInfo objects
[Serializable]
public class PlayerInfoList
{
    public List<PlayerInfo> players = new();
}

public class ScreenLogger
{
    public string message;
    public float timer = 5f;
}
