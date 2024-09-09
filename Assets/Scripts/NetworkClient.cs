using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
    public string address;
    public int port;
    public string message;
    private void Start()
    {
        Connect(address, port, message);
    }

    static void Connect(string address, int port, string message)
    {
        try
        {
            using TcpClient client = new TcpClient(address, port);

            byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

            NetworkStream stream = client.GetStream();

            stream.Write(data, 0, data.Length);

            Debug.Log($"Sent {message}");

            data = new byte[256];

            string responseData = string.Empty;

            int bytes = stream.Read(data, 0, data.Length);
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
            Debug.Log($"Received: {responseData}");
        }
        catch (Exception e) { Debug.LogError(e.ToString()); }
    }
}
