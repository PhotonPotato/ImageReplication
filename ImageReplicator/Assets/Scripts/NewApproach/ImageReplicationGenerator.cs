using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// - Render texture
/// - DownScaled images
/// - Difference of images
/// - Score
/// - Generate a random object
/// - Mutate objects
/// </summary>

public class ImageReplicationGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ImageRepObject
    {
        public int spriteIndex;

        public Vector3 pos;
        public float zRot;
        public float scale;

        public Color32 color;

        public float scoreWithObj;

        public ImageRepObject()
        {

        }
    }

    [Header("Refs")]
    public RenderTexture rt;
    public Camera CaptureCam;

    public RawImage DebugImgOrg;
    public RawImage DebugImgNew;
    public RawImage DebugImgDiff;

    public Texture2D originalReferenceTexture;
    public Texture2D differenceTestTexture;

    public Texture2D currentSetReplicationTexture;
    public Texture2D newObjTestingTexture;

    public Image sampleImage;

    //New SHIT
    public Transform imageRepObjectBinParent;
    public Transform originalDisplayImageObject;

    public Texture2D fullResolutionRender;

    public GameObject imageRepObjectPrefab;
    public Sprite[] imageRepObjectSprites;

    public GameObject CurrentRepObject;

    public List<GameObject> AddedImageRepObjects;
    public List<ImageRepObject> repObjectStack;

    [Header("Settings")]
    public int renderResolutionX = 100;
    public int renderResolutionY = 100;

    public Gradient colorDifferenceGradient;

    public int objectsPerFreshSpawn = 50;
    public int objetsPerMutation = 40;

    [Tooltip("Controls how objects are scaled based on distance to the camera")]
    public AnimationCurve scaleVsDistanceFalloff;

    [Tooltip("How the color sample kernel size is chosen based on the scale of the given object")]
    public AnimationCurve scaleVsColorSampleKernelSize;

    [Tooltip("Used, for example, to bring the objects spawning closer as time goes on")]
    public float amountToMoveZBoundsPerFreshIteration = .5f;

    [Tooltip("Decides whether to move both the near (min) or far (max) or not")]
    public bool onlyMoveFarBound = true;

    public float amountToReduceMaxScalePerFresh = .05f;

    //Force small objects closer?
    //Pro: makes them more visible
    //Con: will get scaled even smaller.
    //     will actually be bigger than originally meant (DoF)

    [Header("Spawn Restrictions")]
    public Vector3 minSpawnPos;
    public Vector3 maxSpawnPos;

    public float maxSpawnScale = 8f;
    public float minSpawnScale = .5f;

    public float minSpawnZRotation = 0f;
    public float maxSpawnZRotation = 360f;


    [Header("Trackers")]
    public float score = 0;


    [Tooltip("Counts the current indecie if the new object stack")]
    private int g = 0;

    [Tooltip("Counts down the number of recurring mutation layers that are left")]
    private int n = 2;

    [Tooltip("Stores the index of the current best scored object")]
    private int bestScoreStackIndex = 0;
    private float bestScoreInStack = 100;

    [Tooltip("Stores the overall score of the last set in stone iteration")]
    public float lastTotalScore;

    public void Start()
    {
        originalDisplayImageObject.gameObject.SetActive(true);

        repObjectStack = new List<ImageRepObject>();
        AddedImageRepObjects = new List<GameObject>();

        InitTextures();

        CaptureToRTToTexture(ref newObjTestingTexture);

        DebugImgNew.texture = newObjTestingTexture;

        //Save a snapshot of the original texture that we captured so that we can reference it later
        CaptureToRTToTexture(ref originalReferenceTexture);

        currentSetReplicationTexture = GenerateBaseBlackTexture();

        CalculateDifferenceOfTextures(originalReferenceTexture, newObjTestingTexture, true, ref differenceTestTexture, ref score);

        DebugImgOrg.texture = originalReferenceTexture;

        DebugImgDiff.texture = differenceTestTexture;

        lastTotalScore = 100;

        originalDisplayImageObject.gameObject.SetActive(false);

        AddNewRepObjects(objectsPerFreshSpawn);
    }

    public void Update()
    {
        //sampleImage.transform.position = Input.mousePosition;
        //sampleImage.color = GetAverageKernelColor(ref fullResolutionRender, Input.mousePosition, 10);

        if (g < repObjectStack.Count)
        {
            CurrentRepObject.GetComponent<SpriteRenderer>().sprite = imageRepObjectSprites[repObjectStack[g].spriteIndex];
            CurrentRepObject.GetComponent<SpriteRenderer>().color = repObjectStack[g].color;

            CurrentRepObject.transform.position = repObjectStack[g].pos;
            CurrentRepObject.transform.rotation = Quaternion.Euler(0, 0, repObjectStack[g].zRot);
            CurrentRepObject.transform.localScale = Vector3.one * repObjectStack[g].scale;

            //Save a snapshot of the original texture that we captured so that we can reference it later
            CaptureToRTToTexture(ref newObjTestingTexture);

            CalculateDifferenceOfTextures(newObjTestingTexture, originalReferenceTexture, true, ref differenceTestTexture, ref repObjectStack[g].scoreWithObj);
            DebugImgDiff.texture = differenceTestTexture;

            if (repObjectStack[g].scoreWithObj <= bestScoreInStack)
            {
                bestScoreStackIndex = g;
                bestScoreInStack = repObjectStack[g].scoreWithObj;
            }

            g++;
        }
        else
        {
            // Get the best object and actually spawn um
            // We are looking for the lowest score and then mutating them
            Debug.Log("N: " + n);
            Debug.Log("G: " + g);
            Debug.Log("BEST: " + bestScoreStackIndex + " Score: " + bestScoreInStack);

            AddNewMutatiedObjects(repObjectStack[bestScoreStackIndex], 20);

            //Reset the bests
            //bestScore = 1;
            //bestScoreIndex = -1;
        }
    }


    public void AddNewRepObjects(int numObjects)
    {
        if (CurrentRepObject == null) CurrentRepObject = Instantiate(imageRepObjectPrefab, imageRepObjectBinParent);

        repObjectStack.Clear();

        //Add a bunch of fresh rep objects
        for (int i = 0; i < numObjects; i++)
        {
            repObjectStack.Add(GenerateFreshObjectData());
        }

        //Reset the best stack trackers
        bestScoreInStack = 1;
        bestScoreStackIndex = -1;

        //Reset the current index and mutation layer
        n = 3;
        g = 0;
    }

    public void AddNewMutatiedObjects(ImageRepObject obj, int numObjects)
    {
        numObjects = (int) (numObjects * (float) n / 3);

        Debug.Log("Mutating " + numObjects + " times");

        n--;

        if (n == 0)
        {
            //Only keep going if the current score is an improvement
            if (bestScoreInStack < lastTotalScore)
            {
                Debug.Log("CHOSEN INDEX " + g + " TO SPAWN.");
                //Actually spawn the best object then
                Transform transform = Instantiate(imageRepObjectPrefab, imageRepObjectBinParent).transform;

                transform.GetComponent<SpriteRenderer>().sprite = imageRepObjectSprites[repObjectStack[bestScoreStackIndex].spriteIndex];
                transform.GetComponent<SpriteRenderer>().color = repObjectStack[bestScoreStackIndex].color;

                transform.position = repObjectStack[bestScoreStackIndex].pos;
                transform.rotation = Quaternion.Euler(0, 0, repObjectStack[bestScoreStackIndex].zRot);
                transform.transform.localScale = Vector3.one * repObjectStack[bestScoreStackIndex].scale;

                lastTotalScore = bestScoreInStack;

                AddedImageRepObjects.Add(transform.gameObject);
            }
            else
            {
                Debug.Log("Discarded new peice");
            }

            Debug.Log("Back to fresh objects");

            maxSpawnPos.z += amountToMoveZBoundsPerFreshIteration;
            maxSpawnScale -= amountToReduceMaxScalePerFresh;

            AddNewRepObjects(objectsPerFreshSpawn);
            return;
        }

        repObjectStack.Clear();

        //Add the old obj
        repObjectStack.Add(obj);
        bestScoreInStack = 100;
        bestScoreStackIndex = 0;

        // Add a bunch of mutated rep objects
        for (int i = 0; i < numObjects; i++)
        {
            repObjectStack.Add(GenerateMutatedObjectFromExisting(obj));
        }

        g = 0;
    }

    /// <summary>
    /// Literally just initialized the used texture vars for their size and filter.
    /// Also gets a full size rendered texture of the image for color sampleing use.
    /// </summary>
    public void InitTextures()
    {
        originalReferenceTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        originalReferenceTexture.filterMode = FilterMode.Point;

        newObjTestingTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        newObjTestingTexture.filterMode = FilterMode.Point;

        differenceTestTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        differenceTestTexture.filterMode = FilterMode.Point;
        
        currentSetReplicationTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        currentSetReplicationTexture.filterMode = FilterMode.Point;

        //Set up a full resolution render so that we can sample color data off of it later
        fullResolutionRender = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        fullResolutionRender.filterMode = FilterMode.Bilinear;

        RenderTexture renderTexture = new RenderTexture(1920, 1080, 31);
        RenderTexture.active = renderTexture;

        CaptureCam.targetTexture = renderTexture;
        CaptureCam.Render();

        fullResolutionRender.ReadPixels(new(0, 0, 1920, 1080), 0, 0);
        fullResolutionRender.Apply();

        RenderTexture.active = null;
        CaptureCam.targetTexture = null;
        DestroyImmediate(renderTexture);
        Resources.UnloadUnusedAssets();
    }

    /// <summary>
    /// Will take a screenshot essentially, however, this is hard set to use the predefined RT render texture.
    /// </summary>
    public void CaptureToRTToTexture(ref Texture2D outTexture)
    {
        CaptureCam.targetTexture = rt;
        CaptureCam.Render();

        RenderTexture.active = rt;

        outTexture.ReadPixels(new(0, 0, rt.width, rt.height), 0, 0);
        outTexture.Apply();

        RenderTexture.active = null;
        CaptureCam.targetTexture = null;
    }

    /// <summary>
    /// Returns a texture the size of the render texture that is filled with black pixels.
    /// </summary>
    public Texture2D GenerateBaseBlackTexture()
    {
        Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

        Color32[] colors = texture.GetPixels32();

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.black;
        }

        texture.SetPixels32(colors);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// Uses the "Redmean" approach to difference of colors in order to output a value 
    /// </summary>
    /// <param name="originalPixel"> The first pixel to compare.</param>
    /// <param name="comparePixel"> The second pixel to compar to.</param>
    /// <returns></returns>
    public float ComparePixel(Color32 originalPixel, Color32 comparePixel)
    {
        //"Redmean" approach to difference of colors
        float r = .5f * (originalPixel.r + comparePixel.r);
        float deltaR = comparePixel.r - originalPixel.r;
        float deltaG = comparePixel.g - originalPixel.g;
        float deltaB = comparePixel.b - originalPixel.b;

        //Modify c by multiplier?

        return Mathf.Sqrt((2 + (r / 256)) * Mathf.Pow(deltaR, 2) + (4 * Mathf.Pow(deltaG, 2)) + ((2 + ((255 - r) / 256)) * Mathf.Pow(deltaB, 2))); ;
    }

    public void CalculateDifferenceOfTextures(Texture2D texture1, Texture2D texture2, bool saveDifferenceToTex, ref Texture2D textureOut, ref float refScore)
    {
        //The first texture will double as where I'll store the difference
        Color32[] colors1 = texture1.GetPixels32();
        Color32[] colors2 = texture2.GetPixels32();

        float overallScore = 0;
        float tempComparedScore = -1;

        // Loop through the each pixel of the texture and compare them
        for (int i = 0; i < rt.width * rt.height; i++)
        {
            tempComparedScore = ComparePixel(colors1[i], colors2[i]) / 700;

            if (saveDifferenceToTex) colors1[i] = colorDifferenceGradient.Evaluate(tempComparedScore);

            overallScore += tempComparedScore;
        }

        if (saveDifferenceToTex)
        {
            textureOut.SetPixels32(colors1);
            textureOut.Apply();

        }

        //Avg it to the score per pixel
        score = overallScore / (rt.width * rt.height);
        refScore = overallScore / (rt.width * rt.height);
    }

    /// <summary>
    /// Returns the average color from a specific point in a texture given a specific kernel size.
    /// (Uses default kernel weighting, nothing special :( lol)
    /// </summary>
    private Color GetAverageKernelColor(ref Texture2D tex, Vector2 kernelPos, int kernelSize)
    {
        Vector2Int kernelUL = new Vector2Int((int)kernelPos.x + kernelSize, (int)kernelPos.y + kernelSize);
        //Vector2Int kernelBR = new Vector2Int((int)kernelPos.x - kernelSize, (int)kernelPos.y - kernelSize);
        float r = 0;
        float g = 0;
        float b = 0;
        float a = 0;

        int numPixelsSampled = 0;

        for (int i = 0; i < kernelSize * 2; i++)
        {
            for (int j = 0; j < kernelSize * 2; j++)
            {
                Vector2Int samplePixel = new Vector2Int(kernelUL.x + i, kernelUL.y - j);

                numPixelsSampled += IsInTexture(1920, 1080, samplePixel) ? 1 : 0;

                Color pixelColor = tex.GetPixel(kernelUL.x + i, kernelUL.y - j);

                //Sum the values
                r += pixelColor.r;
                g += pixelColor.g;
                b += pixelColor.b;
                a += pixelColor.a;
            }
        }

        //Average the values
        r /= numPixelsSampled;
        g /= numPixelsSampled;
        b /= numPixelsSampled;
        a /= numPixelsSampled;

        return new Color(r, g, b, a);
    }

    /// <summary>
    /// Returns whether a pixel coordinate lies within a certain texture.
    /// </summary>
    /// <param name="width"> The width of the texture. </param>
    /// <param name="height"> The height of the texture. </param>
    /// <param name="point"> The actual sample point in the texture. </param>
    /// <returns> Returns true if the pixel exists in the specified texture and false if not. </returns>
    private bool IsInTexture(int width, int height, Vector2Int point)
    {
        if (Mathf.Abs(point.x) > width) return false;

        return Mathf.Abs(point.y) > height ? false : true;
    }

    /// <summary>
    /// Returns an ImageRepObject that hold the data of a new, fresh object.
    /// This includes the posistion, sprite [index], color, rotation, errytang.
    /// </summary>
    public ImageRepObject GenerateFreshObjectData()
    {
        ImageRepObject repObject = new ImageRepObject();

        repObject.spriteIndex = Random.Range(0, imageRepObjectSprites.Length);

        //Generate a spawn point posistion
        repObject.pos = new Vector3(Random.Range(minSpawnPos.x, maxSpawnPos.x),
                                    Random.Range(minSpawnPos.y, maxSpawnPos.y),
                                    Random.Range(minSpawnPos.z, maxSpawnPos.z));
        //Generate a spawn scale
        repObject.scale = Random.Range(minSpawnScale, maxSpawnScale) * scaleVsDistanceFalloff.Evaluate(Mathf.InverseLerp(minSpawnPos.z, maxSpawnPos.z, repObject.pos.z));

        //Generate a spawn roation
        repObject.zRot = Random.Range(minSpawnZRotation, maxSpawnZRotation);

        //Get a slightly mutated color from whats under that actual pixel
        repObject.color = GetAverageKernelColor(ref fullResolutionRender,
                                                CaptureCam.WorldToScreenPoint(repObject.pos),
                                                //Force it to be above a zero kernel size by adding one
                                                3 + Mathf.RoundToInt(4 * scaleVsColorSampleKernelSize.Evaluate(Mathf.InverseLerp(minSpawnScale, maxSpawnScale, repObject.scale))));

        //MutateColor(ref repObject.color, 0);

        return repObject;
    }

    /// <summary>
    /// Changes a referenced Color to be slightly mutated by a <param name="mutationAmount">
    /// </summary>
    public void MutateColor(ref Color32 color, float mutationAmount = 8)
    {
        color.r += (byte) Random.Range(mutationAmount * -1, mutationAmount);
        color.g += (byte) Random.Range(mutationAmount * -1, mutationAmount);
        color.b += (byte) Random.Range(mutationAmount * -1, mutationAmount);
        color.a += (byte) Random.Range(mutationAmount * -1, mutationAmount);
    }

    /// <summary>
    /// Changes a referenced Vector3 to be slightly mutated by a <param name="mutationAmount">
    /// </summary>
    public void MutatePosition(ref Vector3 pos, float mutationAmount = .3f)
    {
        pos.x += Random.Range(mutationAmount * -1, mutationAmount);
        pos.y += Random.Range(mutationAmount * -1, mutationAmount);
        pos.z += Random.Range(mutationAmount * -1, mutationAmount);
    }

    /// <summary>
    /// Adds or subtracts a bit from a given float.
    /// This is meant for mutating a rotation or scale of an object.
    /// </summary>
    public float MutateFloat(float input, float mutationAmount = .2f) => input + Random.Range(mutationAmount * -1, mutationAmount);

    public ImageRepObject GenerateMutatedObjectFromExisting(ImageRepObject rootObject)
    {
        ImageRepObject repObject = rootObject;

        //Run through and apply a bunch of different mutations to the original object

        MutateColor(ref repObject.color, .4f);

        MutatePosition(ref repObject.pos, .4f);

        repObject.scale = MutateFloat(repObject.scale, .6f);

        repObject.zRot = MutateFloat(repObject.zRot, 360);

        return repObject;
    }
}
