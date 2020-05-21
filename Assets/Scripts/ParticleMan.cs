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


public class ParticleMan : MonoBehaviour
{
    public ComputeShader CS;
    public int MaxN = 1024 * 256;
    public int CurN = 1024;
    public int IncN = 256;

    int ChunkSize = 1024 * 256;

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
        
        for(int i = 0; i < MaxN / ChunkSize; ++i)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }

        if(MaxN % ChunkSize != 0)
        {
            ParticlePostionList.Add(new ComputeBuffer(ChunkSize, 4 * sizeof(float)));
        }

        // ParticlePostion = new ComputeBuffer(MaxN, 4 * sizeof(float));
    }

    public void Emulate()
    {
        

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

        if(CurN + IncN < MaxN)
        {
            CurN += IncN;
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
        particleIterator.kernId = 0;
        particleIterator.threads = 1024;

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
        foreach (var part in AllCurrentParticles())
        {
            particleMat.SetBuffer("particleBuffer", part.buf);
            Graphics.DrawProcedural(particleMat, new Bounds(), MeshTopology.Points, 1, CurN);
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
