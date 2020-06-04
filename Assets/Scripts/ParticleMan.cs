using System.Collections;
using System.Collections.Generic;
using System.Linq;
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


// Source see: http://wiki.unity3d.com/index.php/ProceduralPrimitives
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

    public static Mesh Plane(float length, float width, int resX, int resZ)
    {
        // You can change that line to provide another MeshFilter
        // MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        // mesh.Clear();

        //float length = 1f;
        //float width = 1f;
       //int resX = 2; // 2 minimum
        //int resZ = 2;

        #region Vertices		
        Vector3[] vertices = new Vector3[resX * resZ];
        for (int z = 0; z < resZ; z++)
        {
            // [ -length / 2, length / 2 ]
            float zPos = ((float)z / (resZ - 1) - .5f) * length;
            for (int x = 0; x < resX; x++)
            {
                // [ -width / 2, width / 2 ]
                float xPos = ((float)x / (resX - 1) - .5f) * width;
                vertices[x + z * resX] = new Vector3(xPos, 0f, zPos);
            }
        }
        #endregion

        #region Normales
        Vector3[] normales = new Vector3[vertices.Length];
        for (int n = 0; n < normales.Length; n++)
            normales[n] = Vector3.up;
        #endregion

        #region UVs		
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int v = 0; v < resZ; v++)
        {
            for (int u = 0; u < resX; u++)
            {
                uvs[u + v * resX] = new Vector2((float)u / (resX - 1), (float)v / (resZ - 1));
            }
        }
        #endregion

        #region Triangles
        int nbFaces = (resX - 1) * (resZ - 1);
        int[] triangles = new int[nbFaces * 6];
        int t = 0;
        for (int face = 0; face < nbFaces; face++)
        {
            // Retrieve lower left corner from face ind
            int i = face % (resX - 1) + (face / (resZ - 1) * resX);

            triangles[t++] = i + resX;
            triangles[t++] = i + 1;
            triangles[t++] = i;

            triangles[t++] = i + resX;
            triangles[t++] = i + resX + 1;
            triangles[t++] = i + 1;
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
    public enum ParticleMode
    {
        Pure,
        Textured,
        Procedual,
        Billboard
    }

    public enum ManagingMode
    {
        CenterPool,
        Grouped
    }

    public ComputeShader CS;
    public int MaxN = 1024 * 256;
    public int CurN = 1024;
    public int IncN = 256;

    int ChunkSize = 1024 * 256;
    Mesh meshTextured;
    Mesh meshPure;
    Mesh meshPlane;

    public ParticleMode RenderMode = ParticleMode.Pure;
    public Material particleMatTextured;
    public Material particleMatPure;
    public Material particleMatProcedual;
    public Material particleMatBillboard;

    public Vector4 Scale = new Vector4(2.3f, 2.3f, 2.3f, 1);

    //ComputeBuffer ParticlePostion;
    
    List<ComputeBuffer> ParticlePostionList = new List<ComputeBuffer>();
    ManagingMode DataManaging;

    class GroupInfo
    {
        public Vector4 Color;
        public List<ComputeBuffer> Buffers = new List<ComputeBuffer>();
        public int Count;
    };

    List<GroupInfo> Groups = new List<GroupInfo>();


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

    public void InitGeometries()
    {
        meshTextured = GeometryGenerator.Sphere(0.5f, 4, 4);
        meshPure = GeometryGenerator.Sphere(0.1f, 3, 3);
        // meshPlane = GeometryGenerator.Plane(0.2f, 0.2f, 2, 2);
        meshPlane = GeometryGenerator.Plane(0.2f, 0.2f, 2, 2);
    }

    public void Init(ISF _isf, int _MaxN = -1)
    {
        DataManaging = ManagingMode.CenterPool;

        if (_MaxN > 0)
        {
            MaxN = _MaxN;
        }
        
        InitComputeShader(_isf);

        int n = MaxN / ChunkSize;
        for (int i = 0; i < n; ++i)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }

        if(MaxN % ChunkSize != 0)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }


        InitGeometries();
        // ParticlePostion = new ComputeBuffer(MaxN, 4 * sizeof(float));
    }

    public void GroupInit(ISF _isf)
    {
        DataManaging = ManagingMode.Grouped;

        InitComputeShader(_isf);
        InitGeometries();
    }

    

    public void AddGroup(Vector4 color, Vector4[] positions)
    {
        GroupInfo group = new GroupInfo();
        group.Color = color;

        List<ComputeBuffer> cb = new List<ComputeBuffer>();
        int n = positions.Length / ChunkSize;
        int r = positions.Length % ChunkSize;

        for(int i = 0; i < n; ++i)
        {
            var buf = new ComputeBuffer(ChunkSize, 4 * sizeof(float));
            buf.SetData(positions, i * ChunkSize, 0, ChunkSize);
            cb.Add(buf);
        }

        if(r != 0)
        {
            var buf = new ComputeBuffer(ChunkSize, 4 * sizeof(float));
            buf.SetData(positions, n * ChunkSize, 0, r);

            cb.Add(buf);
        }
        group.Buffers = cb;
        group.Count = positions.Length;

        Groups.Add(group);
    }

    public void Emulate()
    {
        if(DataManaging == ManagingMode.CenterPool)
        {
            if (CurN + IncN < MaxN)
            {
                CurN += IncN;
            }
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

    public void AddParticles(Vector3[] positions)
    {
        Vector4[] v = new Vector4[positions.Length];

        for(int i = 0; i < positions.Length; ++i)
        {
            v[i] = new Vector4(positions[i].x, positions[i].y, positions[i].z, 0);
        }

        AddParticles(v);
    }

    public void AddParticles(Vector4[] positions)
    {
        int n = positions.Length / ChunkSize;
        for(int i = 0; i < n; ++i)
        {
            ParticlePostionList[i].SetData(positions, i * ChunkSize, 0, ChunkSize);
        }

        int r = positions.Length % ChunkSize;

        if(r != 0)
        {
            ParticlePostionList[n].SetData(positions, n * ChunkSize, 0, r);
        }

        CurN = positions.Length;
        IncN = 0;
        MaxN = CurN;
    }



    public struct ParticleIterator
    {
        public ComputeBuffer buf;
        public int count;
        public int kernId;
        public int threads;
        public Vector4 color;
    }

    static public void InitMultiKindKernels(ComputeShader cs, string name, out int[] kerns)
    {
        kerns = new int[(int)GPUThreads.T_KINDS];
        kerns[(int)(GPUThreads.T64 & GPUThreads.T_INDEX)] = cs.FindKernel(name + "64");
        kerns[(int)(GPUThreads.T256 & GPUThreads.T_INDEX)] = cs.FindKernel(name + "256");
        kerns[(int)(GPUThreads.T1024 & GPUThreads.T_INDEX)] = cs.FindKernel(name);
    }

    public List<ComputeBuffer> AllCompuetBuffers()
    {
        return ParticlePostionList;
    }

    public IEnumerable<ParticleIterator> AllCurrentParticlesCenterPool()
    {
        ParticleIterator particleIterator;
        particleIterator.count = ChunkSize;
        particleIterator.kernId = (int)GPUThreads.T1024 & (int)GPUThreads.T_INDEX;
        particleIterator.threads = (int)GPUThreads.T1024 & (int)GPUThreads.T_DIV;
        particleIterator.color = new Vector4(1, 1, 1, 0);

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

    public IEnumerable<ParticleIterator> AllCurrentParticlesGrouped()
    {
        ParticleIterator particleIterator;
        particleIterator.count = ChunkSize;
        particleIterator.kernId = (int)GPUThreads.T1024 & (int)GPUThreads.T_INDEX;
        particleIterator.threads = (int)GPUThreads.T1024 & (int)GPUThreads.T_DIV;
        particleIterator.color = new Vector4(1, 1, 1, 0);

        for(int k = 0; k < Groups.Count; ++k)
        {
            
            var buffers = Groups[k].Buffers;
            var count = Groups[k].Count;
            particleIterator.color = Groups[k].Color;

            int i = 0;
            for (; i < buffers.Count; ++i)
            {
                if ((i + 1) * ChunkSize > count)
                {
                    break;
                }

                particleIterator.buf = buffers[i];
                yield return particleIterator;
            }

            if (CurN % ChunkSize != 0)
            {
                int kernId, threads;
                int num = count % ChunkSize;
                GetThreads(num, out kernId, out threads);

                particleIterator.count = num;
                particleIterator.kernId = kernId;
                particleIterator.threads = threads;
                particleIterator.buf = buffers[i];
                yield return particleIterator;
            }
        }
        
    }



    public IEnumerable<ParticleIterator> AllCurrentParticles()
    {
        if(DataManaging == ManagingMode.CenterPool)
        {
            return AllCurrentParticlesCenterPool();
        }

        else
        {
            return AllCurrentParticlesGrouped();
        }
    }


    public void DoRender()
    {
        Mesh mesh = null;
        Material mat = null;

        switch (RenderMode)
        {
            case ParticleMode.Pure:
                mesh = meshPure;
                mat = particleMatPure;
                break;
            case ParticleMode.Textured:
                mesh = meshTextured;
                mat = particleMatTextured;
                break;
            case ParticleMode.Procedual:
                mat = particleMatProcedual;
                break;
            case ParticleMode.Billboard:
                mat = particleMatBillboard;
                mesh = meshPlane;
                break;
        }

        mat.SetColor("_Scale", Scale);

        foreach (var part in AllCurrentParticles())
        {
            var block = new MaterialPropertyBlock();
            block.SetBuffer("particleBuffer", part.buf);
            block.SetVector("_BaseColor", part.color);

            // particleMat.SetBuffer("particleBuffer", part.buf);
            //Graphics.DrawProcedural(particleMat, new Bounds(), MeshTopology.Points, 1, part.count);
            if (ParticleMode.Procedual == RenderMode)
            {
                Graphics.DrawProcedural(mat, new Bounds(), MeshTopology.Points, 1, part.count, properties: block);
            }
            else
            {
                Graphics.DrawMeshInstancedProcedural(mesh, 0, mat, new Bounds(), count: part.count, properties: block);
            }
        }

    }

    void OnDestroy()
    {
        foreach(var part in ParticlePostionList)
        {
            part.Release();
        }
    }

    public void MyRunTest(ISF _isf)
    {
        Init(_isf, 1024);

        var compute = fft.LoadJson_Float4ComputeBuffer("test/part.itr01.json");

        fft.ExportFloat4(compute, "test/part.itr0.reflect.json");
        Texture3D text;
        var velocity = fft.LoadJson3D_Float4("test/isf.velo.json", out text);

        var grid_size = isf.GetGridSize();
        Debug.Log(grid_size.ToString());
        CS.SetVector("grid_size", grid_size);
        CS.SetInts("grids", isf.GetGrids());
        CS.SetFloat("dt", isf.estimate_dt);

        int kernel = kernelEnumlateParticle[2];
        CS.SetTexture(kernel, "Velocity", velocity);
        CS.SetBuffer(kernel, "ParticlePostion", compute);
        CS.Dispatch(kernel, 1, 1, 1);

        fft.ExportFloat4(compute, "test/part.itr1.json");
    }
}
