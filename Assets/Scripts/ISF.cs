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


    int N = 16;

    Vector3 size = new Vector3(2, 2, 2);
    float hbar = 0.1f;
    

    

    

    // Start is called before the first frame update
    void Start()
    {
        fft.init();
        fft.myRunTest();

        InitComputeShader();
        InitISF();
    }

    void DispatchISFCS(int kernel)
    {
        ISFCS.Dispatch(kernel, N / 8, N / 8, N / 8);
    }

    void InitComputeShader()
    {
        kernelInitBuffer = ISFCS.FindKernel("InitBuffer");
        kernelShoedinger = ISFCS.FindKernel("Shoedinger");
        kernelNormalize = ISFCS.FindKernel("Normalize");

        kernelVelocityOneForm = ISFCS.FindKernel("VelocityOneForm");
        kernelComputeDivergence = ISFCS.FindKernel("ComputeDivergence");
        kernelPossionSpectral = ISFCS.FindKernel("PossionSpectral");
        kernelGaugeTransform = ISFCS.FindKernel("GaugeTransform");
    }

    void InitISF()
    {
        int[] res = { N, N, N };

        SchroedingerMul = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
        PossionMul = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RFloat);
        Velocity = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.ARGBFloat);
        Divergence = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RFloat);
        TempRT = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);

        fft.OutputRT = TempRT;
        fft.SetN(N);

        ISFCS.SetVector("size", size);
        ISFCS.SetInts("res", res);
        ISFCS.SetFloat("hbar", hbar);

        ISFCS.SetTexture(kernelInitBuffer, "SchroedingerMul", SchroedingerMul);
        ISFCS.SetTexture(kernelInitBuffer, "PossionMul", PossionMul);
        DispatchISFCS(kernelInitBuffer);

        fft.fftshift(ref SchroedingerMul);
    }

    void ShroedingerIntegration(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        fft.fft(ref psi1);
        fft.fft(ref psi2);

        ISFCS.SetTexture(kernelShoedinger, "Psi1", psi1);
        ISFCS.SetTexture(kernelShoedinger, "Psi2", psi2);
        ISFCS.SetTexture(kernelShoedinger, "SchroedingerMul", SchroedingerMul);
        DispatchISFCS(kernelShoedinger);

        fft.ifft(ref psi1);
        fft.ifft(ref psi2);
    }

    void Normalize(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        ISFCS.SetTexture(kernelNormalize, "Psi1", psi1);
        ISFCS.SetTexture(kernelNormalize, "Psi2", psi2);
        DispatchISFCS(kernelNormalize);
    }

    void PressureProject(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        // 首先计算 Oneform
        ISFCS.SetTexture(kernelVelocityOneForm, "Psi1", psi1);
        ISFCS.SetTexture(kernelVelocityOneForm, "Psi2", psi2);
        ISFCS.SetTexture(kernelVelocityOneForm, "Velocity", Velocity);
        DispatchISFCS(kernelVelocityOneForm);

        // 计算散度
        ISFCS.SetTexture(kernelComputeDivergence, "Velocity", Velocity);
        ISFCS.SetTexture(kernelComputeDivergence, "Divergence", Divergence);
        DispatchISFCS(kernelComputeDivergence);

        // 求解 Possion 方程
        fft.fft(ref Divergence);
        ISFCS.SetTexture(kernelPossionSpectral, "PossionMul", PossionMul);
        ISFCS.SetTexture(kernelPossionSpectral, "Divergence", Divergence);
        DispatchISFCS(kernelPossionSpectral);
        fft.ifft(ref Divergence);

        ISFCS.SetTexture(kernelGaugeTransform, "Psi1", psi1);
        ISFCS.SetTexture(kernelGaugeTransform, "Psi2", psi2);
        ISFCS.SetTexture(kernelGaugeTransform, "Divergence", Divergence);
        DispatchISFCS(kernelGaugeTransform);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
