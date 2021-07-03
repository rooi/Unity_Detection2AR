using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class DepthOfFieldController : MonoBehaviour
{

    //Ray raycast;
    RaycastHit hit;
    bool isHit;
    float hitDistance;

    public PostProcessVolume volume;

    DepthOfField depthOfField;

    [Range(1,10)]
    public float focusSpeed = 8f;
    public float maxFocusDistance = 5f;

    public GameObject focusedObject;

    [SerializeField]
    private LayerMask layersToInclude;

    [SerializeField]
    private bool FocusOnGameObjectCenterOfHit = false;

    private Camera cam;


    private void Start()
    {
        volume.profile.TryGetSettings(out depthOfField);

        cam = transform.GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        //raycast = new Ray(transform.position, transform.forward * 100);

        isHit = false;

        if (focusedObject != null)
        {
            hitDistance = Vector3.Distance(transform.position, focusedObject.transform.position);
        }
        else
        {
            //if (Physics.Raycast(raycast, out hit, 100f, layersToInclude))
            if( Physics.Raycast(cam.ScreenPointToRay(new Vector3(0, 0, cam.nearClipPlane)), out var hit, 100f, layersToInclude))
            {
                isHit = true;

                Vector3 hitPosition;
                if (FocusOnGameObjectCenterOfHit)
                {                    
                    //if (hit.rigidbody) hitPosition = hit.transform.TransformPoint(hit.rigidbody.centerOfMass);
                    if (hit.collider) hitPosition = hit.collider.bounds.center;
                    else hitPosition = hit.point;
                }
                else hitPosition = hit.point;

                
                hitDistance = Vector3.Distance(transform.position, hitPosition);
            }
            else
            {
                if (hitDistance < maxFocusDistance)
                {
                    hitDistance++;
                }
            }
        }

        SetFocus();
    }

    void SetFocus()
    {
        Debug.Log("depth of field distance = " + hitDistance);
        depthOfField.focusDistance.value = Mathf.Lerp(depthOfField.focusDistance.value, hitDistance, Time.deltaTime * focusSpeed);
    }

    private void OnDrawGizmos()
    {
        if(isHit)
        {
            Vector3 hitPosition;
            if (FocusOnGameObjectCenterOfHit)
            {
                //if (hit.rigidbody) hitPosition = hit.transform.TransformPoint(hit.rigidbody.centerOfMass);
                if (hit.collider) hitPosition = hit.collider.bounds.center;
                else hitPosition = hit.point;
            }
            else hitPosition = hit.point;

            Gizmos.DrawSphere(hitPosition, 0.1f);

            Debug.DrawRay(transform.position, transform.forward * Vector3.Distance(transform.position, hitPosition));
        }
        else
        {
            Debug.DrawRay(transform.position,transform.forward * 100f);
        }
    }
}
