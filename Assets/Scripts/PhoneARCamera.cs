using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Barracuda;

using System.IO;
using TFClassify;
using System.Linq;
using System.Collections;
using TMPro;

public class PhoneARCamera : MonoBehaviour
{
    [SerializeField]
    ARCameraManager m_CameraManager;

    [SerializeField]
    GameObject m_3DBoundingBox;

    [SerializeField]
    ARRaycastManager m_RaycastManager;

    [SerializeField]
    private LayerMask layersToInclude;

    [SerializeField]
    private float confidenceFilter = 0.9f;

    [SerializeField]
    private bool enableDebugSymbology = false;

    public ARAnchorManager m_AnchorManager;
    public Camera m_ARCamera;

    [SerializeField]
    private Camera cameraUsedForCalculation;

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    const TrackableType trackableTypes = TrackableType.Planes;// TrackableType.Planes | TrackableType.FeaturePoint;

    /// <summary>
    /// Get or set the <c>ARCameraManager</c>.
    /// </summary>
    public ARCameraManager cameraManager
    {
        get => m_CameraManager;
        set => m_CameraManager = value;
    }

    [SerializeField]
    RawImage m_RawImage;

    /// <summary>
    /// The UI RawImage used to display the image on screen. (deprecated)
    /// </summary>
    public RawImage rawImage
    {
        get { return m_RawImage; }
        set { m_RawImage = value; }
    }

    public enum Detectors{
        Yolo2_tiny,
        Yolo3_tiny,
        Yolo5_ID
    };
    public Detectors selected_detector;

    public Detector detector = null;

    public float shiftX = 0f;
    public float shiftY = 0f;
    public float scaleFactor = 1;

    public Color colorTag = new Color(0.3843137f, 0, 0.9333333f);
    private static GUIStyle labelStyle;
    private static Texture2D boxOutlineTexture;
    // bounding boxes detected for current frame
    private IList<BoundingBox> boxOutlines;
    // bounding boxes detected across frames
    public List<BoundingBox> boxSavedOutlines = new List<BoundingBox>();
    public List<BoundingBox> filteredOutlines = new List<BoundingBox>();

    // lock model when its inferencing a frame
    private bool isDetecting = false;

    // the number of frames that bounding boxes stay static
    private int staticNum = 0;
    public bool localization = false;

    Texture2D m_Texture;

