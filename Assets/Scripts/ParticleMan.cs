using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleMan : MonoBehaviour
{
    public ComputeShader CS;
    public int MaxN = 1024 * 100;
    public int CurN = 1024;
    public int IncN = 1024;

    public Material particleMat;
    public Vector4 Scale = new Vector4(2.3f, 2.3f, 2.3f, 1);

    ComputeBuffer ParticlePostion;

    int kernelEnumlateParticle;
    ISF isf;
    FFT fft;

    public ComputeBuffer GetParticlePostion()
    {
        return ParticlePostion;
    }

    public void InitComputeShader(ISF _isf)
    {
        isf = _isf;
        fft = isf.fft;
        kernelEnumlateParticle = CS.FindKernel("EnumlateParticle");
    }

    public void Init(ISF _isf, int MaxN = 1024 * 100)
    {
        InitComputeShader(_isf);

        ParticlePostion = new ComputeBuffer(MaxN, 4 * sizeof(float));
    }

    public void Emulate()
    {
        CS.SetTexture(kernelEnumlateParticle, "Velocity", isf.GetVelocity());
        CS.SetBuffer(kernelEnumlateParticle, "ParticlePostion", ParticlePostion);
        // CS.SetTexture(kernelEnumlateParticle, "ParticlePostion", ParticlePostion);
        CS.SetVector("grid_size", isf.GetGridSize());
        CS.SetInts("grids", isf.GetGrids());
        CS.SetFloat("dt", isf.estimate_dt);
        CS.Dispatch(kernelEnumlateParticle, CurN / 1024, 1, 1);

        if(CurN + IncN < MaxN)
        {
            CurN += IncN;
        }
    }


    public void DoRender()
    {
        particleMat.SetBuffer("particleBuffer", ParticlePostion);
        particleMat.SetColor("_Scale", Scale);
        Graphics.DrawProcedural(particleMat, new Bounds(), MeshTopology.Points, 1, CurN);
    }

    void OnDestroy()
    {
        ParticlePostion.Release();
    }
}
