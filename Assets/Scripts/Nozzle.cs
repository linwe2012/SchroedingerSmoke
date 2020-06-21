using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class Nozzle : MonoBehaviour
{

    public ISF isf;
    FFT fft;
    public ParticleMan particles;

    public ComputeShader CS;

    RenderTexture NozzleRT;
    RenderTexture DebugRT;

    RenderTexture psi1;
    RenderTexture psi2;

    

    // nozzle 相关
    int kernelCreateNozzleMask;
    int kernelNozzleUpdatePsi;
    int[] kernelNozzleClamp = new int[(int)GPUThreads.T_KINDS];
    int kernelInitNozzle;

    Random random = new Random();

    // Debug 专用
    int kernelZeroOutDebugOutput;
    int kernelBlitDebugMask;

    public Vector3 nozzle_center = new Vector3(4, 8, 8);
    public Vector3 nozzle_right;
    public Vector3 nozzle_dir = new Vector3(1, 0, 0);
    public Vector3 nozzle_velocity = new Vector3(1, 0, 0);
    public float nozzle_radius = 3;
    public float nozzle_length = 3;
    Vector3 nozzle_topleft;

    public bool CreateObstacle = false;
    public float ObstaleRadius = 3;
    public Vector3 ObstaclePosition = new Vector3(8, 8, 8);
    int ObstacleRoundedRadius;
    Vector3 ObstacleTopLeft;

    RenderTexture ObstacleMask;
    int kernelCreateShpereObstacleMask;
    GameObject ObstacleChild;

    public Camera Cam;
    public Vector3 CameraPosition = new Vector3(90, 60, 100);
    public Vector3 CameraRotation = new Vector3(0, 90, 0);

    public void InitComputeShader()
    {
        kernelCreateNozzleMask = CS.FindKernel("CreateNozzleMask");
        kernelNozzleUpdatePsi = CS.FindKernel("NozzleUpdatePsi");

        kernelZeroOutDebugOutput = CS.FindKernel("ZeroOutDebugOutput");
        kernelBlitDebugMask = CS.FindKernel("BlitDebugMask");
        kernelNozzleClamp[(int)(GPUThreads.T64 & GPUThreads.T_INDEX)] = CS.FindKernel("NozzleClamp64");
        kernelNozzleClamp[(int)(GPUThreads.T256 & GPUThreads.T_INDEX)] = CS.FindKernel("NozzleClamp256");
        kernelNozzleClamp[(int)(GPUThreads.T1024 & GPUThreads.T_INDEX)] = CS.FindKernel("NozzleClamp");

        kernelInitNozzle = CS.FindKernel("InitNozzle");
        kernelCreateShpereObstacleMask = CS.FindKernel("CreateShpereObstacleMask");
        CS.SetVector("size", isf.size);
        int[] res = isf.GetGrids();
        CS.SetInts("res", res);
    }

    // TODO: 测试支持6个自由度的nozzle
    public void UpdateNozzles()
    {

        nozzle_dir.Normalize();
        if(nozzle_dir == Vector3.up)
        {
            nozzle_dir.x += 0.01f;
        }
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
        nozzle_topleft = topleft;
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

        //ExportDebugMask();
    }

    void CreateShpereObstacleMask()
    {
        if (!CreateObstacle) return;

        var geo_sphere = GeometryGenerator.Sphere(ObstaleRadius, 32, 32);
        ObstacleChild = new GameObject("sphere");
        ObstacleChild.transform.parent = this.gameObject.transform;
        var mesh_filter = ObstacleChild.AddComponent<MeshFilter>();
        mesh_filter.mesh = geo_sphere;
        ObstacleChild.transform.position = ObstaclePosition;
        var render = ObstacleChild.AddComponent<MeshRenderer>();
        render.material = new Material(Shader.Find("Specular"));

        int n = Mathf.CeilToInt(2 * ObstaleRadius);
        n = ISFUtils.CeilToMutiple(n, 8); // 保证 n 是  8 的倍数
        ObstacleRoundedRadius = n;
        Vector3 relative_center = new Vector3(n / 2, n / 2, n / 2);
        ObstacleMask = FFT.CreateRenderTexture3D(n, n, n);

        CS.SetVector("nozzle_center", relative_center);
        CS.SetFloat("nozzle_radius", ObstaleRadius);
        CS.SetTexture(kernelCreateShpereObstacleMask, "Nozzle", ObstacleMask);

        DispatchCS(kernelCreateShpereObstacleMask, true, ObstacleMask);

        ObstacleTopLeft = ISFUtils.VecSubScalar(ObstaclePosition, n);
     
        for(int i = 1; i < 10; ++i)
        {
            UpdateObstacle();
        }
    }

    void UpdateObstacle()
    {
        if (!CreateObstacle) return;

        CS.SetTexture(kernelNozzleUpdatePsi, "Psi1", psi1);
        CS.SetTexture(kernelNozzleUpdatePsi, "Psi2", psi2);
        CS.SetTexture(kernelNozzleUpdatePsi, "Nozzle", ObstacleMask);

        float omega = 0;
        CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);
        CS.SetVector("nozzle_velocity", new Vector3(0, 0, 0));
        CS.SetVector("nozzle_topleft", ObstacleTopLeft);

        DispatchCS(kernelNozzleUpdatePsi, true, ObstacleMask);
        isf.PressureProject(ref psi1, ref psi2);
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
        CS.SetVector("nozzle_velocity", nozzle_velocity / isf.hbar);
        CS.SetVector("nozzle_topleft", nozzle_topleft);

        var volecity2 = Vector3.Dot(nozzle_velocity, nozzle_velocity);
        float omega = volecity2 / (2 * isf.hbar);
        CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);

        DispatchCS(kernelNozzleUpdatePsi, true);

        isf.PressureProject(ref psi1, ref psi2);
    }

    public void DispatchCS(int kernel, bool is_mini_threads = false, RenderTexture target = null)
    {
        var N = isf.N;
        if(target == null)
        {
            target = NozzleRT;
        }
        // CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
        if (is_mini_threads)
        {
            CS.Dispatch(kernel, target.width / 8, target.height / 8, target.volumeDepth / 8);
        }
        else
        {
            CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
        }
    }

    void ExportDebugMask()
    {
        if(!DebugRT || !DebugRT.IsCreated())
        {
            var N = isf.N;
            var rtf4 = FFT.CreateRenderTexture3D(N[0], N[1], N[2]);
            DebugRT = 
            DebugRT = rtf4;
        }

        CS.SetTexture(kernelZeroOutDebugOutput, "DebugOutput", DebugRT);
        DispatchCS(kernelZeroOutDebugOutput);

        CS.SetTexture(kernelBlitDebugMask, "Nozzle", NozzleRT);
        CS.SetTexture(kernelBlitDebugMask, "DebugOutput", DebugRT);
        DispatchCS(kernelBlitDebugMask);
        fft.ExportFloat4_3D(DebugRT, "test/isf.nozzle.mask.json");
    }

    public void RunMyTest()
    {
        Texture3D tex_psi1;
        Texture3D tex_psi2;

        var psi1 = fft.LoadJson3D("test/psi1.json", out tex_psi1);
        var psi2 = fft.LoadJson3D("test/psi2.json", out tex_psi2);

        UpdateNozzles();
        // fft.ExportFloat1_3D(NozzleRT, "test/isf.nozzle.json");

        var N = isf.N;
        // Nozzle mask
        // =====================
        var rtf4 = FFT.CreateRenderTexture3D(N[0], N[1], N[2]);
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

        foreach(var buf in particles.AllCompuetBuffers())
        {
            CS.SetInt("rng_state", Random.Range(0, 999999));
            CS.SetBuffer(kernelInitNozzle, "ParticlePostion", buf);
            CS.Dispatch(kernelInitNozzle, buf.count / 1024, 1, 1);
        }
        
    }

    void ClampParticles()
    {
        
        foreach(var part in particles.AllCurrentParticles())
        {
            int kernel = kernelNozzleClamp[part.kernId];
            CS.SetInt("rng_state", Random.Range(0, 999999));
            CS.SetBuffer(kernel, "ParticlePostion", part.buf);
            CS.Dispatch(kernel, part.count / part.threads, 1, 1);
        }
        
    }

    void PrepareNozzle()
    {
        fft = isf.fft;
        fft.init();

        isf.InitComputeShader();
        isf.InitISF();

        InitComputeShader();

        var N = isf.N;

        psi1 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        psi2 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);

        UpdateNozzles();
        

        isf.InitializePsi(ref psi1, ref psi2);
        for (int i = 0; i < 10; ++i)
        {
            InitilizeNozzlePsi(ref psi1, ref psi2);
        }
        CreateShpereObstacleMask();

        InitilizeParticles();

        // fft.ExportFloat4_3D(particles.GetParticlePostion(), "test/part.init.json");
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
        /*
        fft = isf.fft;
        fft.init();
        isf.InitComputeShader();
        isf.InitISF();
        
        particles.MyRunTest(isf);
        */
        PrepareNozzle();
        if(Cam)
        {
            Cam.transform.position = CameraPosition;
            Cam.transform.rotation = Quaternion.Euler(CameraRotation);
        }
    }

    // Update is called once per frame
    void Update()
    {
        isf.current_tick += 1;

        isf.ShroedingerIntegration(ref psi1, ref psi2);
        isf.Normalize(ref psi1, ref psi2);
        isf.PressureProject(ref psi1, ref psi2);

        InitilizeNozzlePsi(ref psi1, ref psi2);
        // UpdateObstacle();

        isf.VelocityOneForm(ref psi1, ref psi2, isf.hbar);
        isf.ReprojectToGrid();

        particles.Emulate();
        ClampParticles();

        particles.DoRender();
    }

    void OnDestroy()
    {
        if(psi1) psi1.Release();
        if(psi2) psi2.Release();
    }
}
