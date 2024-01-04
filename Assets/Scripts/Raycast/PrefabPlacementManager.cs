using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PrefabPlacementManager : MonoBehaviour
{
    [SerializeField]
    private GameObject prefab;

    private GameObject spawnedPrefab;

    private ARRaycastManager raycastManager;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    
    UnityEvent placementUpdate;

    [SerializeField]
    GameObject visualObject;

    public void DisableVisual()
    {
        visualObject.SetActive(false);
    }

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();

        if (placementUpdate == null) placementUpdate = new UnityEvent();

        placementUpdate.AddListener(DisableVisual);
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        if (Input.touchCount < 1)
        {
            touchPosition = default;
            return false;
        }
        touchPosition = Input.GetTouch(0).position;
        return true;
    }

    void Update()
    {
        if (!TryGetTouchPosition(out Vector2 touchPosition)) return;

        if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = hits[0].pose;

            if (spawnedPrefab == null) spawnedPrefab = Instantiate(prefab, hitPose.position, hitPose.rotation);
            else spawnedPrefab.transform.position = hitPose.position;
        }
    }
}
