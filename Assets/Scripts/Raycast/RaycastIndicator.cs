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

    private string otherAvatarUrl = null;
    public string myAvatarUrl = null;

    private UnityEngine.Pose placementPose;
    public GameObject Indicator;
    public TMP_Text text;
    public Image ScanQrIcon;
    public VideoPlayer ScanPlane;
    public RawImage ScanPlanUI;
    public RawImage BlankScreen;

    public Button PlaceAvatarBtn;
    public Button ScanMoreQrBtn;
    public Button CancelScanQrBtn;
    public Button MyAvatarBtn;
    public Button CameraBtn;
    public Button CaptureBtn;

    private bool isPlacementPoseValid = false;
    private bool isScanQrPhase = false;
    private bool isIdlePhase = false;
    private bool isMyAvatarPhase = true;
    private bool isCameraPhase = false;
    private bool isNativeGalleryGranted = false;

    public RuntimeAnimatorController masculineController;
    public RuntimeAnimatorController feminineController;

    public GameObject rnComponent;
    public string token;

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        planeManager = GetComponent<ARPlaneManager>();
        qrManager = new QRCodeManager();
        apiManager = new ApiManager();
        myAvatarUrl = rnComponent.GetComponent<DataFromReact>().avatarUrl;
        token = rnComponent.GetComponent<DataFromReact>().token;
    }
    void OnEnable()
    {
        ARCamera.frameReceived += OnCameraFrameReceived;
    }
    void Update()
    {
        if(!isIdlePhase && !isScanQrPhase && !isCameraPhase)
        {
            UpdatePlacement();
        }
    }
    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (!isIdlePhase && !isCameraPhase && isScanQrPhase && (Time.frameCount % 15) == 0)
        {
            XRCpuImage image;
            if (ARCamera.TryAcquireLatestCpuImage(out image))
            {
                StartCoroutine(qrManager.DecodeQR(image, decodedText =>
                {
                    StartCoroutine(apiManager.PostRequest(decodedText, token, result =>
                    {
                        text.SetText("");
                        if (instantiatedAvatar.ContainsKey(result))
                        {
                            text.SetText("Avatar has already been placed from this QR Code");
                            return;
                        }
                        otherAvatarUrl = result;

                        isScanQrPhase = false;

                        ScanQrIcon.gameObject.SetActive(false);

                        CancelScanQrBtn.gameObject.SetActive(false);
                        ActivateButton(ScanMoreQrBtn, true);

                    }, () =>
                    {
                        text.SetText("API call failed. Unable to retrieve data.");
                    }));
                }));
                image.Dispose();
            }
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
        Permission permission = SaveImageToGallery(imageTexture, "Beevatar", fileName, (success, path) => text.SetText("Media save result: " + success + " " + path));

        // Reset camera's targetTexture to null to resume rendering to the screen
        Camera.main.targetTexture = null;

        // Release the dynamically created Render Texture
        renderTexture.Release();
        Destroy(renderTexture);
        Destroy(imageTexture);
    }

    void SetScanPlanePhase(bool isActive)
    {
        ScanPlane.Pause();
        ScanPlane.gameObject.SetActive(isActive);
        ScanPlanUI.gameObject.SetActive(isActive);

        if (isActive)
        {
            ScanPlane.Play();
        }

        ScanMoreQrBtn.gameObject.SetActive(!isActive);
        MyAvatarBtn.gameObject.SetActive(!isActive);
        CameraBtn.gameObject.SetActive(!isActive);
    }

    void UpdatePlacement ()
    {
        if (raycastManager == null) return;

        if (planeManager.trackables.count == 0)
        {
            SetScanPlanePhase(true);
        }
        else
        {
            SetScanPlanePhase(false);
        }

        var screenCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            text.SetText("");
            isPlacementPoseValid = hits.Count > 0;    
            if(isPlacementPoseValid)
            {
                placementPose = hits[0].pose;
                var cameraForward = Camera.main.transform.forward;
                var cameraBearing = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
                placementPose.rotation = Quaternion.LookRotation(cameraBearing);

                Indicator.SetActive(true);
                Indicator.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);

                if((!isMyAvatarPhase && otherAvatarUrl != null) || (isMyAvatarPhase && myAvatarUrl != null))
                {
                    ActivateButton(PlaceAvatarBtn, true);
                }
            }
        }
        else if (planeManager.trackables.count > 0)
        {
            Indicator.SetActive(false);
            text.SetText("Find a nerby surface to place avatar");
            if((!isMyAvatarPhase && otherAvatarUrl != null) || (isMyAvatarPhase && myAvatarUrl != null))
            {
                DisableButton(PlaceAvatarBtn, true);
            }
        }
    }

    void InitiateAvatar()
    {
        string avatarUrl = isMyAvatarPhase ? myAvatarUrl : otherAvatarUrl;
        var avatarLoader = new AvatarObjectLoader();
        avatarLoader.OnCompleted += (_, args) =>
        {
            GameObject avatar = args.Avatar;
            SetAnimatorController(args.Metadata.OutfitGender, avatar);
            SetAvatarTransform(avatar);
            anchor = avatar.GetComponent<ARAnchor>();
            if (anchor == null)
            {
                anchor = avatar.AddComponent<ARAnchor>();
            }
            instantiatedAvatar.Add(avatarUrl, avatar);
            if (isMyAvatarPhase)
            {
                isMyAvatarPhase = false;
                myAvatarUrl = null;
            }
            otherAvatarUrl = null;
            Indicator.SetActive(false);
            DeactivateButton(CameraBtn, false);
        };
        avatarLoader.OnFailed += (_, args) =>
        {
            DeactivateButton(MyAvatarBtn, false);
            text.SetText("Failed to load avatar");
        };
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
        if (animator != null && outfitGender == OutfitGender.Masculine)
        {
            animator.runtimeAnimatorController = masculineController;
        }
        else
        {
            animator.runtimeAnimatorController = feminineController;
        }
    }
    public bool IsGlbUrlValid(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            text.SetText("URL is null or empty");
            return false;
        }

        if (!url.StartsWith("https://models.readyplayer.me/") || !url.EndsWith(".glb"))
        {
            text.SetText("URL does not match RPM Model URI");
            return false;
        }

        Uri uriResult;
        bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        if (!result)
        {
            text.SetText("URL is not a valid URI");
            return false;
        }
        return true;
    }

    public void HandlePlacedAvatarBtn()
    {
        if (isPlacementPoseValid && ((!isMyAvatarPhase && otherAvatarUrl != null) || (isMyAvatarPhase && myAvatarUrl != null)))
        {
            ResetPhase(true);

            isIdlePhase = true;

            ActivateButton(ScanMoreQrBtn, true);

            if(isMyAvatarPhase)
            {
                DisableButton(MyAvatarBtn, false);
            }

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
        if (myAvatarUrl != null)
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
        if (instantiatedAvatar.Count > 0)
        {
            if (!isNativeGalleryGranted)
            {
                RequestPermissionAsynchronously(PermissionType.Read | PermissionType.Write, MediaType.Image, (permissionResult) =>
                {
                    if (permissionResult)
                    {
                        ActivateCameraPhase();
                    }
                    else
                    {
                        text.SetText("Access permission not granted");
                    }
                });
            }
            else
            {
                ActivateCameraPhase();
            }
        }
    }

    public void HandleCaptureBtn()
    {
        StartCoroutine(Blank());
        StartCoroutine(CaptureImage());
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

        if (myAvatarUrl != null)
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
