using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DataFromReact : MonoBehaviour
{
    public TMP_Text text_token;
    public TMP_Text text_avatarUrl;
    public static DataFromReact instance;
    public string token;
    public string avatarUrl;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public class JsonObject
    {
        public string token;
        public string avatarUrl;
    }

    public void GetDatas(string json)
    {
        JsonObject obj = JsonUtility.FromJson<JsonObject>(json);
        token = obj.token;
        avatarUrl = obj.avatarUrl; 
        text_token.SetText(token);
        text_avatarUrl.SetText(avatarUrl);
    }
}
