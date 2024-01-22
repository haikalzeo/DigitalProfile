using ReadyPlayerMe.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using static NativeGallery;

public class RaycastIndicator : MonoBehaviour
{
    private QRCodeManager qrManager;
    private ApiManager apiManager;

    public ARCameraManager ARCamera;

    ARAnchor anchor;
    ARRaycastManager raycastManager;
    ARPlaneManager planeManager;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private Dictionary<string, GameObject> instantiatedAvatar = new Dictionary<string, GameObject>();

    private UnityEngine.Pose placementPose;
    public GameObject Indicator;
    public TMP_Text text;
    public Image ScanQrIcon;
    public VideoPlayer ScanPlane;
    public RawImage ScanPlanUI;
    public RawImage BlankScreen;
    public GameObject rnComponent;
    public Image textBg;

    private string otherAvatarUrl = null;
    public string myAvatarUrl = null;
    public string token = null;
    public string baseUrl = null;

    public Button PlaceAvatarBtn;
    public Button ScanMoreQrBtn;
    public Button CancelScanQrBtn;
    public Button MyAvatarBtn;
    public Button CameraBtn;
    public Button CaptureBtn;

    private bool isPlacementPoseValid = false;
    private bool isScanQrPhase = true;
    private bool isIdlePhase = false;
    private bool isMyAvatarPhase = false;
    private bool isCameraPhase = false;
    private bool isNativeGalleryGranted = false;

