using System;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class PlayerInfo
{
    public int puppetID = -1;
    public string playerName;
    public Vector3 position;
    public Quaternion rotation;
    public int health;
}
[Serializable]
public class ObjectInfo
{
    public int puppetID = -1;
    public Vector3 position;
    public Quaternion rotation;
    public int master = -1;
    public int listPos;
}

public enum PuppetType
{
    None = 0,
    Player,
    Object,
    Entity,
}
public class NetworkClient : MonoBehaviour
{
    //public string playerName;
    //public Vector3 position;
    //public int health;
    //public int puppetID = -1; // Puppet ID assigned by the server
    public PuppetType puppetType = PuppetType.None;
    public PlayerInfo localPlayerInfo; // Reference to PlayerInfo
    public ObjectInfo localObjectInfo;
    [Space]
    public NetworkPuppet networkPuppet;

    private void Start()
    {
        if (puppetType == PuppetType.Object) 
        { 
            networkPuppet = GetComponent<NetworkPuppet>();
            networkPuppet.enabled = false;
            gameObject.name = $"{gameObject.name}({localObjectInfo.puppetID})";
            if (localObjectInfo.puppetID == -1)
            {
                Debug.LogWarning($"{gameObject.name} has an ID of -1 and should be changed immediately");
            }
        }
    }

    public void Connect()
    {
        networkPuppet.enabled = true;
    }

    private void Update()
    {
        if (puppetType == PuppetType.Object)
        {
            if (NetworkManager.isMaster)
            {
                localObjectInfo.position = transform.position;
                localObjectInfo.rotation = transform.rotation;
            }
        }
    }
}