    void OnEnable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnCameraFrameReceived;
        }

        boxOutlineTexture = new Texture2D(1, 1);
        boxOutlineTexture.SetPixel(0, 0, this.colorTag);
        boxOutlineTexture.Apply();
        labelStyle = new GUIStyle();
        labelStyle.fontSize = 50;
        labelStyle.normal.textColor = this.colorTag;

        if (selected_detector == Detectors.Yolo2_tiny)
        {
            detector = GameObject.Find("Detector Yolo2-tiny").GetComponent<DetectorYolo2>();
        }
        else if (selected_detector == Detectors.Yolo3_tiny)
        {
            detector = GameObject.Find("Detector Yolo3-tiny").GetComponent<DetectorYolo3>();
        }
        else if (selected_detector == Detectors.Yolo5_ID)
        {
            detector = GameObject.Find("Detector ID Yolo5").GetComponent<DetectorIDYolo5>();
        }
        else
        {
            Debug.Log("DEBUG: Invalid detector model");
        }

        this.detector.Start();

        CalculateShift(this.detector.IMAGE_SIZE);
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void Awake()
    {
        m_AnchorManager = GetComponent<ARAnchorManager>();
        m_ARCamera = FindObjectOfType<ARSessionOrigin>().camera;
    }

    public void OnRefresh()
    {
        Debug.Log("DEBUG: onRefresh, removing anchors and boundingboxes");
        localization = false;
        staticNum = 0;
        // clear boubding box containers
        boxSavedOutlines.Clear();
        boxOutlines.Clear();
        // clear anchor
        AnchorCreator anchorCreator = FindObjectOfType<AnchorCreator>();
        anchorCreator.RemoveAllAnchors();
    }


    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Attempt to get the latest camera image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        XRCpuImage image;
        if (!cameraManager.TryAcquireLatestCpuImage(out image))
        {
            return;
        }

        // Once we have a valid XRCameraImage, we can access the individual image "planes"
        // (the separate channels in the image). XRCameraImage.GetPlane provides
        // low-overhead access to this data. This could then be passed to a
        // computer vision algorithm. Here, we will convert the camera image
        // to an RGBA texture (and draw it on the screen).

        // Choose an RGBA format.
        // See XRCameraImage.FormatSupported for a complete list of supported formats.
        var format = TextureFormat.RGBA32;

        if (m_Texture == null || m_Texture.width != image.width || m_Texture.height != image.height)
        {
            m_Texture = new Texture2D(image.width, image.height, format, false);
        }

        // Convert the image to format, flipping the image across the Y axis.
        // We can also get a sub rectangle, but we'll get the full image here.
        var conversionParams = new XRCpuImage.ConversionParams(image, format, XRCpuImage.Transformation.None);

        // Texture2D allows us write directly to the raw texture data
        // This allows us to do the conversion in-place without making any copies.
        var rawTextureData = m_Texture.GetRawTextureData<byte>();
        try
        {
            image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
        }
        finally
        {
            // We must dispose of the XRCameraImage after we're finished
            // with it to avoid leaking native resources.
            image.Dispose();
        }

        // Apply the updated texture data to our texture
        m_Texture.Apply();

        // If bounding boxes are static for certain frames, start localization
        if (staticNum > 150)
        {
            localization = true;
        }
        else
        {
            // detect object and create current frame outlines
            TFDetect();
            // merging outliens across frames
            //List<BoundingBox> filteredBoundingBoxes = FilterOutlines2D(this.boxOutlines);
            this.filteredOutlines = FilterOutlines2D(this.boxOutlines);
            Create3DBoundingBoxes(this.filteredOutlines);
            //GroupBoxOutlines();
        }
        // Set the RawImage's texture so we can visualize it.
        m_RawImage.texture = m_Texture;

    }

    public void OnGUI()
    {
        // Do not draw bounding boxes after localization.
        if (localization || !enableDebugSymbology)
        {
            return;
        }

        //Debug.LogWarning("OnGui");
        if (this.boxSavedOutlines != null)// && this.boxSavedOutlines.Any())
        {
            //Debug.LogWarning("this.boxSavedOutlines != null " + this.boxSavedOutlines.Count);
            foreach (var outline in this.boxSavedOutlines)
            {
                //Debug.LogWarning("DrawBoxOutline " + outline.Rect.x + ", " + outline.Rect.y);
                DrawBoxOutline(outline, scaleFactor, shiftX, shiftY);
            }
        }

        if (this.filteredOutlines != null)// && this.boxSavedOutlines.Any())
        {
            //Debug.LogWarning("this.boxSavedOutlines != null " + this.boxSavedOutlines.Count);
            foreach (var outline in this.filteredOutlines)
            {
                //Debug.LogWarning("DrawBoxOutline " + outline.Rect.x + ", " + outline.Rect.y);
                DrawBoxOutline(outline, scaleFactor, shiftX, shiftY, 2);
            }
        }
    }

    // merging bounding boxes and save result to boxSavedOutlines
    private List<BoundingBox> FilterOutlines2D(IList<BoundingBox> inBbs)
    {
        List<BoundingBox> outBbs = null;

        // no bounding boxes in current frame
        if (inBbs == null || inBbs.Count == 0)
        {
            outBbs = new List<BoundingBox>();
        }
        else
        {
            // Check for doubles
            outBbs = inBbs.Distinct().ToList();
        }

        return outBbs;
    }
