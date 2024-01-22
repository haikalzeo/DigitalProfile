using UnityEngine;
using System.Runtime.InteropServices;

public class NativeAPI : MonoBehaviour
{
    #if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
            public static extern void sendMessageToRN(string message);
    #endif
}

public class MessageSent : MonoBehaviour
{
    public static MessageSent instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void SentRNMessage(string log)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            using (AndroidJavaClass jc = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
            {
                jc.CallStatic("sendMessageToRN", log);
            }
        }
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
#if UNITY_IOS && !UNITY_EDITOR
                    NativeAPI.sendMessageToRN(log);
#endif
        }
    }
}