using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NetworkPuppet : MonoBehaviour
{
    public PlayerInfo playerInfo;
    public ObjectInfo objectInfo;
    public PuppetType type;
    public NetworkManager networkManager;
    private NetworkClient nc;
    public bool col = true;
    public bool debug = false;

    public bool moving = false;

    public Vector3 newPos;
    public Quaternion newRot;
    public float moveDur;

    public NameTag nameTag;

    public float inactiveTimer = 0f;
    public float inactiveMax = 10f;
    private void Start()
    {
        networkManager = FindObjectOfType<NetworkManager>();
        if (!GetComponent<NetworkClient>())
        {
            foreach (NetworkClient n in FindObjectsOfType<NetworkClient>()) { if (n.puppetType == PuppetType.Player) { nc = n; break; }}
            if (nc.localPlayerInfo.puppetID == playerInfo.puppetID && debug == false) { LocalPuppet(); }
            if (col == false) { GetComponent<CapsuleCollider>().enabled = false; }
            nameTag.SetPlayerName(playerInfo.playerName);
            StartMove();
        }
        else
        {
            nc = GetComponent<NetworkClient>();
            if (NetworkManager.isMaster == false)
            {
                GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                StartMove();
            }
            else
            {
                GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
            }
        }
        type = nc.puppetType;
    }

    void StartMove()
    {
        newPos = transform.position;
        newRot = transform.rotation;
        StartCoroutine(MovePuppet());
    }

    private void LocalPuppet()
    {
        GetComponent<MeshRenderer>().enabled = false;
        GetComponent<CapsuleCollider>().enabled = false;
        GetComponent<NameTag>().gameObject.SetActive(false);
    }

    private IEnumerator MovePuppet()
    {
        Vector3 startPos = transform.position; // Starting position
        Quaternion startRot = transform.rotation;
        float elapsedTime = 0f;

        while (true)
        {
            // Start lerping only if the position has changed
            if (startPos != newPos)
            {
                elapsedTime = 0f; // Reset elapsed time
                Vector3 targetPos = newPos; // Set target position
                Quaternion targetRot = newRot;

                // Lerp until the duration is completed
                while (elapsedTime < moveDur)
                {
                    elapsedTime += Time.deltaTime; // Increment time
                    float t = Mathf.Clamp01(elapsedTime / moveDur); // Normalized time (0 to 1)
                    transform.position = Vector3.Lerp(startPos, targetPos, t); // Lerp between start and target position
                    transform.rotation = Quaternion.Lerp(startRot,targetRot, t);
                    yield return null; // Wait for the next frame
                }

                // Ensure the position is exactly at the target at the end
                transform.position = targetPos;
                transform.rotation = targetRot;
                startPos = targetPos; // Update startPos to match the new position
                startRot = targetRot;
            }

            yield return null; // Continue checking in the next frame
        }
    }

    private void Update()
    {
        if (type == PuppetType.Player)
        {
            inactiveTimer += Time.deltaTime;

            if (inactiveTimer > inactiveMax) { Destroy(gameObject); }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
    }
#endif
}
