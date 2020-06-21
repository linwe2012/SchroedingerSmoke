using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

class Jet
{
    public Vector3 nozzle_center = new Vector3(4, 8, 8);
    public Vector3 nozzle_right;
    public Vector3 nozzle_dir = new Vector3(1, 0, 0);
    public Vector3 nozzle_velocity = new Vector3(1, 0, 0);
    
    Vector3 nozzle_up;
    Vector3 nozzle_relative_center;
    Vector3 nozzle_center_in_shader;
    Vector3 topleft;

    
    

    public enum Constants {
        ThreadMutiples = 8
    }

    public float nozzle_radius = 3;
    public float nozzle_length = 3;

    RenderTexture NozzleRT;
    ComputeShader CS;
    ISF isf;

    int kernelCreateNozzleMask;
    int kernelNozzleUpdatePsi;
    


    public void Initialize(ComputeShader _CS, ISF _isf)
    {
        CS = _CS;
        isf = _isf;

        kernelCreateNozzleMask = CS.FindKernel("CreateNozzleMask");
        kernelNozzleUpdatePsi = CS.FindKernel("NozzleUpdatePsi");

    }

    public void DispatchCS(int kernel)
    {
        CS.Dispatch(kernel, NozzleRT.width / 8, NozzleRT.height / 8, NozzleRT.volumeDepth / 8);
    }

    public void ResetParameters(MinMaxVec mm)
    {
        ComputeDirectionVectors();

        Vector3Int box, box_center;
        mm.GetRenderTextureBoundingBox((int)Constants.ThreadMutiples, out box, out box_center);
        if (NozzleRT && NozzleRT.IsCreated())
        {
            NozzleRT.Release();
        }

        NozzleRT = FFT.CreateRenderTexture3D(box.x, box.y, box.z, RenderTextureFormat.RFloat);

        topleft = ISFUtils.IntToFloat(box_center) - ISFUtils.IntToFloat(box) / 2;
        if (topleft.x < 0 || topleft.y < 0 || topleft.z < 0)
        {
            Debug.LogError("The bounding box excceeds");
        }

        nozzle_relative_center = ISFUtils.IntToFloat(box) / 2;
        nozzle_center_in_shader = ISFUtils.IntToFloat(box_center);

        SetCSData();

        CS.SetTexture(kernelCreateNozzleMask, "Nozzle", NozzleRT);
        DispatchCS(kernelCreateNozzleMask);
    }

    void SetCSData()
    {
        CS.SetVector("nozzle_ralative_center", nozzle_relative_center);
        CS.SetVector("nozzle_center", nozzle_center_in_shader);
        CS.SetFloat("nozzle_radius", nozzle_radius);
        CS.SetVector("nozzle_dir", nozzle_dir);
        CS.SetVector("nozzle_topleft", topleft);
        CS.SetFloat("nozzle_length", nozzle_length);
        CS.SetVector("nozzle_velocity", nozzle_velocity / isf.hbar);
        CS.SetVector("nozzle_right", nozzle_right);
        CS.SetVector("nozzle_up", nozzle_up);
        CS.SetVector("size", isf.size);
        CS.SetInts("res", isf.GetGrids());
        CS.SetInts("grids", isf.GetGrids());
    }

    void ComputeDirectionVectors()
    {
        nozzle_dir.Normalize();
        if (nozzle_dir == Vector3.up)
        {
            nozzle_dir.x += 0.01f;
        }
        nozzle_right = Vector3.Cross(nozzle_dir, Vector3.up);
        nozzle_right.Normalize();

        nozzle_up = Vector3.Cross(nozzle_dir, nozzle_right);
        nozzle_up.Normalize();
    }


    public void ResetParameters()
    {
        ComputeDirectionVectors();

        var mm = MinMaxVec.Create();

        mm.Feed(nozzle_center + nozzle_dir * nozzle_length / 2);
        mm.Feed(nozzle_center - nozzle_dir * nozzle_length / 2);

        mm.Feed(nozzle_center + nozzle_right * nozzle_radius);
        mm.Feed(nozzle_center - nozzle_right * nozzle_radius);

        mm.Feed(nozzle_center + nozzle_up * nozzle_radius);
        mm.Feed(nozzle_center - nozzle_up * nozzle_radius);

        ResetParameters(mm);
    }


    public void UpdatePsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        CS.SetTexture(kernelNozzleUpdatePsi, "Psi1", psi1);
        CS.SetTexture(kernelNozzleUpdatePsi, "Psi2", psi2);
        CS.SetTexture(kernelNozzleUpdatePsi, "Nozzle", NozzleRT);

