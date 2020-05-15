using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FFT : MonoBehaviour
{
    public ComputeShader CS;
    // public RenderTexture InputRT;
    public RenderTexture OutputRT;

    public Material InputRTMat;

    int kernelFFTHorizontal;
    int kernelFFTVertical;

    int kernelIFFTHorizontal;
    int kernelIFFTVertical;
    int kernelIFFTVerticalEnd;
    int kernelIFFTHorizontalEnd;
    int kernelIFFTFlip;

    int kernelBlitIORT;
    int kernelBlitDebugOutput;

    int N;
    int FFTPow;

    void Blit2DToDebug(RenderTexture texture, RenderTexture dbg)
    {
        CS.SetTexture(kernelBlitDebugOutput, "InputRT", texture);
        CS.SetTexture(kernelBlitDebugOutput, "DebugOutput", dbg);
        CS.Dispatch(kernelBlitDebugOutput, texture.width / 8, texture.height / 8, 1);
    }

    static RenderTexture CreateRenderTexture(int size, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, type);
        rt.enableRandomWrite = true;
        // rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    /*void RunComputeShader()
    {
        CS.Dispatch(kernelSampleSimplexFBMNoise, ChunkDivisions / 8, ChunkDivisions / 8, 1);
    }*/

    public void fft(ref RenderTexture InputRT)
    {
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTHorizontal, ref InputRT);
        }

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTVertical, ref InputRT);
        }
    }

    public void ifft(ref RenderTexture InputRT)
    {
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow)
            {
                ComputeFFT(kernelIFFTHorizontal, ref InputRT);
            }
            else
            {
                ComputeFFT(kernelIFFTHorizontalEnd, ref InputRT);
            }
        }

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow)
            {
                ComputeFFT(kernelIFFTVertical, ref InputRT);
            }
            else
            {
                ComputeFFT(kernelIFFTVerticalEnd, ref InputRT);
            }
        }
    }

    
    //TODO: 理论上这应该和 iff() 一样, but 有 bug,,不知道为啥, 留待修复orz 并且 benchmark
    public void ifft_a(ref RenderTexture InputRT)
    {
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTHorizontal, ref InputRT);
        }

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTVertical, ref InputRT);
        }

        ComputeFFT(kernelIFFTFlip, ref InputRT);
    }

    public void init()
    {
        kernelFFTHorizontal = CS.FindKernel("FFTHorizontal");
        kernelFFTVertical = CS.FindKernel("FFTVertical");

        kernelIFFTHorizontal = CS.FindKernel("IFFTHorizontal");
        kernelIFFTVertical = CS.FindKernel("IFFTVertical");
        kernelIFFTHorizontalEnd = CS.FindKernel("IFFTHorizontalEnd");
        kernelIFFTVerticalEnd = CS.FindKernel("IFFTVerticalEnd");
        kernelBlitIORT = CS.FindKernel("BlitIORT");
        kernelBlitDebugOutput = CS.FindKernel("BlitDebugOutput");
        kernelIFFTFlip = CS.FindKernel("IFFTFlip");
    }

    void ComputeFFT(int kernel, ref RenderTexture input)
    {
        CS.SetTexture(kernel, "InputRT", input);
        CS.SetTexture(kernel, "OutputRT", OutputRT);

        CS.Dispatch(kernel, N / 8, N / 8, 1);
        RenderTexture rt = input;
        input = OutputRT;
        OutputRT = rt;
    }

    Color[] ReadRenderTexture(RenderTexture t, TextureFormat fmt)
    {
        var lastRT = RenderTexture.active;
        RenderTexture.active = t;
        Rect rect = new Rect(0, 0, t.width, t.height);
        Texture2D tmp_texture_2d = new Texture2D(t.width, t.height, fmt, false);
        //tmp_texture_2d.GetPixelData<float>(0);
        tmp_texture_2d.ReadPixels(rect, 0, 0);

        var res = tmp_texture_2d.GetPixels(0, 0, t.width, t.height);
        RenderTexture.active = lastRT;

        return res;
    }

    Unity.Collections.NativeArray<float> ReadRenderTextureRaw(RenderTexture t, TextureFormat fmt)
    {
        var lastRT = RenderTexture.active;
        RenderTexture.active = t;
        Texture2D tmp_texture_2d = new Texture2D(t.width, t.height, fmt, false);
        // Graphics.ConvertTexture()
        //tmp_texture_2d.Apply();
        //Graphics.CopyTexture(t, tmp_texture_2d);
        tmp_texture_2d.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
        var res  = tmp_texture_2d.GetPixelData<float>(0);
        RenderTexture.active = lastRT;
        return res;
    }

    struct ComplexArray
    {
        public float[,] real;
        public float[,] imag;
    };

    [System.Serializable]
    struct InputType
    {
        public ComplexArray data;
    }

    void ExportArray(RenderTexture t, TextureFormat fmt, int channels, string filename)
    {
        var width = t.width;
        var heigh = t.height;
        
        var colors = ReadRenderTextureRaw(t, fmt);
        var index = 0;

        InputType ipt;
        ipt.data.real = new float[N, N];
        ipt.data.imag = new float[N, N];

        //for(var ch = 0;  ch < channels; ++ch)
        //{
            // System.Func<Color, float> get_con = (Color color) => color.a;
            // if (ch == 0) get_con = (Color color) => color.r;
            // if (ch == 1) get_con = (Color color) => color.g;
            // if (ch == 2) get_con = (Color color) => color.b;


            for (int i = 0; i < heigh; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                //ipt.data.real[i, j] = colors[index].r; 
                //ipt.data.imag[i, j] = colors[index].g;

                ipt.data.real[i, j] = colors[index];
                ++index;
                ipt.data.imag[i, j] = colors[index];
                // var r = get_con(colors[index]);
                // row.Add(r);
                ++index;
                ++index;
                ++index;
            }
            }
            
        //}

        var chh = Newtonsoft.Json.JsonConvert.SerializeObject(ipt);
        File.WriteAllText(filename, chh);
    }


    /*
    void RunTest()
    {
        var txt = File.ReadAllText("test/input.json");
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<InputType>(txt);
        N = Mathf.FloorToInt(Mathf.Sqrt(input.data.real.Length));

        var textureIn = CreateRenderTexture(N);
        var textureOut = CreateRenderTexture(N);
        OutputRT = textureOut;

        //float[] buf = new float[N * N * 2];
        //int index = 0;
        //for(int i =0;i < N;++i)
        //{
        //    for(int j = 0; j < N;++j)
        //    {
        //        buf[index] = input.data.real[i, j]; 
        //        ++index;
        //        buf[index] = input.data.imag[i, j]; 
        //        ++index;
        //    }
        //}

        //var buffer = new ComputeBuffer(N * N, 2 * 4);
        //buffer.SetData(buf);
        //CS.SetBuffer(kernelBlitIORT, "InputRT", buffer);
        //CS.SetTexture(kernelBlitIORT, "OutputRT", textureIn);
        //CS.Dispatch(kernelBlitIORT, N / 8, N / 8, 1);

        //Texture2D texture = new Texture2D(N, N, TextureFormat.RGFloat, false);
        //RenderTexture.active = textureIn;
        //// texture.ReadPixels(new Rect(0, 0, N, N), 0, 0);
        //
        //texture.SetPixelData(buf, 0);
        //texture.Apply(updateMipmaps: false);
        //RenderTexture.active = null;


        Texture2D texture = new Texture2D(N, N, TextureFormat.RGBAFloat, false);
        RenderTexture.active = textureIn;
        texture.ReadPixels(new Rect(0, 0, N, N), 0, 0);
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                texture.SetPixel(i, j, new Color(input.data.real[i, j] * 100, input.data.imag[i, j]*100, input.data.real[i, j], input.data.imag[i, j]));
            }
        };

        texture.Apply(updateMipmaps: false);
        RenderTexture.active = null;
        InputRTMat.SetTexture("_MainTex", textureIn);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/last_input.json");
        
        fft(ref textureIn);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result.json");
        Debug.Log(input);
    }*/
    
    void RunTest()
    {
        var txt = File.ReadAllText("test/input.json");
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<InputType>(txt);
        N = Mathf.FloorToInt(Mathf.Sqrt(input.data.real.Length));
        FFTPow = Mathf.FloorToInt(Mathf.Log(N, 2));
        CS.SetInt("N", N);
        
        var textureIn = CreateRenderTexture(N, RenderTextureFormat.RGFloat);
        var textureOut = CreateRenderTexture(N, RenderTextureFormat.RGFloat);
        OutputRT = textureOut;

        RenderTexture DebugOutput = CreateRenderTexture(N);
        Texture2D texture = new Texture2D(N, N, TextureFormat.RGFloat, false);
        RenderTexture.active = textureIn;
        var pxdata = texture.GetPixelData<float>(0);
        int index = 0;
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                pxdata[index] = input.data.real[i, j];
                ++index;
                pxdata[index] = input.data.imag[i, j];
                ++index;
                // ++index;
                // ++index;
                // texture.SetPixel(i, j, new Color(input.data.real[i, j], input.data.imag[i, j], input.data.real[i, j], input.data.imag[i, j]));
            }
        };
        texture.Apply(updateMipmaps: false);
        RenderTexture.active = null;
        Graphics.Blit(texture, textureIn);
        InputRTMat.SetTexture("_MainTex", textureIn);
        Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(DebugOutput, TextureFormat.RGBAFloat, 2, "test/last_input.json");
        

        fft(ref textureIn);
        Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(DebugOutput, TextureFormat.RGBAFloat, 2, "test/result.json");

        Graphics.Blit(texture, textureIn);
        ifft(ref textureIn);
        Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(DebugOutput, TextureFormat.RGBAFloat, 2, "test/result_ifft.json");

        Debug.Log(input);
    }

    void Start()
    {
        init();
        RunTest();

    }
}
