using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Leapfrog : MonoBehaviour
{
    public Vector3Int N = new Vector3Int( 128, 64, 64 );
    public Vector3 Size = new Vector3(10, 5, 5);
    public Vector3 Center1 = new Vector3(64, 32, 32);
    public Vector3 Center2 = new Vector3(64, 32, 32);

    public float Radius1 = 20;
    public float Radius2 = 12;

    public Vector3 Direction1 = new Vector3(-1, 0, 0);
    public Vector3 Direction2 = new Vector3(-1, 0, 0);

    public Vector3 GlobalVelocity = new Vector3(-0.2f, 0, 0);
    public float Thickness = 5;
    public float HBar = 0.1f;

    public int ParticleCount = 65536;
    public Vector3 BoxSize = new Vector3(1, 30, 30);
    public Vector3 BoxCenter = new Vector3(64, 32, 32);

    public Vector3 NFloat;

    FFT fft;
    public ISF isf;
    public ParticleMan particleMan;
    public ComputeShader CS;

    public Camera cam;
    public Vector3 position = new Vector3(180, 90, 267);

    RenderTexture psi1;
    RenderTexture psi2;

    int kernelCreateCylinderPsi;
    int kernelInitPsi;
    int[] kernelInitParticles;

    void DispatchCS(int kernel)
    {
        var N = isf.N;
        CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
    }

    void InitPsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        CS.SetTexture(kernelInitPsi, "Psi1", psi1);
        CS.SetTexture(kernelInitPsi, "Psi2", psi2);

        //var volecity2 = Vector3.Dot(GlobalVelocity, GlobalVelocity);
        //float omega = volecity2 / (2 * isf.hbar);
        // CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);
        CS.SetFloat("omega_t", 0);
        CS.SetVector("velocity", GlobalVelocity / isf.hbar);

        DispatchCS(kernelInitPsi);

        

        InitCylinderPsi(ref psi1, Direction1, Center1, Radius1, Thickness);
        InitCylinderPsi(ref psi1, Direction2, Center2, Radius2, Thickness);
        // fft.ExportComplex3D(psi1, "test/leap.psi1.json");

        isf.Normalize(ref psi1, ref psi2);
        isf.PressureProject(ref psi1, ref psi2);
    }

    void InitCylinderPsi(ref RenderTexture psi1, 
        Vector3 CylinderNormal, Vector3 CylinderCenter, 
        float CylinderRaduis, float CylinderThickness)
    {
        CS.SetVector("CylinderNormal", CylinderNormal);
        CS.SetVector("CylinderCenter", CylinderCenter);
        CS.SetFloat("CylinderRaduis", CylinderRaduis / NFloat[1] * Size[1]);
        CS.SetFloat("CylinderThickness", CylinderThickness / NFloat[0] * Size[0]);
        CS.SetTexture(kernelCreateCylinderPsi, "Psi1", psi1);

        DispatchCS(kernelCreateCylinderPsi);
    }


    void InitComputeShader()
    {
        kernelCreateCylinderPsi = CS.FindKernel("CreateCylinderPsi");
        kernelInitPsi = CS.FindKernel("InitPsi");
        ParticleMan.InitMultiKindKernels(CS, "InitParticles", out kernelInitParticles);

        CS.SetVector("size", isf.size);
        int[] res = isf.GetGrids();
        CS.SetInts("res", res);
        CS.SetInts("grids", res);
    }

    void InitParticles()
    {
        particleMan.Init(isf, ParticleCount, ParticleCount);
        
        CS.SetVector("box_size", BoxSize);
        CS.SetVector("box_center", BoxCenter);

        foreach (var part in particleMan.AllCurrentParticles())
        {
            int[] rng_state = new int[3]
            {
                Random.Range(0, Int32.MaxValue),
                Random.Range(0, Int32.MaxValue),
                Random.Range(0, Int32.MaxValue)
            };

            CS.SetInts("rng_state", rng_state);
            var kern = kernelInitParticles[part.kernId];

            CS.SetBuffer(kern, "ParticlePostion", part.buf);
            CS.Dispatch(kern, part.count / part.threads, 1, 1);
        }
        

    }

    void Prepare()
    {
        fft = isf.fft;
        fft.init();

        isf.hbar = HBar;
        isf.size = Size;
        isf.N = N;
        isf.estimate_dt = 1.0f / 24.0f;

        NFloat = ISFUtils.IntToFloat(N);


        isf.InitComputeShader();
        isf.InitISF();

        psi1 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        psi2 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        isf.InitializePsi(ref psi1, ref psi2);


        InitComputeShader();
        InitPsi(ref psi1, ref psi2);
        InitParticles();
    }

    

    // Start is called before the first frame update
    void Start()
    {
        Prepare();
    }

    // Update is called once per frame
    void Update()
    {
        isf.current_tick += 1;

        isf.ShroedingerIntegration(ref psi1, ref psi2);
        isf.Normalize(ref psi1, ref psi2);
        isf.PressureProject(ref psi1, ref psi2);

        isf.VelocityOneForm(ref psi1, ref psi2, isf.hbar);
        isf.ReprojectToGrid();

        particleMan.Emulate();
        particleMan.DoRender();
    }
}
