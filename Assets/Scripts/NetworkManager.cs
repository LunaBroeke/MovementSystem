using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
[Serializable]
public class ServerInfo
{
    public string type = "ServerInfo";
    public PlayerInfo master;
}
public class Command
{
    public string type = "Command";
    public string command;
    public string[] arguments;
}
public class TypeCheck { public string type; }

public class NetworkManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public string address;
    public int port;

    private TcpClient client;
    private NetworkStream stream;
    private Thread clientThread;
    private Thread sendThread;
    private Thread objThread;
    private bool isConnected = false;

    [Header("Player")]
    public List<GameObject> playerObjects = new List<GameObject>();
    public PlayerInfo localPlayerInfo { get; set; }
    public NetworkClient localPlayerObject;
    public int localPuppetID = -1;
    public static bool isMaster = false;

    public string localName;
    public TMPro.TMP_InputField nameField;
    public TMPro.TMP_InputField addressField;
    public TMPro.TextMeshProUGUI screenLog;
    [Header("Objects")]
    public List<GameObject> localObjects = new List<GameObject>();
    public ObjectInfoList objInfo = new ObjectInfoList();

    public int expectedBytes = 1024;

    public List<ScreenLogger> loggers = new List<ScreenLogger>();
    private void Start()
    {
        UnityMainThreadDispatcher.Instance();
        //Connect(address, port);

        foreach (NetworkClient nc in FindObjectsOfType<NetworkClient>())
        {
            if (nc.puppetType == PuppetType.Object)
            {
                localObjects.Add(nc.gameObject);
                objInfo.objects.Add(nc.localObjectInfo);
            }
        }
        //checkThread = new Thread(ObjectIDCheck) { IsBackground = true };
    }
    [Obsolete]
    public void ObjectIDCheck()
    {
        foreach (GameObject obj in localObjects)
        {
            AssignObjectID(obj);
        }
        //checkThread.Join();
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

            objThread = new Thread(SendObjectDataLoop)
            {
                IsBackground = true
            };
            objThread.Start();

            Debug.Log("Connected to server");

            // Receive the puppet ID assigned by the server
            byte[] buffer = new byte[512];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                string puppetIDString = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                if (CheckConnectionError(puppetIDString)) { return; }
                localPuppetID = int.Parse(puppetIDString); // sometimes it cannot parse.
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
                    if (localNetworkClient != null && localNetworkClient.puppetType == PuppetType.Player)
                    {
                        //localNetworkClient.localPlayerInfo = localPlayerInfo;
                        localNetworkClient.localPlayerInfo.puppetID = localPlayerInfo.puppetID;
                        localNetworkClient.localPlayerInfo.playerName = localPlayerInfo.playerName;
                    }
                    else if (localNetworkClient.puppetType != PuppetType.Player)
                    {
                        Debug.LogError("LocalPlayer is NOT assigned as player");
                    }
                }
            }

        }
        catch (FormatException e)
        {
            Debug.LogError($"Formatting error: {e}");
            Disconnect(1f);
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection error: {e}");
            Log($"Connection error: {e.Message}");
            Disconnect(5f);
        }
    }

    void ReceivePlayerData()
    {
        //checkThread.Start();

        //ObjectIDCheck();

        bool objCheck = false;
        while (isConnected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[1024 * 3];
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
                if (!objCheck) { objCheck = true; }
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
        string check = JsonUtility.FromJson<TypeCheck>(m).type;
        switch (check)
        {
            case "PlayerInfo":
                ProcessPlayerInfo(m);
                break;
            case "ServerInfo":
                ProcessServerInfo(m);
                break;
            case "ObjectInfo":
                ProcessObjectInfo(m);
                break;
            case "Command":
                CommandHandler(m);
                break;
            default:
                Debug.LogError("received invalid data");
                break;
        }

    }
    void ProcessPlayerInfo(string m)
    {
        PlayerInfoList playerInfoList = JsonUtility.FromJson<PlayerInfoList>(m);
        // Invoke on main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            ProcessPlayerInfoList(playerInfoList);
        });
    }

    void ProcessServerInfo(string m)
    {
        ServerInfo si = JsonUtility.FromJson<ServerInfo>(m);
        if (si.master.puppetID == localPuppetID) { Debug.Log("Current Player is master"); }
        else { Debug.Log("Current Player is NOT master"); }
    }

    void ProcessObjectInfo(string m)
    {
        ObjectInfoList objectInfoList = JsonUtility.FromJson<ObjectInfoList>(m);
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            ProcessObjectInfoList(objectInfoList);
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
            npu.newRot = playerInfo.rotation;
            npu.inactiveTimer = 0f;
            //playerToUpdate.transform.position = playerInfo.position;
        }
    }

    void ProcessObjectInfoList(ObjectInfoList objectInfoList)
    {
        Debug.LogWarning(objectInfoList.objects.Count);
        foreach (ObjectInfo objectInfo in objectInfoList.objects)
        {
            if (!isMaster)
            {
                GameObject objectToUpdate = FindGoByID(objectInfo);
                NetworkPuppet npu = objectToUpdate.GetComponent<NetworkPuppet>();
                npu.newPos = objectInfo.position;
                npu.newRot = objectInfo.rotation;
                npu.inactiveTimer = 0f;
            }
        }
    }
    [Obsolete]
    void AssignObjectID(GameObject obj)
    {
        ObjectInfo objectInfo = obj.GetComponent<NetworkClient>().localObjectInfo;
        Debug.LogWarning("assign woa");
        if (objectInfo.puppetID == -1)
        {
            Debug.LogWarning("assign w");
            int tempID = UnityEngine.Random.Range(1, 100);
            Command c = new Command
            {
                command = "RequestID",
                arguments = new string[] { obj.name, tempID.ToString(), "" }
            };
            string s = JsonUtility.ToJson(c);
            byte[] data = Encoding.UTF8.GetBytes(s + '\n');
            stream.WriteTimeout = 1000;
            stream.Write(data, 0, data.Length);
            bool received = false;
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string a = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string[] b = a.Split('\n', options: StringSplitOptions.RemoveEmptyEntries);
            foreach (string st in b)
            {
                Command d = JsonUtility.FromJson<Command>(st);
                try
                {
                    if (d.type == "Command" && d.command == "SyncID")
                    {
                        if (d.arguments[1] == tempID.ToString())
                        {
                            objectInfo.puppetID = int.Parse(d.arguments[2]);
                            obj.name = $"{obj.name} ({objectInfo.puppetID})";
                            received = true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("a");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    break;
                }
            }
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
    GameObject FindGoByID(ObjectInfo objectInfo)
    {
        NetworkClient[] nps = FindObjectsOfType<NetworkClient>();
        foreach (NetworkClient np in nps)
        {
            ObjectInfo pi = np.localObjectInfo;
            Debug.Log($"seen {np.gameObject.name}");
            if (pi.puppetID == objectInfo.puppetID) { Debug.LogWarning($"found {pi.puppetID}"); return np.gameObject; }
        }
        return null;
    }

    private void Update()
    {
        try
        {
            if (isConnected) { localPlayerInfo.position = localPlayerObject.transform.position; localPlayerInfo.rotation = localPlayerObject.transform.rotation; }
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
                    localPlayerInfo.position = localPlayerInfo.position.Round(2);
                    string json = JsonUtility.ToJson(localPlayerInfo);
                    byte[] data = Encoding.ASCII.GetBytes(json + '\n');
                    //byte[] data = Encoding.ASCII.GetBytes("dam");
                    stream.Write(data, 0, data.Length);
                    //Debug.Log($"Sent player data: {json}");
                }
                Thread.Sleep(100);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending player data: {e.Message}");
            Disconnect();
        }
    }
    void SendObjectDataLoop()
    {
        try
        {
            while (isConnected)
            {
                if (isMaster)
                {
                    string s = "";
                    foreach (ObjectInfo oi in objInfo.objects)
                    {
                        oi.position = oi.position.Round(2);
                        string a = JsonUtility.ToJson(oi) + '\n';
                    }
                    //byte[] data = Encoding.UTF8.GetBytes(s + '\n');
                    //stream.Write(data, 0, data.Length);
                }
                Thread.Sleep(100);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending object data: {e.Message}");
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
    void Disconnect(float time)
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

        StartCoroutine(DelayConnect(time));
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

    private void CommandHandler(string m)
    {
        Command c = JsonUtility.FromJson<Command>(m);
        switch (c.command)
        {
            case "ObjectSyncRequest":
                Debug.LogError("ObjectSyncRequest is obsolete");
                break;
            case "BroadcastMaster":
                AssignMaster(m);
                break;
        }
    }

    private void AssignMaster(string m)
    {
        Command c = JsonUtility.FromJson<Command>(m);
        if (int.Parse(c.arguments[0]) == localPuppetID) { isMaster = true; Debug.Log("Local Player is master"); }
        else { Debug.Log("Local Player is NOT master"); }
        UnityMainThreadDispatcher.Instance().Enqueue(() => ConnectObjects());
    }

    private void ConnectObjects()
    {
        foreach (GameObject obj in localObjects)
        {
            obj.GetComponent<NetworkClient>().Connect();
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
    public const string type = "PlayerInfoList";
    public List<PlayerInfo> players = new();
}
[Serializable]
public class ObjectInfoList
{
    public const string type = "ObjectInfoList";
    public List<ObjectInfo> objects = new();
}

public class ScreenLogger
{
    public string message;
    public float timer = 5f;
}

static class ExtensionMethods
{
    /// <summary>
    /// Rounds Vector3.
    /// </summary>
    /// <param name="vector3"></param>
    /// <param name="decimalPlaces"></param>
    /// <returns></returns>
    public static Vector3 Round(this Vector3 vector3, int decimalPlaces = 2)
    {
        float multiplier = 1;
        for (int i = 0; i < decimalPlaces; i++)
        {
            multiplier *= 10f;
        }
        return new Vector3(
            Mathf.Round(vector3.x * multiplier) / multiplier,
            Mathf.Round(vector3.y * multiplier) / multiplier,
            Mathf.Round(vector3.z * multiplier) / multiplier);
    }

    public static Quaternion Round(this Quaternion quaternion, int decimalPlaces = 2)
    {
        float multiplier = 1;
        for (int i = 0;i < decimalPlaces;i++)
        {
            multiplier *= 10f;
        }
        return new Quaternion(
            Mathf.Round(quaternion.x*multiplier)/multiplier,
            Mathf.Round(quaternion.y*multiplier)/multiplier,
            Mathf.Round(quaternion.z*multiplier)/multiplier,
            Mathf.Round(quaternion.w*multiplier)/multiplier);
    }
}