using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JsonExample : MonoBehaviour
{
    public string jsonText;
    public string filePath;
    private void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "score.json");

        GameResult result = new();
        result.name = "Luna";
        result.score = 6445;
        result.date = DateTime.Now.ToString();
        result.endReached = false;
        //string j = JsonUtility.ToJson(result);
        //File.WriteAllText(filePath,j);

        string s = File.ReadAllText(filePath);
        GameResult resul = JsonUtility.FromJson<GameResult>(s);
        Debug.Log(resul.name);
        Debug.Log(resul.score);

    }
}
[Serializable]
public class GameResult
{
    public string name;
    public int score;
    public string date;
    public bool endReached;
}