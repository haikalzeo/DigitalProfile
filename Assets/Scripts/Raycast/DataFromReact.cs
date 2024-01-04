using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DataFromReact : MonoBehaviour
{
    public TMP_Text value;
    public TMP_Text label;
    public static DataFromReact instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public class JsonObject
    {
        public string value;
        public string label;
    }

    // As you can see here is the name of the function that we get the data.
    // it should have the same name in RN function postMessage.
    public void GetDatas(string json)
    {
        JsonObject obj = JsonUtility.FromJson<JsonObject>(json);
        value.SetText(obj.value);
        label.SetText(obj.label);
    }
}
