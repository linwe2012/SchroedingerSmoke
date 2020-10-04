using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using System.IO;


public class Benchmark : MonoBehaviour
{
    public ISF isf;
    FFT fft;

    RenderTexture psi1;
    RenderTexture psi2;

    struct BenchmarkSingleResult
    {
        public double milli;
        public int Nx, Ny, Nz;
        public int scale;
    }

    class BenchmarkResult
    {
        public List<BenchmarkSingleResult> results;

        public BenchmarkResult()
        {
            results = new List<BenchmarkSingleResult>();
        }
    }

    BenchmarkResult result;

    void TakeTestN(Vector3Int N)
    {
        isf.N = N;

        
        isf.InitISF();

        psi1 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        psi2 = FFT.CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        isf.InitializePsi(ref psi1, ref psi2);

        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();

        for (int i = 0; i < 5; ++i)
        {
            isf.current_tick += 1;

            isf.ShroedingerIntegration(ref psi1, ref psi2);
            isf.Normalize(ref psi1, ref psi2);
            isf.PressureProject(ref psi1, ref psi2);
            fft.ExportComplex3D(psi1, null);
            Debug.Log("  At: " + i.ToString());
        }
        
        watch.Stop();
        BenchmarkSingleResult ben = new BenchmarkSingleResult();
        ben.milli = watch.ElapsedMilliseconds / 5.0;
        ben.Nx = N.x;
        ben.Ny = N.y;
        ben.Nz = N.z;
        ben.scale = N.x * N.y * N.z;

        result.results.Add(ben);
    }

    void Start()
    {
        result = new BenchmarkResult();

        fft = isf.fft;
        fft.init();

        isf.InitComputeShader();

        for (int i = 3; i < 10; ++i)
        {
            for (int j = i; j < 10; ++j)
            {
                for (int k = j; k < 10; ++k)
                {
                    int s = 1 << i;
                    int l = 1 << j;
                    int m = 1 << k;
                    TakeTestN(new Vector3Int(s, l, m));
                }
            }
        }

        //for (int i = 8; i < 9; ++i)
        //{
        //    int k = 1 << i;
        //    TakeTestN(new Vector3Int(k, k, k));
        //    Debug.Log(k.ToString() + " Done");
        //}

        var ch = Newtonsoft.Json.JsonConvert.SerializeObject(result);
        File.WriteAllText("test/bench.isf.json", ch);
    }

    void Update()
    {

    }

}