#if false
    // merging bounding boxes and save result to boxSavedOutlines
    private void GroupBoxOutlines()
    {
        // if savedoutlines is empty, add current frame outlines if possible.
        if (this.boxSavedOutlines.Count == 0)
        {
            // no bounding boxes in current frame
            if (this.boxOutlines == null || this.boxOutlines.Count == 0)
            {
                return;
            }

            // deep copy current frame bounding boxes
            foreach (var outline1 in this.boxOutlines)
            {
                // Only add when it is world referenced
                if (outline1.WorldReferenced)
                {
                    this.boxSavedOutlines.Add(outline1);
                }
            }
            return;
        }

        // adding current frame outlines to existing savedOulines and merge if possible.
        bool addOutline = false;
        foreach (var outline1 in this.boxOutlines)
        {
            // Do not process oultines that are not world referenced
            if (!outline1.WorldReferenced) continue;

            bool unique = true;
            List<BoundingBox> itemsToAdd = new List<BoundingBox>();
            List<BoundingBox> itemsToRemove = new List<BoundingBox>();
            foreach (var outline2 in this.boxSavedOutlines)
            {
                // if two bounding boxes are for the same object, use high confidnece one
                if (IsSameObject(outline1, outline2))
                {
                    unique = false;
                    if (outline1.Confidence > outline2.Confidence + 0.05F) //& outline2.Confidence < 0.5F)
                    {
                        Debug.Log("DEBUG: add detected boxes in this frame.");
                        Debug.Log($"DEBUG: Add Label: {outline1.Label}. Confidence: {outline1.Confidence}.");
                        Debug.Log($"DEBUG: Remove Label: {outline2.Label}. Confidence: {outline2.Confidence}.");

                        itemsToRemove.Add(outline2);
                        itemsToAdd.Add(outline1);
                        addOutline = true;
                        staticNum = 0;
                        break;
                    }
                }
            }
            this.boxSavedOutlines.RemoveAll(item => itemsToRemove.Contains(item));
            this.boxSavedOutlines.AddRange(itemsToAdd);

            // if outline1 in current frame is unique, add it permanently
            if (unique)
            {
                Debug.Log($"DEBUG: add detected boxes in this frame");
                addOutline = true;
                staticNum = 0;
                this.boxSavedOutlines.Add(outline1);
                Debug.Log($"Add Label: {outline1.Label}. Confidence: {outline1.Confidence}.");
            }
        }
        if (!addOutline)
        {
            staticNum += 1;
        }
        /*
        // merge same bounding boxes
        // remove will cause duplicated bounding box?
        List<BoundingBox> temp = new List<BoundingBox>();
        foreach (var outline1 in this.boxSavedOutlines)
        {
            if (temp.Count == 0)
            {
                temp.Add(outline1);
                continue;
            }

            List<BoundingBox> itemsToAdd = new List<BoundingBox>();
            List<BoundingBox> itemsToRemove = new List<BoundingBox>();
            foreach (var outline2 in temp)
            {
                if (IsSameObject(outline1, outline2))
                {
                    if (outline1.Confidence > outline2.Confidence)
                    {
                        itemsToRemove.Add(outline2);
                        itemsToAdd.Add(outline1);
                        Debug.Log("DEBUG: merge bounding box conflict!!!");
                    }
                }
                else
                {
                    itemsToAdd.Add(outline1);
                }
            }
            temp.RemoveAll(item => itemsToRemove.Contains(item));
            temp.AddRange(itemsToAdd);
        }
        this.boxSavedOutlines = temp;
        */
    }