    public RuntimeAnimatorController masculineController;
    public RuntimeAnimatorController feminineController;

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        planeManager = GetComponent<ARPlaneManager>();
        qrManager = new QRCodeManager();
        apiManager = new ApiManager();
    }
    private IEnumerator Start()
    {
        // Wait until the DataFromReact component has at least one
        while (string.IsNullOrEmpty(rnComponent.GetComponent<DataFromReact>().avatarUrl) &&
               string.IsNullOrEmpty(rnComponent.GetComponent<DataFromReact>().token) &&
               string.IsNullOrEmpty(rnComponent.GetComponent<DataFromReact>().baseUrl))
        {
            yield return null; // Wait for one frame
        }

        myAvatarUrl = apiManager.ChangeUrlExtension(rnComponent.GetComponent<DataFromReact>().avatarUrl);
        token = rnComponent.GetComponent<DataFromReact>().token;
        baseUrl = rnComponent.GetComponent<DataFromReact>().baseUrl;

        // If the myAvatarUrl is not empty, enable the My Avatar button
        if (!string.IsNullOrEmpty(myAvatarUrl))
        {
            DeactivateButton(MyAvatarBtn, false);
        }
    }
    void OnEnable()
    {
        ARCamera.frameReceived += OnCameraFrameReceived;
    }
    void Update()
    {
        string avatarUrl = isMyAvatarPhase ? myAvatarUrl : otherAvatarUrl;
        if (!isIdlePhase && !isScanQrPhase && !isCameraPhase && !string.IsNullOrEmpty(avatarUrl))
        {
            UpdatePlacement();
        }
    }
    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (isIdlePhase || isCameraPhase || !isScanQrPhase || (Time.frameCount % 8) != 0)
        {
            return;
        }

        XRCpuImage image;
        // Try to acquire the latest image from the AR camera
        if (ARCamera.TryAcquireLatestCpuImage(out image))
        {
            // Start a coroutine to decode the QR code from the image
            StartCoroutine(qrManager.DecodeQR(image, decodedText =>
            {
                text.SetText("");

                // If the decoded text does not contain the expected prefix, it's an invalid QR code
                if (!decodedText.Contains("digitalprofile-friend"))
                {
                    text.SetText("Invalid QR code");
                    return;
                }

                isScanQrPhase = false;

                ScanQrIcon.gameObject.SetActive(false);

                CancelScanQrBtn.gameObject.SetActive(false);

                ActivateButton(ScanMoreQrBtn, true);

                StartCoroutine(TextBG("QR code recognized. Retrieving data...", false));

                // Start a coroutine to send a POST request with the decoded text
                StartCoroutine(apiManager.PostRequest(decodedText, token, baseUrl, result =>
                {
                    textBg.gameObject.SetActive(false);

                    // If the avatar has already been instantiated from this QR code, display a message
                    if (instantiatedAvatar.ContainsKey(result))
                    {
                        text.SetText("Beevatar has already been placed from this QR code");
                        return;
                    }

                    StartCoroutine(TextBG("Scan successful", true));

                    // Store the URL of the other avatar
                    otherAvatarUrl = result;

                }, errorMessage =>
                {
                    textBg.gameObject.SetActive(false);
                    text.SetText(errorMessage);
                }));
            }));

            // Dispose of the image to free up resources
            image.Dispose();
        }

    }
    IEnumerator CaptureImage()
    {
        // Dynamically create a Render Texture
        int width = Screen.width;
        int height = Screen.height;
        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        Camera.main.targetTexture = renderTexture;

        // Wait until rendering is complete
        yield return new WaitForEndOfFrame();

        // Read pixels from the render texture into a new texture
        Texture2D imageTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        imageTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        imageTexture.Apply();

        string fileName = "Beevatar " + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        Permission permission = SaveImageToGallery(imageTexture, "Beevatar", fileName, (success, path) => {
            if (success)
            {
                StartCoroutine(TextBG("Photo saved successfully", true));
            }
            else
            {
                text.SetText("Failed to save photo");
            }
        });

        // Reset camera's targetTexture to null to resume rendering to the screen
        Camera.main.targetTexture = null;

        // Release the dynamically created Render Texture
        renderTexture.Release();
        Destroy(renderTexture);
        Destroy(imageTexture);
    }

    void SetScanPlanePhase(bool isActive)
    {
        if (isActive)
        {
            ScanPlane.Prepare();
            ScanPlane.prepareCompleted += HandlePrepareCompleted;
        }
        else
        {
            ScanPlane.Stop();
        }
        ScanPlanUI.gameObject.SetActive(isActive);

        ScanMoreQrBtn.interactable = !isActive;
        if (!string.IsNullOrEmpty(myAvatarUrl))
        {
            MyAvatarBtn.interactable = !isActive;
        }
        if (instantiatedAvatar.Count > 0)
        {
            CameraBtn.interactable = !isActive;
        }
    }

    void HandlePrepareCompleted(VideoPlayer player)
    {
        player.prepareCompleted -= HandlePrepareCompleted;
        player.Play();
    }

    void UpdatePlacement()
    {
        // If the raycast manager is null, exit the method
        if (raycastManager == null) return;

        // If there are no trackable planes, start the scan plane phase
        if (planeManager.trackables.count == 0)
        {
            SetScanPlanePhase(true);
        }
        // If there are trackable planes, stop the scan plane phase
        else
        {
            SetScanPlanePhase(false);
        }

        // Get the center of the screen
        var screenCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));

        // Determine the URL of the avatar to place
        string avatarUrl = isMyAvatarPhase ? myAvatarUrl : otherAvatarUrl;

        // Perform a raycast from the center of the screen
        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            text.SetText("");

            // Check if the raycast hit a trackable plane
            isPlacementPoseValid = hits.Count > 0;

            // If the raycast hit a trackable plane, update the placement pose
            if (isPlacementPoseValid)
            {
                // Get the pose of the hit
                placementPose = hits[0].pose;

                // Get the forward direction of the camera
                var cameraForward = Camera.main.transform.forward;

                // Normalize the forward direction to get the bearing
                var cameraBearing = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

                // Set the rotation of the placement pose to face the camera
                placementPose.rotation = Quaternion.LookRotation(cameraBearing);

                Indicator.SetActive(true);
                Indicator.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);

                // If there's an avatar URL, activate the Place Avatar button
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    ActivateButton(PlaceAvatarBtn, true);
                }
            }
        }
        // If the raycast didn't hit a trackable plane but there are trackable planes, hide the indicator and show a message
        else if (planeManager.trackables.count > 0)
        {
            Indicator.SetActive(false);
            text.SetText("Find a nearby surface to place beevatar");

            // If there's an avatar URL, disable the Place Avatar button
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                DisableButton(PlaceAvatarBtn, true);
            }
        }
    }

    void InitiateAvatar()
    {
        // Determine the URL of the avatar to load
        string avatarUrl = isMyAvatarPhase ? myAvatarUrl : otherAvatarUrl;

        // Create a new avatar loader
        var avatarLoader = new AvatarObjectLoader();

        // Set up the event handler for when the avatar has loaded
        avatarLoader.OnCompleted += (_, args) =>
        {
            // Get the loaded avatar
            GameObject avatar = args.Avatar;

            // Set the animator controller based on the avatar's outfit gender
            SetAnimatorController(args.Metadata.OutfitGender, avatar);

            // Set the avatar's transform
            SetAvatarTransform(avatar);

            // Get the ARAnchor component from the avatar
            anchor = avatar.GetComponent<ARAnchor>();

            // If the avatar doesn't have an ARAnchor component, add one
            if (anchor == null)
            {
                anchor = avatar.AddComponent<ARAnchor>();
            }

            // Add the avatar to the dictionary of instantiated avatars
            instantiatedAvatar.Add(avatarUrl, avatar);

            // If this is the user's avatar, reset the myAvatarUrl
            if (isMyAvatarPhase)
            {
                isMyAvatarPhase = false;
                myAvatarUrl = null;
            }

            // Reset the otherAvatarUrl
            otherAvatarUrl = null;

            // Hide the indicator
            Indicator.SetActive(false);

            DeactivateButton(CameraBtn, false);

            textBg.gameObject.SetActive(false);
        };

        // Set up the event handler for when the avatar fails to load
        avatarLoader.OnFailed += (_, args) =>
        {
            textBg.gameObject.SetActive(false);

            // If this is the user's avatar, deactivate the My Avatar button
            if (isMyAvatarPhase)
            {
                DeactivateButton(MyAvatarBtn, false);
            }

            text.SetText("Failed to load beevatar");
        };

        // Start loading the avatar
        avatarLoader.LoadAvatar(avatarUrl);
    }

    void SetAvatarTransform(GameObject avatar)
    {
        avatar.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
        avatar.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
        avatar.transform.Rotate(0, 180, 0);
    }

    private void SetAnimatorController(OutfitGender outfitGender, GameObject avatar)
    {
        var animator = avatar.GetComponent<Animator>();
        if (animator != null)
        {
            if(outfitGender == OutfitGender.Masculine)
            {
                animator.runtimeAnimatorController = masculineController;
            } 
            else
            {
                animator.runtimeAnimatorController = feminineController;
            }
        }
    }

    public void HandlePlacedAvatarBtn()
    {
        string avatarUrl = isMyAvatarPhase ? myAvatarUrl : otherAvatarUrl;
        if (isPlacementPoseValid && !string.IsNullOrEmpty(avatarUrl))
        {
            ResetPhase(true);

            isIdlePhase = true;

            ActivateButton(ScanMoreQrBtn, true);

            if(isMyAvatarPhase)
            {
                DisableButton(MyAvatarBtn, false);
            }
            StartCoroutine(TextBG("Initializing beevatar...", false));

            InitiateAvatar();
        }
    }

    public void HandleScanMoreQrCodeBtn()
    {
        ResetPhase(false);

        isScanQrPhase = true;

        ScanQrIcon.gameObject.SetActive(true);

        ScanMoreQrBtn.gameObject.SetActive(false);

        ActivateButton(CancelScanQrBtn, true);
    }

    public void HandleCancelScanQrCodeBtn()
    {
        ResetPhase(false);

        isIdlePhase = true;

        ActivateButton(ScanMoreQrBtn, true);
    }

    public void HandleMyAvatarBtn()
    {
        if (!string.IsNullOrEmpty(myAvatarUrl))
        {
            ResetPhase(false);
            
            isMyAvatarPhase = true;

            ActivateButton(MyAvatarBtn, false);
        }
    }

    private async void RequestPermissionAsynchronously(PermissionType permissionType, MediaType mediaTypes, Action<bool> callback)
    {
        Permission permission = await RequestPermissionAsync(permissionType, mediaTypes);
        isNativeGalleryGranted = permission == Permission.Granted;

        callback?.Invoke(isNativeGalleryGranted);
    }

    private void ActivateCameraPhase()
    {
        ResetPhase(false);
        isCameraPhase = true;
        ActivateButton(CameraBtn, false);
        CaptureBtn.gameObject.SetActive(true);
    }

    public void HandleCameraBtn()
    {
        // Guard clause to exit the method if there are no instantiated avatars
        if (instantiatedAvatar.Count == 0)
        {
            return;
        }

        // If the native gallery permission has been granted, activate the camera phase and exit the method
        if (isNativeGalleryGranted)
        {
            ActivateCameraPhase();
            return;
        }

        // If the native gallery permission has not been granted, request it asynchronously
        RequestPermissionAsynchronously(PermissionType.Read | PermissionType.Write, MediaType.Image, (permissionResult) =>
        {
            if (permissionResult)
            {
                // If the permission is granted, activate the camera phase
                ActivateCameraPhase();
            }
            else
            {
                text.SetText("Access permission not granted");
            }
        });
    }

    public void HandleCaptureBtn()
    {
        StartCoroutine(Blank());
        StartCoroutine(CaptureImage());
    }

    IEnumerator TextBG(string text, bool isAutoDisapper)
    {
        textBg.gameObject.transform.GetChild(0).GetComponent<TMP_Text>().SetText(text);
        textBg.gameObject.SetActive(true);
        if(isAutoDisapper)
        {
            yield return new WaitForSeconds(2f);
            textBg.gameObject.SetActive(false);
        }
    }

    IEnumerator Blank()
    {
        BlankScreen.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        BlankScreen.gameObject.SetActive(false);
    }

    void ActivateButton(Button btn, bool isTextBtn)
    {
        btn.gameObject.SetActive(true);
        btn.interactable = true;
        btn.GetComponent<Image>().color = new Color(17 / 255f, 141 / 255f, 212 / 255f);
        SetBtnIconColor(btn, isTextBtn, Color.white);
    }

    void DisableButton(Button btn, bool isTextBtn)
    {
        btn.interactable = false;
        btn.GetComponent<Image>().color = Color.gray;
        SetBtnIconColor(btn, isTextBtn, Color.white);
    }

    void DeactivateButton(Button btn, bool isTextBtn)
    {
        btn.gameObject.SetActive(true);
        btn.interactable = true;
        btn.GetComponent<Image>().color = Color.white;
        Color color = new Color(170 / 255f, 170 / 255f, 170 / 255f);
        SetBtnIconColor(btn, isTextBtn, color);
    }

    void SetBtnIconColor(Button btn, bool isTextBtn, Color color)
    {
        if (isTextBtn)
        {
            btn.GetComponentInChildren<TextMeshProUGUI>().color = color;
        }
        else
        {
            btn.GetComponentInChildren<RawImage>().color = color;
        }
    }

    public void ResetPhase(bool isInstantiatePhase)
    {
        text.SetText("");

        isIdlePhase = false;
        isScanQrPhase = false;
        isCameraPhase = false;
        
        if(!isInstantiatePhase)
        {
            otherAvatarUrl = null;
            isMyAvatarPhase = false;
        }

        Indicator.SetActive(false);

        ScanQrIcon.gameObject.SetActive(false);

        PlaceAvatarBtn.gameObject.SetActive(false);

        CancelScanQrBtn.gameObject.SetActive(false);

        DeactivateButton(ScanMoreQrBtn, true);

        if (!string.IsNullOrEmpty(myAvatarUrl))
        {
            DeactivateButton(MyAvatarBtn, false);
        }

        if (instantiatedAvatar.Count > 0)
        {
            DeactivateButton(CameraBtn, false);
            CaptureBtn.gameObject.SetActive(false);
        }
    }
}
