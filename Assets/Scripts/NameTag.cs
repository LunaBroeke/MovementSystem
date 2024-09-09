using UnityEngine;
using TMPro; // If using TextMeshPro

public class NameTag : MonoBehaviour
{
    public Transform puppetTransform; // Player object to follow
    public Camera mainCamera; // Camera that the name tag will face
    public TextMeshProUGUI nameText; // Text to display player's name

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main; // Assign the main camera
        }
    }

    void LateUpdate()
    {
        // Always face the camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward, mainCamera.transform.rotation * Vector3.up);
        }
    }

    public void SetPlayerName(string playerName)
    {
        nameText.text = playerName;
    }
}
