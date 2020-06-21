using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
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

    public static Vector3 Div(Vector3 a, Vector3 b)
    {
        Vector3 c;
        c.x = a.x / b.x;
        c.y = a.y / b.y;
        c.z = a.z / b.z;
        return c;
    }

    public static Vector3Int Int3ToVector(int[] c)
    {
        return new Vector3Int(c[0], c[1], c[2]);
    }

    public static int CeilToMutiple(int x, int multiple)
    {
        int r = x % multiple;
        int d = x / multiple;

        if (r == 0) return x;

        return (d + 1) * multiple;
    }

    public static Vector3 VecSubScalar(Vector3 a, float s)
    {
        return new Vector3(
            a.x - s,
            a.y - s,
            a.z - s
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
        Vector3 dummy = new Vector3(0, 0, 0);
        GetMinMax(dummy, ref dummy, ref min);

        var ssize = max - min;
        ssize /= multiple;
        if(ssize.x <= 0)
        {
            ssize.x = 0.1f;
        }
        else if(ssize.y <= 0)
        {
            ssize.y = 0.1f;
        }
        else if(ssize.z <= 0)
        {
            ssize.z = 0.1f;
        }
        size = CeilToInt(ssize) * multiple;

        center = CeilToInt(min + new Vector3(size.x, size.y, size.z) / 2.0f);
    }

    public Vector3 min;
    public Vector3 max;
}