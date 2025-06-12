using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MainGenerator : MonoBehaviour
{
    public Texture2D originalPicture;
    public SpriteRenderer OriginalPictureRenderer;

    public Texture2D downScaledOriginalPicture;
    public SpriteRenderer downscaledPictureRenderer;

    public GameObject OriginalObjectBin;

    [Space]
    public GameObject ReplicationObjectBin;

    public GameObject[] replicationObjects;
    public float maxSizeVariation = 1;
    public float minSizeVariation = 8;
    //For some reason this really likes to be low.
    public float colorMutationStrength = .05f;

    public float transformMutationStrength = .05f;

    [Space]
    public Camera mCamera;
    public int mWidth;
    public int mHeight;

    public Texture2D currentReplicationTexture;
    public Texture2D newIterationOfReplicationTex;

    public int targetScalingX = 100;

    //Scoring and replication
    //Empty scene score
    float currentScore = Mathf.Infinity;
    public float numberBaseIterationObjects = 10;
    public float numSpawnIterations;
    public int numRecursions = 10;

    public List<ScoredObject> baseIterationReplicationObjects;

    [Space]
    public int colorSampleKernelSize = 5;
    public Vector3 boundUL;
    public Vector3 boundBR;

    public int loops = 0;

    public void Start()
    {
        currentScore = Mathf.Infinity;
        if (originalPicture == null) return;
        //OriginalPictureRenderer.sprite = Sprite.Create(originalPicture, new Rect(0.0f, 0.0f, originalPicture.width, originalPicture.height), new Vector2(0.5f, 0.5f), 100.0f);


        //Save a screenshot of the image.
        originalPicture = RTImage();
        downScaledOriginalPicture = Resize(originalPicture, targetScalingX, 0, true);

        OriginalPictureRenderer.sprite = Sprite.Create(originalPicture, new Rect(0.0f, 0.0f, originalPicture.width, originalPicture.height), new Vector2(0.5f, 0.5f), 100.0f);
        downscaledPictureRenderer.sprite = Sprite.Create(downScaledOriginalPicture, new Rect(0.0f, 0.0f, downScaledOriginalPicture.width, downScaledOriginalPicture.height), new Vector2(0.5f, 0.5f), 100.0f);

        //Hide all of the scene objects
        OriginalObjectBin.SetActive(false);

        //Set up teh first replication texture.
        currentReplicationTexture = Resize(RTImage(), targetScalingX, 0, true);
        //currentScore = scoreDiffenrenceOfTextures(currentReplicationTexture, downScaledOriginalPicture);

        OriginalObjectBin.SetActive(false);

        baseIterationReplicationObjects = new List<ScoredObject>();

        /*for (int i = 0; i < numberBaseIterationObjects; i++)
        {
            ScoredObject curObj = new ScoredObject(getNewReplicationObject());

            curObj.score = scoreDiffenrenceOfTextures(currentReplicationTexture, Resize(RTImage(), targetScalingX, 0, true));
            //print(i + " | " + curObj.score);

            baseIterationReplicationObjects.Add(curObj);

            curObj.Object.SetActive(false);
        }*/

      
    }

    private void Update()
    {
        loops++;
        print("loop " + loops);
        if (loops > 400 || loops == 1) return;

        ScoredObject bestObject = new ScoredObject(null, Mathf.Infinity);

        while (currentScore <= bestObject.score)
        {

            for (int i = 0; i < numberBaseIterationObjects; i++)
            {
                ScoredObject curObj = new ScoredObject(getNewReplicationObject(null, 0, true));
                Texture2D screenImage = RTImage();
                Texture2D result = Resize(screenImage, targetScalingX, 0, true);

                curObj.score = scoreDiffenrenceOfTextures(downScaledOriginalPicture, result);

                DestroyImmediate(result, true);
                DestroyImmediate(screenImage, true);

                if (curObj.score < bestObject.score) bestObject = curObj;
            }


            print("Best score " + bestObject.score);
            ScoredObject newObj = getMutatedFromObject(bestObject, numRecursions);
            print("Mutated score " + newObj.score);

            if (currentScore < newObj.score && currentScore < bestObject.score)
            {
                continue;
                /*
                DestroyImmediate(newObj.Object);
                DestroyImmediate(bestObject.Object);
                print("Current score better : " + currentScore);
                //return;*/
            }
            else
            {
                
            }

            print(newObj.Object == null || bestObject.Object == null);
            Instantiate(newObj.score < bestObject.score ? newObj.Object : bestObject.Object);

            if (newObj.score < bestObject.score)
            {
                currentScore = newObj.score;

                baseIterationReplicationObjects.Add(newObj);

                newObj.Object.transform.SetParent(ReplicationObjectBin.transform);
            }
            else
            {
                currentScore = bestObject.score;
                //DestroyImmediate(newObj.Object, true);

                baseIterationReplicationObjects.Add(bestObject);

                bestObject.Object.transform.SetParent(ReplicationObjectBin.transform);
            }

            //DestroyImmediate(newObj.Object, true);
            //DestroyImmediate(bestObject.Object, true);
            Resources.UnloadUnusedAssets();
        }
        print("Current score " + currentScore);

    }

    Texture2D Resize(Texture2D texture2D, int targetX, int targetY, bool saveAspectRatio = false)
    {
        //Preserve aspect ratio, scale based on target x-value
        if (saveAspectRatio)
        {
            float ratio = (float) texture2D.height / (float) texture2D.width;
            
            targetY = Mathf.RoundToInt(targetX * ratio);
        }

        RenderTexture rt = new RenderTexture(targetX, targetY, 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);
        rt = null;
        DestroyImmediate(rt, true);
        Texture2D result = new Texture2D(targetX, targetY);
        result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
        result.Apply();

        Resources.UnloadUnusedAssets();
        return result;
    }

    public float scoreDiffenrenceOfTextures(Texture2D originalTexture, Texture2D newTexture)
    {
        int width = newTexture.width;
        int height = newTexture.height;

        float score = 0;

        //If the textures have different sizes, then set the original to be same as the new.
        if (originalTexture.width != width || originalPicture.height != height)
        {
            originalTexture = Resize(originalTexture, width, height);
        }


        //Iterate through and just add up the scores
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                score += comparePixel(originalTexture.GetPixel(i, j), newTexture.GetPixel(i, j));
            }
        }

        DestroyImmediate(originalTexture, true);

        return score;
    }

    public float comparePixel(Color originalPixel, Color comparePixel)
    {
        //"Redmean" approach to difference of colors
        float r = .5f * (originalPixel.r + comparePixel.r);
        float deltaR = comparePixel.r - originalPixel.r;
        float deltaG = comparePixel.g - originalPixel.g;
        float deltaB = comparePixel.b - originalPixel.b;

        

        //Modify c by multiplier?

        return Mathf.Sqrt((2 + (r / 256)) * Mathf.Pow(deltaR, 2) + (4 * Mathf.Pow(deltaG, 2)) + ((2 + ((255 - r) / 256)) * Mathf.Pow(deltaB, 2))); ;
    }

    private Texture2D RTImage()
    {
        Rect rect = new Rect(0, 0, mWidth, mHeight);
        RenderTexture renderTexture = new RenderTexture(mWidth, mHeight, 24);
        Texture2D screenShot = new Texture2D(mWidth, mHeight, TextureFormat.RGBA32, false);

        mCamera.targetTexture = renderTexture;
        mCamera.Render();

        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(rect, 0, 0);

        mCamera.targetTexture = null;
        RenderTexture.active = null;

        renderTexture = null;
        DestroyImmediate(renderTexture, true);
        screenShot.Apply();

        Resources.UnloadUnusedAssets();
        return screenShot;
    }

    private GameObject getNewReplicationObject(GameObject inputObj = null, int n = 0, bool instantiateObj = true)
    {
        if (inputObj == null)
        {
            //Create a random objefct with a random z rotation
            GameObject obj = instantiateObj ? Instantiate(replicationObjects[Random.Range(0, replicationObjects.Length)], GenerateRandomScreenPos(), Quaternion.Euler(0, 0, Random.Range(0.0f, 360.0f))) : new GameObject();

            //Get the correct color from an average of the pixels below
            Vector2 objectScreenPos = mCamera.WorldToScreenPoint(obj.transform.position);
            Color objectColor = getAverageKernelColor(originalPicture, objectScreenPos, colorSampleKernelSize);

            //Mutate?
            //TH: Stupidly long line, CLEAN it later...
            objectColor += new Color(Random.Range(-1 * colorMutationStrength, colorMutationStrength), Random.Range(-1 * colorMutationStrength, colorMutationStrength), Random.Range(-1 * colorMutationStrength, colorMutationStrength), 0);

            obj.GetComponent<SpriteRenderer>().color = objectColor;

            //Choose random size variation
            float size = Random.Range(minSizeVariation, maxSizeVariation);
            //CAN ADD Z SCALE FOR 3D objects in futute
            obj.transform.localScale = new Vector3(size, size, 0);
            //Destroy(obj);
            return obj;
        }
        else
        {
            float multFactor = Mathf.Pow(.9f, n) * transformMutationStrength;
            float colorMult = Mathf.Pow(.9f, n) * colorMutationStrength;
            //Mutate all of the diffferent properties
            Color objectColor = inputObj.GetComponent<SpriteRenderer>().color;
            objectColor.r += Random.Range(-1 * colorMult, colorMult);
            objectColor.g += Random.Range(-1 * colorMult, colorMult);
            objectColor.b += Random.Range(-1 * colorMult, colorMult);

            Vector3 objectPositionOffset = Vector3.zero;
            objectPositionOffset.x = Random.Range(-1 * multFactor, multFactor);
            objectPositionOffset.y = Random.Range(-1 * multFactor, multFactor);
            objectPositionOffset.z = Random.Range(-1 * multFactor * 2, multFactor * 2);

            float randScale = Random.Range(-1 * multFactor, multFactor); ;
            Vector3 scaleOffset = new Vector3(randScale, randScale, 0);
           /* scaleOffset.x = randScale;
            scaleOffset.y = Random.Range(-1 * multFactor, multFactor);
            scaleOffset.z = Random.Range(-1 * multFactor, multFactor);*/

            float rotationOffset = Random.Range(-1 * multFactor, multFactor);

            inputObj.GetComponent<SpriteRenderer>().color = objectColor;
            inputObj.transform.position += objectPositionOffset;
            inputObj.transform.localScale += scaleOffset;
            inputObj.transform.eulerAngles += new Vector3(0, 0, rotationOffset);

            //Destroy(inputObj);
            return inputObj;
        }
    }

    private Vector3 GenerateRandomScreenPos()
    {
        float x = Random.Range(boundUL.x, boundBR.x);
        float y = Random.Range(boundUL.y, boundBR.y);
        float z = Random.Range(boundUL.z, boundBR.z);

        Vector3 outputPos = new Vector3(x, y, z);

        //Run post processing on this output?? (later issue)

        return outputPos;
    }

    private Color getAverageKernelColor(Texture2D tex, Vector2 kernelPos, int kernelSize)
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

                numPixelsSampled += isInTexture(tex, samplePixel) ? 1 : 0;

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

    private bool isInTexture(Texture2D tex, Vector2Int point)
    {
        if (Mathf.Abs(point.x) > tex.width) return false;

        return Mathf.Abs(point.y) > tex.height ? false : true;
    }

    private ScoredObject getMutatedFromObject(ScoredObject originalObject, int n)
    {
        //The program currently just checks it for the highest
        //List<ScoredObject> currentIterationReplicationObjects = new List<ScoredObject>();

        //Recursive function for artificial selection process with objects

        Resources.UnloadUnusedAssets();

        //Mutate given object
        ScoredObject bestObject = new ScoredObject(null, Mathf.Infinity);
        for (int i = 0; i < numSpawnIterations; i++)
        {
            ScoredObject curObj = new ScoredObject(getNewReplicationObject(originalObject.Object, n));
            Texture2D screenImage = RTImage();
            Texture2D result = Resize(screenImage, targetScalingX, 0, true);
            curObj.score = scoreDiffenrenceOfTextures(downScaledOriginalPicture, result);

            DestroyImmediate(result, true);
            DestroyImmediate(screenImage, true);

            if (curObj.score < bestObject.score) bestObject = curObj;

            //DestroyImmediate(curObj.Object, true);
        }

        Resources.UnloadUnusedAssets();

        //Now we should have gotten the best mutated object of the group

        //Measure best scores
        Destroy(bestObject.Object);
        if (n == 0)
        {
            return bestObject;
        }
        else
        {
            return getMutatedFromObject(bestObject, n - 1);
        }
    }
}
