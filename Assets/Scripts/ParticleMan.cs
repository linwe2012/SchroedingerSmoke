using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum GPUThreads : int
{
    T64 = 0 + 64,
    T256 = 1 + 256,
    T1024 = 2 + 1024,

    T_KINDS = 3,
    T_INDEX = T64 - 1,
    T_DIV = ~T_INDEX
}



class GeometryGenerator
{
    public static Mesh Sphere(float radius = 1f, int nbLong = 24, int nbLat = 16)
    {
        //MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        // Mesh mesh = filter.mesh;
        Mesh mesh = new Mesh();

        mesh.Clear();

        #region Vertices
        Vector3[] vertices = new Vector3[(nbLong + 1) * nbLat + 2];
        float _pi = Mathf.PI;
        float _2pi = _pi * 2f;

        vertices[0] = Vector3.up * radius;
        for (int lat = 0; lat < nbLat; lat++)
        {
            float a1 = _pi * (float)(lat + 1) / (nbLat + 1);
            float sin1 = Mathf.Sin(a1);
            float cos1 = Mathf.Cos(a1);

            for (int lon = 0; lon <= nbLong; lon++)
            {
                float a2 = _2pi * (float)(lon == nbLong ? 0 : lon) / nbLong;
                float sin2 = Mathf.Sin(a2);
                float cos2 = Mathf.Cos(a2);

                vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
            }
        }
        vertices[vertices.Length - 1] = Vector3.up * -radius;
        #endregion

        #region Normales		
        Vector3[] normales = new Vector3[vertices.Length];
        for (int n = 0; n < vertices.Length; n++)
            normales[n] = vertices[n].normalized;
        #endregion

        #region UVs
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = Vector2.up;
        uvs[uvs.Length - 1] = Vector2.zero;
        for (int lat = 0; lat < nbLat; lat++)
            for (int lon = 0; lon <= nbLong; lon++)
                uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
        #endregion

        #region Triangles
        int nbFaces = vertices.Length;
        int nbTriangles = nbFaces * 2;
        int nbIndexes = nbTriangles * 3;
        int[] triangles = new int[nbIndexes];

        //Top Cap
        int i = 0;
        for (int lon = 0; lon < nbLong; lon++)
        {
            triangles[i++] = lon + 2;
            triangles[i++] = lon + 1;
            triangles[i++] = 0;
        }

        //Middle
        for (int lat = 0; lat < nbLat - 1; lat++)
        {
            for (int lon = 0; lon < nbLong; lon++)
            {
                int current = lon + lat * (nbLong + 1) + 1;
                int next = current + nbLong + 1;

                triangles[i++] = current;
                triangles[i++] = current + 1;
                triangles[i++] = next + 1;

                triangles[i++] = current;
                triangles[i++] = next + 1;
                triangles[i++] = next;
            }
        }

        //Bottom Cap
        for (int lon = 0; lon < nbLong; lon++)
        {
            triangles[i++] = vertices.Length - 1;
            triangles[i++] = vertices.Length - (lon + 2) - 1;
            triangles[i++] = vertices.Length - (lon + 1) - 1;
        }
        #endregion

        mesh.vertices = vertices;
        mesh.normals = normales;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.Optimize();

        return mesh;
    }
}
public class ParticleMan : MonoBehaviour
{
    public ComputeShader CS;
    public int MaxN = 1024 * 256;
    public int CurN = 1024;
    public int IncN = 256;

    int ChunkSize = 1024 * 256;
    Mesh mesh;

    public Material particleMat;
    public Vector4 Scale = new Vector4(2.3f, 2.3f, 2.3f, 1);

    //ComputeBuffer ParticlePostion;

    List<ComputeBuffer> ParticlePostionList = new List<ComputeBuffer>();

    int[] kernelEnumlateParticle = new int[(int)GPUThreads.T_KINDS];
    ISF isf;
    FFT fft;

    public void GetThreads(int N, out int kernelIndex, out int kernelThreads)
    {
        int n = GetThreads(N);
        kernelIndex = n & (int)GPUThreads.T_INDEX;
        kernelThreads = n & (int)GPUThreads.T_DIV;
    }

