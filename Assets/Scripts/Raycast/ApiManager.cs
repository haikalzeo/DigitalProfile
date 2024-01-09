using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

public class ApiManager
{
    public IEnumerator PostRequest(string encryptedData, string token, System.Action<string> onSuccess, System.Action onFailed)
    {
        string url = "https://func-digitalprofile-test.azurewebsites.net/api/Friend/GetFriendDetailFromQR";

        // Create a new UnityWebRequest, and set the url
        UnityWebRequest request = new UnityWebRequest(url, "POST");

        // Create a new DownloadHandlerBuffer (which will receive the raw data), and set it on the request
        request.downloadHandler = new DownloadHandlerBuffer();

        // Create a new UploadHandlerRaw (which will send the raw data), and set it on the request
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{\"encryptedData\": \"" + encryptedData + "\"}");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);

        // Set the content type header
        request.SetRequestHeader("Content-Type", "application/json");

        // Set the authorization header
        request.SetRequestHeader("Authorization", "Bearer " + token);

        // Send the request
        yield return request.SendWebRequest();

        // Check for errors
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
                onSuccess?.Invoke(ChangeUrlExtension(response.data.userBeevatarUrl));
            }
        }
    }

    public string ChangeUrlExtension(string url)
    {
        string withoutExtension = System.IO.Path.ChangeExtension(url, null);
        string newUrl = withoutExtension + ".glb";
        return newUrl;
    }
}

[System.Serializable]
public class ApiResponse
{
    public string status;
    public string message;
    public UserData data;
}

[System.Serializable]
public class UserData
{
    public string fullName;
    public string gender;
    public string userCode;
    public string academicGroupDesc;
    public string academicOrganizationDesc;
    public string campusDescription;
    public string userPictureUrl;
    public string userBeevatarUrl;
    public bool usingBeevatar;
}
