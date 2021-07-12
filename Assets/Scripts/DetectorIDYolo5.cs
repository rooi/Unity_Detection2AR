using System;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

public class YoloV5Result
{
    /// <summary>
    /// x1, y1, x2, y2 in page coordinates.
    /// <para>left, top, right, bottom.</para>
    /// </summary>
    public float[] BBox { get; }

    /// <summary>
    /// The Bbox category.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Confidence level.
    /// </summary>
    public float Confidence { get; }

    public YoloV5Result(float[] bbox, string label, float confidence)
    {
        BBox = bbox;
        Label = label;
        Confidence = confidence;
    }
}

public class DetectorIDYolo5 : MonoBehaviour, Detector
{

    public NNModel modelFile;
    public TextAsset labelsFile;

    private const int IMAGE_MEAN = 0;
    private const float IMAGE_STD = 255.0F;

    // ONNX model input and output name. Modify when switching models.
    //These aren't const values because they need to be easily edited on the component before play mode

    public string INPUT_NAME;
    public string OUTPUT_NAME;

    //This has to stay a const
    private const int _image_size = 640;
    public int IMAGE_SIZE { get => _image_size; }

    // Minimum detection confidence to track a detection
    public float MINIMUM_CONFIDENCE = 0.3f;

    static readonly string[] classesNames = new string[] {  "Enhancement - pistol", "Container - plastic",
                                                            "Power source - battery pack", 
                                                            "Main Charge – high explosive military munitions",
                                                            "Switch – pressure plate (victim operated)",
                                                            "Enhancement – electric wire",
                                                            "Initiator – electric detonator" };


    private IWorker worker;

    // public const int ROW_COUNT_L = 13;
    // public const int COL_COUNT_L = 13;
    // public const int ROW_COUNT_M = 26;
    // public const int COL_COUNT_M = 26;
    public Dictionary<string, int> params_ = new Dictionary<string, int>(){{"ROW_COUNT", 13}, {"COL_COUNT", 13}, {"CELL_WIDTH", 32}, {"CELL_HEIGHT", 32}};
    public const int BOXES_PER_CELL = 3 ;
    public const int BOX_INFO_FEATURE_COUNT = 5;

    //Update this!
    public int CLASS_COUNT;

    // public const float CELL_WIDTH_L = 32;
    // public const float CELL_HEIGHT_L = 32;
    // public const float CELL_WIDTH_M = 16;
    // public const float CELL_HEIGHT_M = 16;
    private string[] labels;

    private float[] anchors = new float[]
    {
        10F, 14F,  23F, 27F,  37F, 58F,  81F, 82F,  135F, 169F,  344F, 319F // yolov3-tiny
    };


    public void Start()
    {
        this.labels = Regex.Split(this.labelsFile.text, "\n|\r|\r\n")
            .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        var model = ModelLoader.Load(this.modelFile);
        // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html
        //These checks all check for GPU before CPU as GPU is preferred if the platform + rendering pipeline support it
        this.worker = GraphicsWorker.GetWorker(model);
    }

    public IEnumerator Detect(RenderTexture rt, System.Action<IList<BoundingBox>> callback)
    {
        using (var tensor = TransformInput(rt))
        //using (var tensor = new Tensor(tex))
        {
            var inputs = new Dictionary<string, Tensor>();
            inputs.Add(INPUT_NAME, tensor);
            yield return StartCoroutine(worker.StartManualSchedule(inputs));
            //worker.Execute(inputs);
            var output = worker.PeekOutput(OUTPUT_NAME);
            //Debug.Log("Output: " + output);
            //var texture = BarracudaTextureUtils.TensorToRenderTexture(output);


            var results = GetResults(output, classesNames, IMAGE_SIZE, IMAGE_SIZE, MINIMUM_CONFIDENCE, 0.7f);

            var boxes = new List<BoundingBox>();
            for (int i = 0; i < results.Count; i++)
            {
                Debug.Log("result " + i + "; " + results.ElementAt(i).Label + "; " + results.ElementAt(i).Confidence + "; " + results.ElementAt(i).BBox[0] + "; " + results.ElementAt(i).BBox[1] + "; " + results.ElementAt(i).BBox[2] + "; " + results.ElementAt(i).BBox[3]);
                var bb = new BoundingBox();

                boxes.Add(new BoundingBox
                {
                    Dimensions = new BoundingBoxDimensions
                    {
                        X = results.ElementAt(i).BBox[0],//(mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                        Y = results.ElementAt(i).BBox[1],//(mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                        Width = results.ElementAt(i).BBox[2] - results.ElementAt(i).BBox[0],
                        Height = results.ElementAt(i).BBox[3] - results.ElementAt(i).BBox[1],
                    },
                    Confidence = results.ElementAt(i).Confidence,
                    Label = results.ElementAt(i).Label,
                    Used = false
                });
            }
            //            var results = ParseOutputs(output, MINIMUM_CONFIDENCE, params_);

            //var boxes = FilterBoundingBoxes(results, 5, MINIMUM_CONFIDENCE);

            callback(boxes);
        }
    }


