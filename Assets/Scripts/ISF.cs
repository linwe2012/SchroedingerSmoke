using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ISF : MonoBehaviour
{
    public FFT fft;
    public ComputeShader ISFCS;

    RenderTexture SchroedingerMul;
    RenderTexture PossionMul;
    RenderTexture Velocity;
    RenderTexture Divergence;
    RenderTexture TempRT;
    

    int kernelInitBuffer;
    int kernelShoedinger;
    int kernelNormalize;
    int kernelVelocityOneForm;
    int kernelComputeDivergence;
    int kernelPossionSpectral;
    int kernelGaugeTransform;
    
    int kernelInitializePsi;
    int kernelReprojectVelocityToGrid;

    public Vector3Int N = new Vector3Int(64, 64, 64);

    public Vector3 size = new Vector3(2, 2, 2);
    public float hbar = 0.1f;
    public float estimate_dt = 1f / 30f;
    public int current_tick = 0;
    public bool KeepSceneViewActive = true;

    class DebugCallCount
    {
        public int velocity = 1;
    } ;

    DebugCallCount dbg_call = new DebugCallCount();

    public Vector3 GetGridSize()
    {
        return ISFUtils.Div(size, ISFUtils.IntToFloat(N));
    }

    public int[] GetGrids()
    {
        int[] n = { N[0], N[1], N[2] };
        return n;
    }

    public RenderTexture GetVelocity()
    {
        return Velocity;
    }

    public void DispatchISFCS(int kernel, bool is_mini_threads = false)
    {
        ISFCS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
    }

    public void InitComputeShader()
    {
        kernelInitBuffer = ISFCS.FindKernel("InitBuffer");
        kernelShoedinger = ISFCS.FindKernel("Shoedinger");
        kernelNormalize = ISFCS.FindKernel("Normalize");

        kernelVelocityOneForm = ISFCS.FindKernel("VelocityOneForm");
        kernelComputeDivergence = ISFCS.FindKernel("ComputeDivergence");
        kernelPossionSpectral = ISFCS.FindKernel("PossionSpectral");
        kernelGaugeTransform = ISFCS.FindKernel("GaugeTransform");

        kernelInitializePsi = ISFCS.FindKernel("InitializePsi");
        kernelReprojectVelocityToGrid = ISFCS.FindKernel("ReprojectVelocityToGrid");
    }


    public void InitISF()
    {
        int[] res = GetGrids();

        SchroedingerMul = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        PossionMul = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RFloat);
        Velocity = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.ARGBFloat);
        Divergence = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        TempRT = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);

        fft.OutputRT = TempRT;
        fft.SetN(N);

        ISFCS.SetVector("size", size);
        ISFCS.SetInts("res", res);
        ISFCS.SetFloat("hbar", hbar);
        ISFCS.SetFloat("dt", estimate_dt);

        ISFCS.SetTexture(kernelInitBuffer, "SchroedingerMul", SchroedingerMul);
        ISFCS.SetTexture(kernelInitBuffer, "PossionMul", PossionMul);
        DispatchISFCS(kernelInitBuffer);

        fft.fftshift(ref SchroedingerMul, ref TempRT);
    }

    public void ShroedingerIntegration(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        fft.fft(ref psi1, ref TempRT);
        fft.fft(ref psi2, ref TempRT);
        
        ISFCS.SetTexture(kernelShoedinger, "Psi1", psi1);
        ISFCS.SetTexture(kernelShoedinger, "Psi2", psi2);
        ISFCS.SetTexture(kernelShoedinger, "SchroedingerMul", SchroedingerMul);
        DispatchISFCS(kernelShoedinger);

        

        fft.ifft(ref psi1, ref TempRT);
        fft.ifft(ref psi2, ref TempRT);
    }

    public void Normalize(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        ISFCS.SetTexture(kernelNormalize, "Psi1", psi1);
        ISFCS.SetTexture(kernelNormalize, "Psi2", psi2);
        DispatchISFCS(kernelNormalize);
    }

    public void VelocityOneForm(ref RenderTexture psi1, ref RenderTexture psi2, float new_hbar = 1)
    {
        ISFCS.SetFloat("hbar", new_hbar);
        // 首先计算 Oneform
        ISFCS.SetTexture(kernelVelocityOneForm, "Psi1", psi1);
        ISFCS.SetTexture(kernelVelocityOneForm, "Psi2", psi2);
        ISFCS.SetTexture(kernelVelocityOneForm, "Velocity", Velocity);
        DispatchISFCS(kernelVelocityOneForm);

        ISFCS.SetFloat("hbar", hbar);
    }

    public void PressureProject(ref RenderTexture psi1, ref RenderTexture psi2)
    { 
        VelocityOneForm(ref psi1, ref psi2);
        if(dbg_call.velocity == 0)
        {
            fft.ExportFloat4_3D(Velocity, "test/isf.velo.json");
        }

        ++dbg_call.velocity;
        

        // 计算散度
        ISFCS.SetTexture(kernelComputeDivergence, "Velocity", Velocity);
        ISFCS.SetTexture(kernelComputeDivergence, "Divergence", Divergence);
        DispatchISFCS(kernelComputeDivergence);

        //fft.ExportComplex3D(Divergence, "test/isf.div.json");

        // 求解 Possion 方程
        fft.fft(ref Divergence, ref TempRT);
        // Divergence 比较大, FFT 之后会放大 Divergence, 
        // float & double 会产生超过 0.02 的精度误差

        ISFCS.SetTexture(kernelPossionSpectral, "PossionMul", PossionMul);
        ISFCS.SetTexture(kernelPossionSpectral, "Divergence", Divergence);
        DispatchISFCS(kernelPossionSpectral);
        
        fft.ifft(ref Divergence, ref TempRT);
        //fft.ExportComplex3D(Divergence, "test/isf.pos.json");

        //fft.ExportFloat1_3D(PossionMul, "test/isf.fac.json");

        ISFCS.SetTexture(kernelGaugeTransform, "Psi1", psi1);
        ISFCS.SetTexture(kernelGaugeTransform, "Psi2", psi2);
        ISFCS.SetTexture(kernelGaugeTransform, "Divergence", Divergence);
        DispatchISFCS(kernelGaugeTransform);
    }

    public void ReprojectToGrid()
    {
        ISFCS.SetTexture(kernelReprojectVelocityToGrid, "Velocity", Velocity);
        DispatchISFCS(kernelReprojectVelocityToGrid);
    }

    public void MyRunTest()
    {
        // ISF 基础函数测试
        // ======================
        Texture3D tex_psi1;
        Texture3D tex_psi2;

        var psi1 = fft.LoadJson3D("test/psi1.json", out tex_psi1);
        var psi2 = fft.LoadJson3D("test/psi2.json", out tex_psi2);

        fft.ExportComplex3D(psi1, "test/sch.ps1.fft.json");

        ShroedingerIntegration(ref psi1, ref psi2);
        

        fft.ExportArray(SchroedingerMul, TextureFormat.RGBAFloat, 2, "test/isf.sch.mul.json");

        fft.ExportArray(psi1, TextureFormat.RGBAFloat, 2, "test/isf.sch.ps1.json");
        fft.ExportArray(psi2, TextureFormat.RGBAFloat, 2, "test/isf.sch.ps2.json");

        Normalize(ref psi1, ref psi2);

        
        fft.ExportArray(psi1, TextureFormat.RGBAFloat, 2, "test/isf.nor.ps1.json");
        fft.ExportArray(psi2, TextureFormat.RGBAFloat, 2, "test/isf.nor.ps2.json");

        PressureProject(ref psi1, ref psi2);

        fft.ExportArray(psi1, TextureFormat.RGBAFloat, 2, "test/isf.pre.ps1.json");
        fft.ExportArray(psi2, TextureFormat.RGBAFloat, 2, "test/isf.pre.ps2.json");
    }

    public void InitializePsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        ISFCS.SetTexture(kernelInitializePsi, "Psi1", psi1);
        ISFCS.SetTexture(kernelInitializePsi, "Psi2", psi2);

        DispatchISFCS(kernelInitializePsi);
    }

    private void Start()
    {
        //if (this.KeepSceneViewActive && Application.isEditor)
        //{
        //    UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        //}
    }
}
