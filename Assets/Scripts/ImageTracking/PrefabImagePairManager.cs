using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ReadyPlayerMe.Core;
using TMPro;
using System.Collections;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

[RequireComponent(typeof(ARTrackedImageManager))]
public class PrefabImagePairManager : MonoBehaviour
{
    private ARTrackedImageManager trackedImagesManager;

    public ARCameraManager arCamera;

    private Dictionary<string, GameObject> instantiatedPrefabs = new Dictionary<string, GameObject>();

    private Dictionary<string, string> modelDictionary = new Dictionary<string, string>();

    private ApiManager apiManager;

    private QRCodeManager qrManager;

    public TMP_Text text;

    public TMP_Text text2;

    public TMP_Text text3;

    public Image ImageQr;

    private void Update()
    {
        text3.SetText("referenceLibrary: " + trackedImagesManager.referenceLibrary.count + "  modelDictionary: " + modelDictionary.Count + "  instantiatedPrefabs: " + instantiatedPrefabs.Count);
    }
    void Awake()
    {
        trackedImagesManager = GetComponent<ARTrackedImageManager>();

        qrManager = new QRCodeManager();
        apiManager = new ApiManager();
        modelDictionary = new Dictionary<string, string> { { "dummy67", "dummy67" } };
    }
    void OnEnable()
    {
        arCamera.frameReceived += OnCameraFrameReceived;
        trackedImagesManager.trackedImagesChanged += OnTrackedImagesChanged;
    }
    void OnDisable()
    {
        trackedImagesManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }
    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if ((Time.frameCount % 15) == 0)
        {
            XRCpuImage image;
            if (arCamera.TryAcquireLatestCpuImage(out image))
            {
                StartCoroutine(qrManager.DecodeQR(image, decodedText => {
                    text2.SetText(decodedText);
                    ProcessDecodedQR(decodedText);
                }));
                image.Dispose();
            }
        }
    }
    void ProcessDecodedQR(string decodedText)
    {
        var qrTexture = qrManager.GenerateQR(decodedText);
        ImageQr.sprite = Sprite.Create(qrTexture, new Rect(0, 0, qrTexture.width, qrTexture.height), new Vector2(0.02f, 0.02f));

        StartCoroutine(apiManager.GetRequest(decodedText,
        data => {
            if (!modelDictionary.ContainsKey(decodedText) && !instantiatedPrefabs.ContainsKey(decodedText))
            {
                modelDictionary.Add(decodedText, data);
                StartCoroutine(AddImageReferenceToLibrary(qrTexture, decodedText));
            } else text2.SetText("QR recorded already");
        },
        () => {
            text2.SetText("API call failed or no data received.");
        }));
    }
    public IEnumerator AddImageReferenceToLibrary(Texture2D imageReference, string imageName)
    {
        if (!trackedImagesManager.descriptor.supportsMutableLibrary)
        {
            text.SetText("Failed to convert runtimeLibrary");
            yield break;
        }

        MutableRuntimeReferenceImageLibrary runtimeLibrary = trackedImagesManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;

        if (runtimeLibrary == null)
        {
            text.SetText("Failed to create MutableRuntimeReferenceImageLibrary");
            yield break;
        }

        var jobHandle = runtimeLibrary.ScheduleAddImageWithValidationJob(imageReference, imageName, 0.05f);

        yield return jobHandle;

        if (jobHandle.status != AddReferenceImageJobStatus.Success)
        {
            text.SetText("Failed to Add ReferenceImage");
            yield break;
        }

        trackedImagesManager.referenceLibrary = runtimeLibrary;
        text.SetText("Add ReferenceImage Succeed");
    }
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            AssignModel(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            instantiatedPrefabs[trackedImage.referenceImage.name].SetActive(trackedImage.trackingState == TrackingState.Tracking);
        }

        foreach (var trackedImage in eventArgs.removed)
        {
            Destroy(instantiatedPrefabs[trackedImage.referenceImage.name]);
            instantiatedPrefabs.Remove(trackedImage.referenceImage.name);
        }
    }
    public void AssignModel(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;
        if (!instantiatedPrefabs.ContainsKey(imageName) && modelDictionary.TryGetValue(imageName, out var avatarUrl))
        {
            var avatarLoader = new AvatarObjectLoader();
            avatarLoader.OnCompleted += (_, args) =>
            {
                GameObject newAvatar = args.Avatar;
                AvatarAnimatorHelper.SetupAnimator(args.Metadata.BodyType, newAvatar);
                newAvatar.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                newAvatar.transform.SetParent(trackedImage.transform, false);
                instantiatedPrefabs.Add(imageName, newAvatar);
            };
            avatarLoader.LoadAvatar(avatarUrl);
        }
    }
}