    public IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback)
    {
        /*
        var tex = new Texture2D(IMAGE_SIZE, IMAGE_SIZE, TextureFormat.RGBA32, false);
        tex.SetPixels32(picture);
        tex.Apply();
        
        byte[] bytes = tex.EncodeToPNG();
        var dirPath = Application.dataPath + "/../SaveImages/";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        File.WriteAllBytes(dirPath + "Image2" + ".png", bytes);
        */

        
        using (var tensor = TransformInput(picture, IMAGE_SIZE, IMAGE_SIZE))
        //using (var tensor = new Tensor(tex))
        {
            var inputs = new Dictionary<string, Tensor>();
            inputs.Add(INPUT_NAME, tensor);
            yield return StartCoroutine(worker.StartManualSchedule(inputs));
            //worker.Execute(inputs);
            var output = worker.PeekOutput(OUTPUT_NAME);
            //Debug.Log("Output: " + output);
            //var texture = BarracudaTextureUtils.TensorToRenderTexture(output);
            

            var results = GetResults(output, classesNames, IMAGE_SIZE, IMAGE_SIZE, MINIMUM_CONFIDENCE, 0.7f);

            var boxes = new List<BoundingBox>();
            for (int i = 0; i < results.Count; i++)
            {
                Debug.Log("result " + i + "; " + results.ElementAt(i).Label + "; " + results.ElementAt(i).Confidence + "; " + results.ElementAt(i).BBox[0] + "; " + results.ElementAt(i).BBox[1] + "; " + results.ElementAt(i).BBox[2] + "; " + results.ElementAt(i).BBox[3]);
                var bb = new BoundingBox();

                boxes.Add(new BoundingBox
                {
                    Dimensions = new BoundingBoxDimensions
                    {
                        X = results.ElementAt(i).BBox[0],//(mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                        Y = results.ElementAt(i).BBox[1],//(mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                        Width = results.ElementAt(i).BBox[2] - results.ElementAt(i).BBox[0],
                        Height = results.ElementAt(i).BBox[3] - results.ElementAt(i).BBox[1],
                    },
                    Confidence = results.ElementAt(i).Confidence,
                    Label = results.ElementAt(i).Label,
                    Used = false
                });
            }
            //            var results = ParseOutputs(output, MINIMUM_CONFIDENCE, params_);

            //var boxes = FilterBoundingBoxes(results, 5, MINIMUM_CONFIDENCE);
            
            callback(boxes);
        }
    }

    public static void DumpRenderTexture(RenderTexture rt, string pngOutPath)
    {
        var oldRT = RenderTexture.active;

        var tex = new Texture2D(rt.width, rt.height);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(pngOutPath, tex.EncodeToPNG());
        RenderTexture.active = oldRT;
    }

    public static Tensor TransformInput(RenderTexture inputRenderTexture)
    {
        RenderTexture tmpRenderTexture = new RenderTexture(inputRenderTexture.descriptor);

        var tmp = new Tensor(srcTexture: inputRenderTexture, channels: 3);
        tmp.ToRenderTexture(target: tmpRenderTexture, batch: 0, fromChannel: 0, scale: 1 / IMAGE_STD, bias: -IMAGE_MEAN / IMAGE_STD);

        DumpRenderTexture(tmpRenderTexture, Application.dataPath + "/../SaveImages/ImageRttTensor.png");
        /*
        Texture2D m_Texture = null;
        if (m_Texture == null || m_Texture.width != tmpRenderTexture.width || m_Texture.height != tmpRenderTexture.width)
        {
            m_Texture = new Texture2D(tmpRenderTexture.width, tmpRenderTexture.height, TextureFormat.RGBA32, false);
        }
        m_Texture.ReadPixels(new Rect(0, 0, tmpRenderTexture.width, tmpRenderTexture.height), 0, 0, false);
        m_Texture.Apply();

        byte[] bytes = m_Texture.EncodeToPNG();
        var dirPath = Application.dataPath + "/../SaveImages/";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        File.WriteAllBytes(dirPath + "ImageRttTensor" + ".png", bytes);
        */



        return new Tensor(tmpRenderTexture, 3);
    }
    public static Tensor TransformInput(Color32[] pic, int width, int height)
    {
        float[] floatValues = new float[width * height * 3];

        for (int i = 0; i < pic.Length; ++i)
        {
            var color = pic[i];

            floatValues[i * 3 + 0] = (color.r - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 1] = (color.g - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 2] = (color.b - IMAGE_MEAN) / IMAGE_STD;
        }

        return new Tensor(1, height, width, 3, floatValues);
        
    }







    public IReadOnlyList<YoloV5Result> GetResults(Tensor Output, string[] categories, float ImageWidth, float ImageHeight, float scoreThres = 0.5f, float iouThres = 0.5f)
    {

        // Probabilities + Characteristics
        int characteristics = categories.Length + 5;

        // Needed info
        float modelWidth = 640.0F;
        float modelHeight = 640.0F;
        float xGain = modelWidth / ImageWidth;
        float yGain = modelHeight / ImageHeight;
        //var tr = Output.Reshape(new TensorShape(1, 25500, 12));
        float[] results = Output.ToReadOnlyArray();
        
        List<float[]> postProcessedResults = new List<float[]>();

        int iMax = 25500;
        for (int i = 0; i < iMax; i++)
        {
            // Filter some boxes
            var objConf = results[i + 4 * iMax];
            if (objConf <= scoreThres) continue;

            var x1 = results[i] - results[i + 2 * iMax] / xGain; //top left x
            var y1 = results[i + iMax] - results[i + 3 * iMax] / yGain; //top left y
            var x2 = results[i] + results[i + 2 * iMax] / xGain; //bottom right x
            var y2 = results[i + iMax] + results[i + 3 * iMax] / yGain; //bottom right y

            // Get real class scores
            List<float> scores = new List<float>();
            for(int j = 0; j<categories.Length; j++)
            {
                scores.Add(objConf * results[i + (5 + j) * iMax]);
            }
            
            //var classProbs = predCell.Skip(5).Take(categories.Length).ToList();
            //var scores = classProbs.Select(p => p * objConf).ToList();

            // Get best class and index
            float maxConf = scores.Max();
            float maxClass = scores.ToList().IndexOf(maxConf);

            postProcessedResults.Add(new[] { x1, y1, x2, y2, maxConf, maxClass });
        }

        /*
            // For every cell of the image, format for NMS
            //for (int i = 0; i < 25200; i++)
        for (int i = 0; i < 25500; i++)
        {
            // Get offset in float array
            int offset = characteristics * i;

            // Get a prediction cell
            var predCell = results.Skip(offset).Take(characteristics).ToList();

            // Filter some boxes
            var objConf = predCell[4];
            if (objConf <= scoreThres) continue;

            // Get corners in original shape
            var x1 = (predCell[0] - predCell[2] / 2) / xGain; //top left x
            var y1 = (predCell[1] - predCell[3] / 2) / yGain; //top left y
            var x2 = (predCell[0] + predCell[2] / 2) / xGain; //bottom right x
            var y2 = (predCell[1] + predCell[3] / 2) / yGain; //bottom right y

            // Get real class scores
            var classProbs = predCell.Skip(5).Take(categories.Length).ToList();
            var scores = classProbs.Select(p => p * objConf).ToList();

            // Get best class and index
            float maxConf = scores.Max();
            float maxClass = scores.ToList().IndexOf(maxConf);

            postProcessedResults.Add(new[] { x1, y1, x2, y2, maxConf, maxClass });
        }
        */
        var resultsNMS = ApplyNMS(postProcessedResults, categories, iouThres);

        return resultsNMS;
    }

    private List<YoloV5Result> ApplyNMS(List<float[]> postProcessedResults, string[] categories, float iouThres = 0.5f)
    {
        postProcessedResults = postProcessedResults.OrderByDescending(x => x[4]).ToList(); // sort by confidence
        List<YoloV5Result> resultsNms = new List<YoloV5Result>();

        int f = 0;
        while (f < postProcessedResults.Count)
        {
            var res = postProcessedResults[f];
            if (res == null)
            {
                f++;
                continue;
            }

            var conf = res[4];
            string label = categories[(int)res[5]];

            resultsNms.Add(new YoloV5Result(res.Take(4).ToArray(), label, conf));
            postProcessedResults[f] = null;

            var iou = postProcessedResults.Select(bbox => bbox == null ? float.NaN : BoxIoU(res, bbox)).ToList();
            for (int i = 0; i < iou.Count; i++)
            {
                if (float.IsNaN(iou[i])) continue;
                if (iou[i] > iouThres)
                {
                    postProcessedResults[i] = null;
                }
            }
            f++;
        }

        return resultsNms;
    }

    /// <summary>
    /// Return intersection-over-union (Jaccard index) of boxes.
    /// <para>Both sets of boxes are expected to be in (x1, y1, x2, y2) format.</para>
    /// </summary>
    private static float BoxIoU(float[] boxes1, float[] boxes2)
    {
        static float box_area(float[] box)
        {
            return (box[2] - box[0]) * (box[3] - box[1]);
        }

        var area1 = box_area(boxes1);
        var area2 = box_area(boxes2);

        Debug.Assert(area1 >= 0);
        Debug.Assert(area2 >= 0);

        var dx = Math.Max(0, Math.Min(boxes1[2], boxes2[2]) - Math.Max(boxes1[0], boxes2[0]));
        var dy = Math.Max(0, Math.Min(boxes1[3], boxes2[3]) - Math.Max(boxes1[1], boxes2[1]));
        var inter = dx * dy;

        return inter / (area1 + area2 - inter);
    }















    private IList<BoundingBox> ParseOutputs(Tensor yoloModelOutput, float threshold, Dictionary<string, int> parameters)
    {
        var boxes = new List<BoundingBox>();

        for (int cy = 0; cy < parameters["COL_COUNT"]; cy++)
        {
            for (int cx = 0; cx < parameters["ROW_COUNT"]; cx++)
            {
                for (int box = 0; box < BOXES_PER_CELL; box++)
                {
                    var channel = (box * (CLASS_COUNT + BOX_INFO_FEATURE_COUNT));
                    var bbd = ExtractBoundingBoxDimensions(yoloModelOutput, cx, cy, channel);
                    float confidence = GetConfidence(yoloModelOutput, cx, cy, channel);

                    if (confidence < threshold)
                    {
                        continue;
                    }

                    float[] predictedClasses = ExtractClasses(yoloModelOutput, cx, cy, channel);
                    var (topResultIndex, topResultScore) = GetTopResult(predictedClasses);
                    var topScore = topResultScore * confidence;
                    Debug.Log("DEBUG: results: " + topResultIndex.ToString());

                    if (topScore < threshold)
                    {
                        continue;
                    }

                    var mappedBoundingBox = MapBoundingBoxToCell(cx, cy, box, bbd, parameters);
                    boxes.Add(new BoundingBox
                    {
                        Dimensions = new BoundingBoxDimensions
                        {
                            X = (mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                            Y = (mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                            Width = mappedBoundingBox.Width,
                            Height = mappedBoundingBox.Height,
                        },
                        Confidence = topScore,
                        Label = labels[topResultIndex],
                        Used = false
                    });
                }
            }
        }

        return boxes;
    }


    private float Sigmoid(float value)
    {
        var k = (float)Math.Exp(value);

        return k / (1.0f + k);
    }


    private float[] Softmax(float[] values)
    {
        var maxVal = values.Max();
        var exp = values.Select(v => Math.Exp(v - maxVal));
        var sumExp = exp.Sum();

        return exp.Select(v => (float)(v / sumExp)).ToArray();
    }


    private BoundingBoxDimensions ExtractBoundingBoxDimensions(Tensor modelOutput, int x, int y, int channel)
    {
        return new BoundingBoxDimensions
        {
            X = modelOutput[0, x, y, channel],
            Y = modelOutput[0, x, y, channel + 1],
            Width = modelOutput[0, x, y, channel + 2],
            Height = modelOutput[0, x, y, channel + 3]
        };
    }


    private float GetConfidence(Tensor modelOutput, int x, int y, int channel)
    {
        //Debug.Log("ModelOutput " + modelOutput);
        return Sigmoid(modelOutput[0, x, y, channel + 4]);
    }


    private CellDimensions MapBoundingBoxToCell(int x, int y, int box, BoundingBoxDimensions boxDimensions, Dictionary<string, int> parameters)
    {
        return new CellDimensions
        {
            X = ((float)y + Sigmoid(boxDimensions.X)) * parameters["CELL_WIDTH"],
            Y = ((float)x + Sigmoid(boxDimensions.Y)) * parameters["CELL_HEIGHT"],
            Width = (float)Math.Exp(boxDimensions.Width) * anchors[6 + box * 2],
            Height = (float)Math.Exp(boxDimensions.Height) * anchors[6 + box * 2 + 1],
        };
    }


    public float[] ExtractClasses(Tensor modelOutput, int x, int y, int channel)
    {
        float[] predictedClasses = new float[CLASS_COUNT];
        int predictedClassOffset = channel + BOX_INFO_FEATURE_COUNT;

        for (int predictedClass = 0; predictedClass < CLASS_COUNT; predictedClass++)
        {
            predictedClasses[predictedClass] = modelOutput[0, x, y, predictedClass + predictedClassOffset];
        }

        return Softmax(predictedClasses);
    }


    private ValueTuple<int, float> GetTopResult(float[] predictedClasses)
    {
        return predictedClasses
            .Select((predictedClass, index) => (Index: index, Value: predictedClass))
            .OrderByDescending(result => result.Value)
            .First();
    }


    private float IntersectionOverUnion(Rect boundingBoxA, Rect boundingBoxB)
    {
        var areaA = boundingBoxA.width * boundingBoxA.height;

        if (areaA <= 0)
            return 0;

        var areaB = boundingBoxB.width * boundingBoxB.height;

        if (areaB <= 0)
            return 0;

        var minX = Math.Max(boundingBoxA.xMin, boundingBoxB.xMin);
        var minY = Math.Max(boundingBoxA.yMin, boundingBoxB.yMin);
        var maxX = Math.Min(boundingBoxA.xMax, boundingBoxB.xMax);
        var maxY = Math.Min(boundingBoxA.yMax, boundingBoxB.yMax);

        var intersectionArea = Math.Max(maxY - minY, 0) * Math.Max(maxX - minX, 0);

        return intersectionArea / (areaA + areaB - intersectionArea);
    }


    private IList<BoundingBox> FilterBoundingBoxes(IList<BoundingBox> boxes, int limit, float threshold)
    {
        var activeCount = boxes.Count;
        var isActiveBoxes = new bool[boxes.Count];

        for (int i = 0; i < isActiveBoxes.Length; i++)
        {
            isActiveBoxes[i] = true;
        }

        var sortedBoxes = boxes.Select((b, i) => new { Box = b, Index = i })
                .OrderByDescending(b => b.Box.Confidence)
                .ToList();

        var results = new List<BoundingBox>();

        for (int i = 0; i < boxes.Count; i++)
        {
            if (isActiveBoxes[i])
            {
                var boxA = sortedBoxes[i].Box;
                results.Add(boxA);

                if (results.Count >= limit)
                    break;

                for (var j = i + 1; j < boxes.Count; j++)
                {
                    if (isActiveBoxes[j])
                    {
                        var boxB = sortedBoxes[j].Box;

                        if (IntersectionOverUnion(boxA.Rect, boxB.Rect) > threshold)
                        {
                            isActiveBoxes[j] = false;
                            activeCount--;

                            if (activeCount <= 0)
                                break;
                        }
                    }
                }

                if (activeCount <= 0)
                    break;
            }
        }
        return results;
    }
}
