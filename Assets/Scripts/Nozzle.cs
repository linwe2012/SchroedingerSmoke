﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Nozzle : MonoBehaviour
{
    public ISF isf;
    public FFT fft;
    public ParticleMan particles;

    public ComputeShader CS;

    RenderTexture NozzleRT;
    RenderTexture DebugRT;

    RenderTexture psi1;
    RenderTexture psi2;

    int N = 16;

    // nozzle 相关
    int kernelCreateNozzleMask;
    int kernelNozzleUpdatePsi;
    int kernelNozzleClamp;
    int kernelInitNozzle;

    // Debug 专用
    int kernelZeroOutDebugOutput;
    int kernelBlitDebugMask;

    public Vector3 nozzle_center = new Vector3(4, 8, 8);
    public Vector3 nozzle_right;
    public Vector3 nozzle_dir = new Vector3(1, 0, 0);
    public Vector3 nozzle_velocity = new Vector3(0, 0, 1);
    public float nozzle_radius = 3;
    public float nozzle_length = 3;

    public void InitComputeShader()
    {
        kernelCreateNozzleMask = CS.FindKernel("CreateNozzleMask");
        kernelNozzleUpdatePsi = CS.FindKernel("NozzleUpdatePsi");

        kernelZeroOutDebugOutput = CS.FindKernel("ZeroOutDebugOutput");
        kernelBlitDebugMask = CS.FindKernel("BlitDebugMask");
        kernelNozzleClamp = CS.FindKernel("NozzleClamp");
        kernelInitNozzle = CS.FindKernel("InitNozzle");

        CS.SetVector("size", isf.size);
        int[] res = { N, N, N };
        CS.SetInts("res", res);
    }

    // TODO: 测试支持6个自由度的nozzle
    public void UpdateNozzles()
    {
        nozzle_dir.Normalize();
        nozzle_right = Vector3.Cross(nozzle_dir, Vector3.up);
        nozzle_right.Normalize();

        Vector3 nozzle_up = Vector3.Cross(nozzle_dir, nozzle_right);
        nozzle_up.Normalize();

        var mm = MinMaxVec.Create();

        mm.Feed(nozzle_center + nozzle_dir * nozzle_length / 2);
        mm.Feed(nozzle_center - nozzle_dir * nozzle_length / 2);

        mm.Feed(nozzle_center + nozzle_right * nozzle_radius);
        mm.Feed(nozzle_center - nozzle_right * nozzle_radius);

        mm.Feed(nozzle_center + nozzle_up * nozzle_radius);
        mm.Feed(nozzle_center - nozzle_up * nozzle_radius);

        Vector3Int box, box_center;
        mm.GetRenderTextureBoundingBox(8, out box, out box_center);

        if (NozzleRT && NozzleRT.IsCreated())
        {
            NozzleRT.Release();
        }

        NozzleRT = FFT.CreateRenderTexture3D(box.x, box.y, box.z, RenderTextureFormat.RFloat);

        var topleft = ISFUtils.IntToFloat(box_center) - ISFUtils.IntToFloat(box) / 2;
        if (topleft.x < 0 || topleft.y < 0 || topleft.z < 0)
        {
            Debug.LogError("The bounding box excceeds");
        }

        CS.SetVector("nozzle_ralative_center", ISFUtils.IntToFloat(box) / 2);
        CS.SetVector("nozzle_center", ISFUtils.IntToFloat(box_center));
        CS.SetFloat("nozzle_radius", nozzle_radius);
        CS.SetVector("nozzle_dir", nozzle_dir);
        CS.SetVector("nozzle_topleft", topleft);
        CS.SetFloat("nozzle_length", nozzle_length);
        CS.SetVector("nozzle_velocity", nozzle_velocity / isf.hbar);
        CS.SetVector("nozzle_right", nozzle_right);
        CS.SetVector("nozzle_up", nozzle_up);

        ISFSync();

        CS.SetTexture(kernelCreateNozzleMask, "Nozzle", NozzleRT);
        DispatchCS(kernelCreateNozzleMask, true);
    }

    void ISFSync()
    {
        
        CS.SetVector("size", isf.size);
        CS.SetInts("res", isf.GetGrids());
        CS.SetInts("grids", isf.GetGrids());
    }

    public void InitilizeNozzlePsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        CS.SetTexture(kernelNozzleUpdatePsi, "Psi1", psi1);
        CS.SetTexture(kernelNozzleUpdatePsi, "Psi2", psi2);
        CS.SetTexture(kernelNozzleUpdatePsi, "Nozzle", NozzleRT);
        
        

        var volecity2 = Vector3.Dot(nozzle_velocity, nozzle_velocity);
        float omega = volecity2 / (2 * isf.hbar);
        CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);

        DispatchCS(kernelNozzleUpdatePsi, true);

        isf.PressureProject(ref psi1, ref psi2);
    }

    public void DispatchCS(int kernel, bool is_mini_threads = false)
    {
        CS.Dispatch(kernel, N / 8, N / 8, N / 8);
        if (is_mini_threads)
        {
            CS.Dispatch(kernel, NozzleRT.width / 8, NozzleRT.height / 8, NozzleRT.volumeDepth / 8);
        }
        else
        {
            CS.Dispatch(kernel, N / 8, N / 8, N / 8);
        }
    }

    public void RunMyTest()
    {
        Texture3D tex_psi1;
        Texture3D tex_psi2;

        var psi1 = fft.LoadJson3D("test/psi1.json", out tex_psi1);
        var psi2 = fft.LoadJson3D("test/psi2.json", out tex_psi2);

        UpdateNozzles();
        fft.ExportFloat1_3D(NozzleRT, "test/isf.nozzle.json");

        // Nozzle mask
        // =====================
        var rtf4 = FFT.CreateRenderTexture3D(N, N, N);
        DebugRT = rtf4;
        CS.SetTexture(kernelZeroOutDebugOutput, "DebugOutput", rtf4);
        DispatchCS(kernelZeroOutDebugOutput);

        CS.SetTexture(kernelBlitDebugMask, "Nozzle", NozzleRT);
        CS.SetTexture(kernelBlitDebugMask, "DebugOutput", rtf4);
        DispatchCS(kernelBlitDebugMask);
        fft.ExportFloat4_3D(rtf4, "test/isf.nozzle.mask.json");

        DispatchCS(kernelZeroOutDebugOutput);

        isf.InitializePsi(ref psi1, ref psi2);
        for (int i = 0; i < 10; ++i)
        {
            InitilizeNozzlePsi(ref psi1, ref psi2);
        }

        fft.ExportComplex3D(psi1, "test/isf.ini.ps1.json");
        fft.ExportComplex3D(psi2, "test/isf.ini.ps2.json");

        fft.ExportFloat4_3D(rtf4, "test/isf.phase.json");

        // Application.Quit(0);
    }

    void InitilizeParticles()
    {
        particles.Init(isf);
        CS.SetBuffer(kernelInitNozzle, "ParticlePostion", particles.GetParticlePostion());
        CS.Dispatch(kernelInitNozzle, particles.MaxN / 1024, 1, 1);
    }

    void ClampParticles()
    {
        CS.SetBuffer(kernelNozzleClamp, "ParticlePostion", particles.GetParticlePostion());
        CS.Dispatch(kernelNozzleClamp, particles.CurN / 1024, 1, 1);
    }

    void PrepareNozzle()
    {
        N = isf.N;
        fft = isf.fft;
        fft.init();

        isf.InitComputeShader();
        isf.InitISF();

        InitComputeShader();
        UpdateNozzles();

        psi1 = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
        psi2 = FFT.CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);

        isf.InitializePsi(ref psi1, ref psi2);
        for (int i = 0; i < 10; ++i)
        {
            InitilizeNozzlePsi(ref psi1, ref psi2);
        }

        InitilizeParticles();
    }

    // Start is called before the first frame update
    void Start()
    {
        /*
        fft = isf.fft;
        fft.init();
        fft.myRunTest();

        isf.InitComputeShader();
        isf.InitISF();
        isf.MyRunTest();

        InitComputeShader();
        RunMyTest();
        */

        PrepareNozzle();
    }

    // Update is called once per frame
    void Update()
    {
        isf.ShroedingerIntegration(ref psi1, ref psi2);
        isf.Normalize(ref psi1, ref psi2);
        isf.PressureProject(ref psi1, ref psi2);

        InitilizeNozzlePsi(ref psi1, ref psi2);

        isf.VelocityOneForm(ref psi1, ref psi2, isf.hbar);
        isf.ReprojectToGrid();

        particles.Emulate();
        ClampParticles();

        isf.current_tick += 1;

        particles.DoRender();
    }

    void OnDestroy()
    {
        psi1.Release();
        psi2.Release();
    }
}