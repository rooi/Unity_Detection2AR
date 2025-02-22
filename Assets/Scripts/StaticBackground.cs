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
using UnityEngine.Windows.WebCam;

public class StaticBackground : MonoBehaviour
{
	[SerializeField]
	RawImage m_RawImage;

	public enum Detectors
	{
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
	// lock model when its inferencing a frame
	[SerializeField]
	private bool isDetecting = false;

	// the number of frames that bounding boxes stay static
	[SerializeField]
	private int staticNum = 0;
	public bool localization = false;

	Texture2D m_Texture;
	[SerializeField]
	private Sprite sprite;

	private void Start()
	{
		if (m_Texture == null || m_Texture.width != sprite.texture.width || m_Texture.height != sprite.texture.height)
		{
			m_Texture = new Texture2D(sprite.texture.width, sprite.texture.height, TextureFormat.RGBA32, false);
		}
		m_Texture.SetPixels32(sprite.texture.GetPixels32());
		m_Texture.Apply();


		byte[] bytes = m_Texture.EncodeToPNG();
		var dirPath = Application.dataPath + "/../SaveImages/";
		if (!Directory.Exists(dirPath))
		{
			Directory.CreateDirectory(dirPath);
		}
		File.WriteAllBytes(dirPath + "Image1" + ".png", bytes);


		//if (staticNum > 150)
		//{
		//	localization = true;
		//}
		//else
		{
			// detect object and create current frame outlines
			TFDetect();
			// merging outliens across frames
			//GroupBoxOutlines();
		}
		m_RawImage.texture = m_Texture;
	}

	void OnEnable()
	{
		boxOutlineTexture = new Texture2D(1, 1);
		boxOutlineTexture.SetPixel(0, 0, this.colorTag);
		boxOutlineTexture.Apply();
		labelStyle = new GUIStyle();
		labelStyle.fontSize = 50;
		labelStyle.normal.textColor = this.colorTag;

		if (selected_detector == Detectors.Yolo2_tiny)
		{
			throw new NotImplementedException("Not using Yolo2 for now");
			//detector = GameObject.Find("Detector Yolo2-tiny").GetComponent<DetectorYolo2>();
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
	public void OnGUI()
	{
		// Do not draw bounding boxes after localization.
		if (localization)
		{
			return;
		}

		if (this.boxSavedOutlines != null && this.boxSavedOutlines.Any())
		{
			foreach (var outline in this.boxSavedOutlines)
			{
				DrawBoxOutline(outline, scaleFactor, shiftX, shiftY);
			}
		}
	}

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
			foreach (var outline in this.boxOutlines)
			{
				this.boxSavedOutlines.Add(outline);
			}
			return;
		}

		// adding current frame outlines to existing savedOulines and merge if possible.
		bool addOutline = false;
		foreach (var outline1 in this.boxOutlines)
		{
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
	}

	// For two bounding boxes, if at least one center is inside the other box,
	// treate them as the same object.
	private bool IsSameObject(BoundingBox outline1, BoundingBox outline2)
	{
		var xMin1 = outline1.Dimensions.X * this.scaleFactor + this.shiftX;
		var width1 = outline1.Dimensions.Width * this.scaleFactor;
		var yMin1 = outline1.Dimensions.Y * this.scaleFactor + this.shiftY;
		var height1 = outline1.Dimensions.Height * this.scaleFactor;
		float center_x1 = xMin1 + width1 / 2f;
		float center_y1 = yMin1 + height1 / 2f;

		var xMin2 = outline2.Dimensions.X * this.scaleFactor + this.shiftX;
		var width2 = outline2.Dimensions.Width * this.scaleFactor;
		var yMin2 = outline2.Dimensions.Y * this.scaleFactor + this.shiftY;
		var height2 = outline2.Dimensions.Height * this.scaleFactor;
		float center_x2 = xMin2 + width2 / 2f;
		float center_y2 = yMin2 + height2 / 2f;

		bool cover_x = (xMin2 < center_x1) && (center_x1 < (xMin2 + width2));
		bool cover_y = (yMin2 < center_y1) && (center_y1 < (yMin2 + height2));
		bool contain_x = (xMin1 < center_x2) && (center_x2 < (xMin1 + width1));
		bool contain_y = (yMin1 < center_y2) && (center_y2 < (yMin1 + height1));

		return (cover_x && cover_y) || (contain_x && contain_y);
	}

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
		StartCoroutine(ProcessImage(this.detector.IMAGE_SIZE, result =>
		{
			StartCoroutine(this.detector.Detect(result, boxes =>
			{
				this.boxOutlines = boxes;
				GroupBoxOutlines();
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


	private void DrawBoxOutline(BoundingBox outline, float scaleFactor, float shiftX, float shiftY)
	{
		var x = outline.Dimensions.X * scaleFactor + shiftX;
		var width = outline.Dimensions.Width * scaleFactor;
		var y = outline.Dimensions.Y * scaleFactor + shiftY;
		var height = outline.Dimensions.Height * scaleFactor;

		DrawRectangle(new Rect(x, y, width, height), 10, this.colorTag);
		DrawLabel(new Rect(x, y - 80, 200, 20), $"Localizing {outline.Label}: {(int)(outline.Confidence * 100)}%");
	}


	public void DrawRectangle(Rect area, int frameWidth, Color color)
	{
		var topLeftPoint = new Vector2(area.x, area.y);
		var topRightPoint = new Vector2(area.xMax, area.y);
		var bottomLeftPoint = new Vector2(area.x, area.yMax);
		var bottomRightPoint = new Vector2(area.xMax, area.yMax);
//		m_Texture.DrawLine(topLeftPoint, topRightPoint, color); //top line
//		m_Texture.DrawLine(bottomLeftPoint, bottomRightPoint, color); //bottom line
//		m_Texture.DrawLine(topLeftPoint, bottomLeftPoint, color); //left line
//		m_Texture.DrawLine(topRightPoint, bottomRightPoint, color); //right line
		m_Texture.Apply();

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



}
