using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;

public class ApiManager
{
    public IEnumerator PostRequest(string encodedData, string token, string baseUrl, System.Action<string> onSuccess, System.Action<string> onFailed)
    {
        // Define the URL to call
        string url = baseUrl + "/Friend/GetBeevatarUrlFromQR";

        // Get Encrypted Data
        string encryptedData = GetEncryptedData(encodedData);

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
            onFailed?.Invoke("Failed to retrieve data, connection error");
            yield break;
        }

        var response = request.downloadHandler.text;

        if (request.responseCode == 400 && response == "QR code already expired")
        {
            onFailed?.Invoke("Failed to retrieve data, " + response);
            yield break;
        }

        if (request.responseCode != 200 || string.IsNullOrEmpty(response))
        {
            onFailed?.Invoke("Failed to retrieve data");
            yield break;
        }

        var data = JsonUtility.FromJson<ApiResponse>(response);
        var userBeevatarUrl = ChangeUrlExtension(data.beevatarUrl);

        if(string.IsNullOrEmpty(userBeevatarUrl))
        {
            onFailed?.Invoke("The user has not created a beevatar");
            yield break;
        }

        // On Success
        onSuccess?.Invoke(userBeevatarUrl);
    }

    public string ChangeUrlExtension(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        Uri uri = new Uri(url);
        string withoutQuery = uri.GetLeftPart(UriPartial.Path);
        string withoutExtension = System.IO.Path.ChangeExtension(withoutQuery, null);
        string newUrl = withoutExtension + ".glb";
        return newUrl;
    }

    public string GetEncryptedData(string url)
    {
        string prefix = "digitalprofile-friend-";
        int index = url.IndexOf(prefix);
        if (index >= 0)
        {
            return url.Substring(index + prefix.Length);
        }
        else
        {
            return null;
        }
    }
}

[System.Serializable]
public class ApiResponse
{
    public string beevatarUrl;
}

