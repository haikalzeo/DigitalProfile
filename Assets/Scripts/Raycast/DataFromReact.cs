using UnityEngine;

public class DataFromReact : MonoBehaviour
{
    public static DataFromReact instance;
    public string token = null;
    public string avatarUrl = null;
    public string baseUrl = null;

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
        public string baseUrl;
    }

    public void GetDatas(string json)
    {
        JsonObject obj = JsonUtility.FromJson<JsonObject>(json);
        token = obj.token;
        avatarUrl = obj.avatarUrl;
        baseUrl = obj.baseUrl;
    }
}
