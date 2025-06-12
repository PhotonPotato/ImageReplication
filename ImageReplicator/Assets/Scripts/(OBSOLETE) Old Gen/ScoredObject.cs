using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoredObject
{
    public GameObject Object;
    public float score;

    public ScoredObject(GameObject obj, float score = 0)
    {
        Object = obj;
        this.score = score;
    }
}