    public int GetThreads(int N)
    {
        //if(CurN > 1024 * 128) return (int)GPUThreads.T1024;
        if (N % 1024 == 0) return (int)GPUThreads.T1024;

        //if (CurN > 1024 * 64) return (int)GPUThreads.T256;
        if (N % 256 == 0) return (int)GPUThreads.T256;

        return (int)GPUThreads.T64;
    }

    public int GetThreads()
    {
        //if(CurN > 1024 * 128) return (int)GPUThreads.T1024;
        if (CurN % 1024 == 0) return (int)GPUThreads.T1024;

        //if (CurN > 1024 * 64) return (int)GPUThreads.T256;
        if (CurN % 256 == 0) return (int)GPUThreads.T256;

        return (int)GPUThreads.T64;
    }

    public void InitComputeShader(ISF _isf)
    {
        isf = _isf;
        fft = isf.fft;

        kernelEnumlateParticle[(int)(GPUThreads.T1024 & GPUThreads.T_INDEX)] = CS.FindKernel("EnumlateParticle");
        kernelEnumlateParticle[(int)(GPUThreads.T256 & GPUThreads.T_INDEX)] = CS.FindKernel("EnumlateParticle256");
        kernelEnumlateParticle[(int)(GPUThreads.T64 & GPUThreads.T_INDEX)] = CS.FindKernel("EnumlateParticle64");
    }

    public void Init(ISF _isf, int _MaxN = -1)
    {
        if(_MaxN > 0)
        {
            MaxN = _MaxN;
        }
        
        InitComputeShader(_isf);

        int n = MaxN / ChunkSize;


        for (int i = 0; i < MaxN / ChunkSize; ++i)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }

        if(MaxN % ChunkSize != 0)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }

        mesh = GeometryGenerator.Sphere(0.02f, 3, 3);
        

        // ParticlePostion = new ComputeBuffer(MaxN, 4 * sizeof(float));
    }

    public void Emulate()
    {
        if (CurN + IncN < MaxN)
        {
            CurN += IncN;
        }

        CS.SetVector("grid_size", isf.GetGridSize());
        CS.SetInts("grids", isf.GetGrids());
        CS.SetFloat("dt", isf.estimate_dt);

        foreach(var part in AllCurrentParticles())
        {
            int kernel = kernelEnumlateParticle[part.kernId];
            CS.SetTexture(kernel, "Velocity", isf.GetVelocity());
            CS.SetBuffer(kernel, "ParticlePostion", part.buf);
            CS.Dispatch(kernel, part.count / part.threads, 1, 1);
        }
    }

    public struct ParticleIterator
    {
        public ComputeBuffer buf;
        public int count;
        public int kernId;
        public int threads;
    }

    public List<ComputeBuffer> AllCompuetBuffers()
    {
        return ParticlePostionList;
    }

    public IEnumerable<ParticleIterator> AllCurrentParticles()
    {
        ParticleIterator particleIterator;
        particleIterator.count = ChunkSize;
        particleIterator.kernId = (int)GPUThreads.T1024 & (int) GPUThreads.T_INDEX;
        particleIterator.threads = (int)GPUThreads.T1024 & (int)GPUThreads.T_DIV;

        int i = 0;
        for (; i < ParticlePostionList.Count; ++i)
        {
            if ((i + 1) * ChunkSize > CurN)
            {
                break;
            }

            particleIterator.buf = ParticlePostionList[i];
            yield return particleIterator;
        }

        if (CurN % ChunkSize != 0)
        {
            int kernId, threads;
            int num = CurN % ChunkSize;
            GetThreads(num, out kernId, out threads);

            particleIterator.count = num;
            particleIterator.kernId = kernId;
            particleIterator.threads = threads;
            particleIterator.buf = ParticlePostionList[i];
            yield return particleIterator;
        }
    }


    public void DoRender()
    {
        particleMat.SetColor("_Scale", Scale);
        int N = 0;

         


        foreach (var part in AllCurrentParticles())
        {
            var block = new MaterialPropertyBlock();
            block.SetBuffer("particleBuffer", part.buf);
            // particleMat.SetBuffer("particleBuffer", part.buf);
            //Graphics.DrawProcedural(particleMat, new Bounds(), MeshTopology.Points, 1, part.count);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, particleMat, new Bounds(), count: part.count, properties: block);

            N += part.count;
        }

    }

    void OnDestroy()
    {
        foreach(var part in ParticlePostionList)
        {
            part.Release();
        }
    }
}
