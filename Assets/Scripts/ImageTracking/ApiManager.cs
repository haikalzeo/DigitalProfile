using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

public class ApiManager
{
    public IEnumerator GetRequest(string binusianId, System.Action<string> onSuccess, System.Action onFailed)
    {
        using (UnityWebRequest request = UnityWebRequest.Get("https://651088073ce5d181df5d557c.mockapi.io/rpm/users/" + binusianId))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                onFailed?.Invoke();
            }
            else
            {
                if (request.responseCode != 200 || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    onFailed?.Invoke();
                }
                else
                {
                    var response = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    onSuccess?.Invoke(response.avatar);
                }
            }
        }
    }
}

[System.Serializable]
public class ApiResponse
{
    public string binusianId;
    public List<string> purchasedAsset;
    public string avatar;
    public string userId;
}