#endif
    GameObject CreateAnchorGameObject(in Vector3 pos, in Quaternion rot, in Vector3 localScale, in String label="", in float confidence=0.0f)
    {
        // create a regular anchor at the hit pose
        //Debug.Log($"DEBUG: Creating anchor for Create3DBoundingBoxes.");

        GameObject go = new GameObject($"{label}: {(int)(confidence * 100)}%");

        GameObject child = Instantiate(m_3DBoundingBox, new Vector3(0, 0, 0), Quaternion.identity);
        child.transform.SetParent(go.transform, true);
        
        Merge3DBoundingBoxes mbb = child.GetComponent(typeof(Merge3DBoundingBoxes)) as Merge3DBoundingBoxes;
        mbb.label = label; mbb.confidence = confidence;

        Rigidbody rb = child.GetComponent<Rigidbody>();
        
        var text = child.GetComponentInChildren<TextMeshPro>();
        text.text = $"{label}: {(int)(confidence * 100)}%";
        
        // Note: rect bounding box coordinates starts from top left corner.
        // AR camera starts from borrom left corner.
        // Need to flip Y axis coordinate of the anchor 2D position when raycast
        child.transform.localScale = localScale;//scaleX, scaleY, 0.001f);
        child.transform.rotation = rot;

        go.transform.position = pos;
        go.AddComponent(typeof(ARAnchor));

        //go.layer = detectedObjectsLayer;

        return go;
    }

    private void Create3DBoundingBoxes(List<BoundingBox> inBbs)
    {
        // no bounding boxes in current frame
        if (inBbs == null || inBbs.Count == 0)
        {
            return;
        }

        foreach (var outline in inBbs)
        {
            // Filter confidence
            if (outline.Confidence < confidenceFilter) continue;

            // Note: rect bounding box coordinates starts from top left corner.
            // AR camera starts from borrom left corner.
            // Need to flip Y axis coordinate of the anchor 2D position when raycast
            var xMin = outline.Dimensions.X * this.scaleFactor + this.shiftX;
            var width = outline.Dimensions.Width * this.scaleFactor;
            var yMin = outline.Dimensions.Y * this.scaleFactor + this.shiftY;
            yMin = Screen.height - yMin;
            var height = outline.Dimensions.Height * this.scaleFactor;

            float center_x = xMin + width / 2f;
            float center_y = yMin - height / 2f;

            // Used camera ref if there is one
            var cam = m_ARCamera;
            if (cameraUsedForCalculation && outline.CameraProperties.IsSet) // only reposition camera if there is a dedicated camera
            {
                cam = cameraUsedForCalculation;
                outline.CameraProperties.CopyToCamera(cam);
            }

            var worldPosCenter = cam.ScreenToWorldPoint(new Vector3(center_x, center_y, cam.nearClipPlane));
            var worldPosLeft = cam.ScreenToWorldPoint(new Vector3(xMin, center_y, cam.nearClipPlane));
            var worldPosUp = cam.ScreenToWorldPoint(new Vector3(center_x, yMin, cam.nearClipPlane));
            var worldScaleX = 2.0f * (worldPosCenter - worldPosLeft).magnitude;
            var worldScaleY = 2.0f * (worldPosCenter - worldPosUp).magnitude;
            var rotation = cam.transform.rotation;
            //CreateAnchorGameObject(worldPosCenter, rotation, worldScaleX, worldScaleY);

            if(Physics.Raycast(cam.ScreenPointToRay(new Vector3(center_x, center_y, cam.nearClipPlane)), out var hit, float.PositiveInfinity, layersToInclude))
            //if (m_RaycastManager.Raycast(new Vector2(center_x, center_y), s_Hits, trackableTypes))
            {
                int i = 0;
                //foreach (var hit in s_Hits)
                //{
                //    Debug.Log("Hits[" + i + "]: " + (hit.trackable? hit.trackable.gameObject.name : "NO_TRACKABLE??"));
                //    i++;
                //}
                //foreach (var hit in s_Hits)
                {
                    //if (hit.trackable != null && hit.trackable.gameObject.layer == roomMeshLayer) ;// != detectedObjectsLayer)
                    {
                        // Raycast hits are sorted by distance, so the first one will be the closest hit.
                        //var hit = s_Hits[0];
                        //TextMesh anchorObj = GameObject.Find("New Text").GetComponent<TextMesh>();
                        // Create a new anchor
                        //Debug.Log("Creating 3D World Reference for BoundingBox");

                        var worldHitPosCenter = hit.point;// pose.position; 
                        var worldHitPosLeft = cam.ScreenToWorldPoint(new Vector3(xMin, center_y, hit.distance)); 
                        var worldHitPosUp = cam.ScreenToWorldPoint(new Vector3(center_x, yMin, hit.distance)); 
                        var worldHitScaleX = 2.0f * (worldHitPosCenter - worldHitPosLeft).magnitude;
                        var worldHitScaleY = 2.0f * (worldHitPosCenter - worldHitPosUp).magnitude;
                        var localScale = new Vector3(worldHitScaleX, worldHitScaleY, Math.Min(worldHitScaleX, worldHitScaleY));
                        var rotationHit = Quaternion.identity;
                        if(hit.normal.x != 0 || hit.normal.z != 0) rotationHit = Quaternion.LookRotation(new Vector3(hit.normal.x, 0, hit.normal.z));
                        CreateAnchorGameObject(worldHitPosCenter, rotationHit, localScale, outline.Label, outline.Confidence);
                        /*
                        outline.WorldDimensions = new BoundingBoxWorldDimensions();
                        outline.WorldDimensions.position = worldHitPosCenter;
                        outline.WorldDimensions.orientation = rotationHit;
                        outline.WorldDimensions.localScale = new Vector3(worldHitScaleX, worldHitScaleY, Math.Min(worldHitScaleX, worldHitScaleY));
                        outline.WorldReferenced = true;

                        outline.AttachedGameObject = CreateAnchorGameObject(outline.WorldDimensions.position,
                                                                            outline.WorldDimensions.orientation,
                                                                            outline.WorldDimensions.localScale);
                        outline.AttachedGameObject.name = outline.Label;

                        var textMesh = new GameObject();
                        var text = textMesh.AddComponent<TextMesh>();
                        text.text = $"{outline.Label}: {(int)(outline.Confidence * 100)}%";
                        textMesh.transform.SetParent(outline.AttachedGameObject.transform, true);
                        textMesh.transform.localPosition = new Vector3();
                        */
                        break;
                    }
                }
            }
            else
            {
                //Debug.Log("Couldn't raycast");
            }
        }
    }