        SetCSData();

        var volecity2 = Vector3.Dot(nozzle_velocity, nozzle_velocity);
        float omega = volecity2 / (2 * isf.hbar);
        CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);

        DispatchCS(kernelNozzleUpdatePsi);

        isf.PressureProject(ref psi1, ref psi2);
    }
};


public class InkCollison : MonoBehaviour
{
    public ISF isf;
    public FFT fft;
    public ParticleMan particles;
    public ComputeShader CS;
    public ComputeShader NozzleCS;


    public GameObject LeftObject;
    public GameObject RightObject;
    public bool DisableObjects = true;
    public int LevelOfFills = 10;
    public int Duplicates = 2;
    public AnimationCurve FillCurve;
    public float HBar = 0.2f;

    List<Jet> jets = new List<Jet>();


    int[] kernelClamp;
    int[] kernelInitPsiMask;
    
    RenderTexture psi1;
    RenderTexture psi2;
    RenderTexture psi_mask;


    public Vector3 Velocity = new Vector3(1, 0, 0);
    public float VelocityScale = 1.67f;
    public Vector3 transa = new Vector3(10, 20, 20);
    public Vector3 transb = new Vector3(10, 20, 20);
    public float InvDt = 24;

    int kernelFlushPsiMask;
    int kernelInitInkCollisionPsi;
    int kernelUpdatePsiGlobal;

    public void InitComputeShader()
    {
        kernelClamp = new int[(int)GPUThreads.T_KINDS];

        ParticleMan.InitMultiKindKernels(CS, "Clamp", out kernelClamp);
        ParticleMan.InitMultiKindKernels(CS, "InitPsiMask", out kernelInitPsiMask);

        kernelFlushPsiMask = CS.FindKernel("FlushPsiMask");
        kernelInitInkCollisionPsi = CS.FindKernel("InitInkCollisionPsi");
        kernelUpdatePsiGlobal = CS.FindKernel("UpdatePsiGlobal");

        CS.SetVector("size", isf.size);
        int[] res = isf.GetGrids();
        CS.SetInts("res", res);
        CS.SetInts("grids", res);
    }

    public void DispatchCS(int kernel)
    {
        var N = isf.N;
        CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
    }



    List<Vector4> LoadMesh(GameObject obj, int fill, int dup, float dir, bool generate_jet, Vector3 trans, out Jet jet)
    {
        var mesh = obj.GetComponentInChildren<MeshFilter>();
        var vertices = mesh.mesh.vertices;
        
        var vres = new List<Vector4>();
        var last_scale = obj.transform.localScale;
        var pos = obj.transform.position;
        var pos4 = new Vector4(pos.x, pos.y, pos.z, 0);
        // var minmax = MinMaxVec.Create();

        for (int j = 0; j < fill; ++j)
        {
            obj.transform.localScale = last_scale * FillCurve.Evaluate(((float)j) / fill);
            
            var mat = obj.transform.localToWorldMatrix;
            for(int k = 0; k < dup; ++k)
            {
                for (int i = 0; i < vertices.Length; ++i)
                {
                    if (Random.Range(0, j + 1) == j)
                    {
                        var c = mat.MultiplyVector(vertices[i]) ;
                        // var c = obj.transform.TransformVector(vertices[i]) + trans;
                        var res = new Vector4(c.x, c.y, c.z, 1);
                        res += pos4;
                        // res += new Vector4(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0);
                        // minmax.Feed(new Vector3(res.x, res.y, res.z));
                        vres.Add(res);
                    }
                }
            }
        }
        obj.transform.localScale = last_scale;

        if (DisableObjects)
        {
            obj.SetActive(false);
        }

        if(generate_jet)
        {
            jet = new Jet();
            // minmax.GetRenderTextureBoundingBox((int)Jet.Constants.ThreadMutiples, out Vector3Int size, out Vector3Int center);
            // jet.nozzle_radius = ISFUtils.IntToFloat(size).x * 0.4f;
            var N = isf.GetGrids();
            jet.nozzle_radius = N[1] / 4;
            jet.nozzle_length = N[1] / 4;
            jet.nozzle_center = new Vector3(dir > 0 ? 4 : N[0] - 4 - 4, N[1] / 2, N[2] / 2);
            jet.nozzle_velocity = new Vector3(dir, 0, 0);
            jet.nozzle_dir = new Vector3(dir, 0, 0);
            jet.Initialize(NozzleCS, isf);
            // jet.ResetParameters(minmax);
            jet.ResetParameters();
        }
        else
        {
            jet = null;
        }
        return vres;
    }

    void UpdateGlabalMaskedPsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        CS.SetTexture(kernelUpdatePsiGlobal, "Psi1", psi1);
        CS.SetTexture(kernelUpdatePsiGlobal, "Psi2", psi2);
        CS.SetTexture(kernelUpdatePsiGlobal, "PsiMask", psi_mask);

        var velo = new Vector3(VelocityScale, 0, 0);
        var volecity2 = Vector3.Dot(velo, velo);
        float omega = volecity2 / (2 * isf.hbar);
        CS.SetFloat("omega_t", omega * isf.current_tick * isf.estimate_dt);
        CS.SetVector("velocity", velo);

        DispatchCS(kernelUpdatePsiGlobal);

        // isf.PressureProject(ref psi1, ref psi2);
    }

    void ComputePsiMask()
    {
        foreach (var part in particles.AllCurrentParticles())
        {
            var kern = kernelInitPsiMask[part.kernId];
            CS.SetTexture(kern, "PsiMask", psi_mask);
            CS.SetBuffer(kern, "ParticlePostion", part.buf);
            if (part.groud_id == 0)
            {
                CS.SetFloat("direction", -VelocityScale);
            }
            else
            {
                CS.SetFloat("direction", VelocityScale);
            }

            CS.Dispatch(kern, part.count / part.threads, 1, 1);
        }
    }

    void InitilizeParticles()
    {
        Jet ljet, rjet;

        var lmesh = LoadMesh(LeftObject, LevelOfFills, Duplicates, -VelocityScale,  false, transa, out ljet);
        var rmesh = LoadMesh(RightObject, LevelOfFills, Duplicates, VelocityScale, false, transb, out rjet);

        particles.GroupInit(isf);
        particles.AddGroup(new Vector4(1, 0, 0, 0), lmesh.ToArray());
        particles.AddGroup(new Vector4(0, 0, 1, 0), rmesh.ToArray());

        CS.SetTexture(kernelFlushPsiMask, "PsiMask", psi_mask);
        DispatchCS(kernelFlushPsiMask);

        ComputePsiMask();

        for (int i = 0; i < 100; ++i)
        {
            UpdateGlabalMaskedPsi(ref psi1, ref psi2);
            isf.PressureProject(ref psi1, ref psi2);
        }
        // UpdateGlabalMaskedPsi(ref psi1, ref psi2);
        // isf.PressureProject(ref psi1, ref psi2);
        //jets.Add(ljet);
        //jets.Add(rjet);
        //
        //foreach (var jet in jets)
        //{
        //    for(int i = 0; i < 10; ++i)
        //    {
        //        jet.UpdatePsi(ref psi1, ref psi2);
        //    }
        //}

        // fft.ExportComplex3D(psi1, "test/ink.ps1.json");
    }

    void UpdatePsi(ref RenderTexture psi1, ref RenderTexture psi2)
    {
        foreach (var jet in jets)
        {
            jet.UpdatePsi(ref psi1, ref psi2);
        }
    }

    void Prepare()
    {
        fft = isf.fft;
        fft.init();

        isf.hbar = HBar;
        isf.estimate_dt = 1.0f / InvDt;

        isf.InitComputeShader();
        isf.InitISF();

        

        InitComputeShader();

        var N = isf.N;

        psi1 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        psi2 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        psi_mask = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RFloat);

        isf.InitializePsi(ref psi1, ref psi2);



        //for(int i = 0; i < 10; ++i)
        //{
            //UpdatePsi(ref psi1, ref psi2);
        //}

        //fft.ExportComplex3D(psi1, "test/ink.psi1.json");
        InitilizeParticles();
    }

    void Clamp()
    {
        foreach (var part in particles.AllCurrentParticles())
        {
            int kernel = kernelClamp[part.kernId];
            // CS.SetInt("rng_state", Random.Range(0, 999999));
            CS.SetBuffer(kernel, "ParticlePostion", part.buf);
            CS.Dispatch(kernel, part.count / part.threads, 1, 1);
        }
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

        //DispatchCS(kernelFlushPsiMask);
        //ComputePsiMask();
        //UpdateGlabalMaskedPsi(ref psi1, ref psi2);
        UpdateGlabalMaskedPsi(ref psi1, ref psi2);
        // UpdatePsi(ref psi1, ref psi2);

        isf.VelocityOneForm(ref psi1, ref psi2, isf.hbar);
        isf.ReprojectToGrid();

        particles.Emulate();
        // Clamp();

        particles.DoRender();
    }

    private void OnDestroy()
    {
        if (psi1) psi1.Release();
        if (psi2) psi2.Release();
    }
}
