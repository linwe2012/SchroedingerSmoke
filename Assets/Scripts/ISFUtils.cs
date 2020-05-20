using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public static class ISFUtils
{
    // Start is called before the first frame update
    public static Vector3 IntToFloat(Vector3Int v)
    {
        return new Vector3(
            v.x, v.y, v.z
            );
    }

}


public struct MinMaxVec
{
    public static void GetMinMax(Vector3 v1, ref Vector3 min, ref Vector3 max)
    {
        min.x = Mathf.Min(v1.x, min.x);
        min.y = Mathf.Min(v1.y, min.y);
        min.z = Mathf.Min(v1.z, min.z);

        max.x = Mathf.Max(v1.x, max.x);
        max.y = Mathf.Max(v1.y, max.y);
        max.z = Mathf.Max(v1.z, max.z);
    }

    static Vector3Int FloorToInt(Vector3 v)
    {
        Vector3Int i = new Vector3Int();
        i.x = Mathf.FloorToInt(v.x);
        i.y = Mathf.FloorToInt(v.y);
        i.z = Mathf.FloorToInt(v.z);

        return i;
    }

    static Vector3Int CeilToInt(Vector3 v)
    {
        Vector3Int i = new Vector3Int();
        i.x = Mathf.CeilToInt(v.x);
        i.y = Mathf.CeilToInt(v.y);
        i.z = Mathf.CeilToInt(v.z);

        return i;
    }

    public static MinMaxVec Create()
    {
        MinMaxVec v;
        v.min = new Vector3(9999, 9999, 9999);
        v.max = new Vector3(-9999, -9999, -9999);
        return v;
    }

    public void Feed(Vector3 v)
    {
        GetMinMax(v, ref min, ref max);
    }

    public void GetRenderTextureBoundingBox(int multiple, out Vector3Int size, out Vector3Int center)
    {
        var ssize = max - min;
        ssize /= multiple;
        size = CeilToInt(ssize) * multiple;

        center = FloorToInt(min + size / 2);
    }

    public Vector3 min;
    public Vector3 max;
}