#if false
    private bool IsSameObject(BoundingBox outline1, BoundingBox outline2)
    {
        // For two bounding boxes, if at least one center is inside the other box,
        // treate them as the same object.
        if(outline1.AttachedGameObject == null || outline2.AttachedGameObject == null)
        {
            Debug.LogWarning("IsSameObject without AttachedGameObject");
            return true;
        }

        Collider collider1 = outline1.AttachedGameObject.GetComponent<Collider>();
        Collider collider2 = outline2.AttachedGameObject.GetComponent<Collider>();

        if(!collider1 || !collider2)
        {
            Debug.LogWarning("IsSameObject without Collider for AttachedGameObject");
            return true;
        }

        if (collider1.bounds.Contains(collider2.bounds.center)) return true;
        if (collider2.bounds.Contains(collider1.bounds.center)) return true;

        Collider[] hitColliders1 = Physics.OverlapBox(collider1.bounds.center, collider1.bounds.extents /2.0f, outline1.AttachedGameObject.transform.rotation);
        foreach (Collider col in hitColliders1)
        {
            if (col == collider1) return true;
            if (col.gameObject.layer == detectedObjectsLayer) ;
        }
        Collider[] hitColliders2 = Physics.OverlapBox(collider2.bounds.center, collider2.bounds.extents / 2.0f, outline2.AttachedGameObject.transform.rotation);
        foreach (Collider col in hitColliders2)
        {
            if (col == collider2) return true;
        }
        return true;
    }
#endif
    private void CalculateShift(int inputSize)
    {
        int smallest;

        if (Screen.width < Screen.height)
        {
            smallest = Screen.width;
            this.shiftY = (Screen.height - smallest) / 2f;
        }
        else
        {
            smallest = Screen.height;
            this.shiftX = (Screen.width - smallest) / 2f;
        }

        this.scaleFactor = smallest / (float)inputSize;
    }

    private void TFDetect()
    {
        if (this.isDetecting)
        {
            return;
        }

        this.isDetecting = true;

        var camProps = new CameraProperties(m_ARCamera);

        StartCoroutine(ProcessImage(this.detector.IMAGE_SIZE, result =>
        {
            StartCoroutine(this.detector.Detect(result, boxes =>
            {
                this.boxOutlines = boxes;
                foreach (var box in this.boxOutlines) box.CameraProperties = camProps;
                Resources.UnloadUnusedAssets();
                this.isDetecting = false;
            }));
        }));
    }


    private IEnumerator ProcessImage(int inputSize, System.Action<Color32[]> callback)
    {
        Coroutine croped = StartCoroutine(TextureTools.CropSquare(m_Texture,
           TextureTools.RectOptions.Center, snap =>
           {
               var scaled = Scale(snap, inputSize);
               var rotated = Rotate(scaled.GetPixels32(), scaled.width, scaled.height);
               callback(rotated);
           }));
        yield return croped;
    }


    private void DrawBoxOutline(BoundingBox outline, float scaleFactor, float shiftX, float shiftY, int frameWidth=10)
    {
        var x = outline.Dimensions.X * scaleFactor + shiftX;
        var width = outline.Dimensions.Width * scaleFactor;
        var y = outline.Dimensions.Y * scaleFactor + shiftY;
        var height = outline.Dimensions.Height * scaleFactor;

        DrawRectangle(new Rect(x, y, width, height), frameWidth, this.colorTag);
        DrawLabel(new Rect(x, y - 80, 200, 20), $"Localizing {outline.Label}: {(int)(outline.Confidence * 100)}%");
    }


    public static void DrawRectangle(Rect area, int frameWidth, Color color)
    {
        Rect lineArea = area;
        lineArea.height = frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Top line

        lineArea.y = area.yMax - frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Bottom line

        lineArea = area;
        lineArea.width = frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Left line

        lineArea.x = area.xMax - frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Right line
    }


    private static void DrawLabel(Rect position, string text)
    {
        GUI.Label(position, text, labelStyle);
    }

    private Texture2D Scale(Texture2D texture, int imageSize)
    {
        var scaled = TextureTools.scaled(texture, imageSize, imageSize, FilterMode.Bilinear);
        return scaled;
    }


    private Color32[] Rotate(Color32[] pixels, int width, int height)
    {
        var rotate = TextureTools.RotateImageMatrix(
                pixels, width, height, 90);
        // var flipped = TextureTools.FlipYImageMatrix(rotate, width, height);
        //flipped =  TextureTools.FlipXImageMatrix(flipped, width, height);
        // return flipped;
        return rotate;
    }

    public void OnConfidenceLevelChanged(float conf)
    {
        confidenceFilter = conf;
    }



}
