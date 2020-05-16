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
    int kernelFFTDepth;

    int kernelIFFTHorizontal;
    int kernelIFFTVertical;
    int kernelIFFTDepth;

    int kernelIFFTVerticalEnd;
    int kernelIFFTHorizontalEnd;
    int kernelIFFTDepthEnd;

    int kernelIFFTFlip;

    int kernelBlitIOTex;
    int kernelBlitIORT;
    int kernelBlitDebugOutput;

    int kernelBlitSliceOf3DTexture;

    int N;
    int FFTPow;

    void Blit2DToDebug(RenderTexture texture, RenderTexture dbg)
    {
        CS.SetTexture(kernelBlitDebugOutput, "InputRT", texture);
        CS.SetTexture(kernelBlitDebugOutput, "DebugOutput", dbg);
        int z = 1;
        if(texture.dimension == UnityEngine.Rendering.TextureDimension.Tex3D)
        {
            z = texture.volumeDepth / 8;
            if(z == 0)
            {
                z = 1;
            }
        }
        CS.Dispatch(kernelBlitDebugOutput, texture.width / 8, texture.height / 8, z);
    }

    static RenderTexture CreateRenderTexture(int size, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, type);
        rt.enableRandomWrite = true;
        // rt.useMipMap = false;
        rt.Create();
        return rt;
    }
    static RenderTexture CreateRenderTexture3D(int width, int height, int volumn, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, type);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = volumn;
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

        //进行深度FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTDepth, ref InputRT);
        }
    }

    public void Blit3D(Texture3D texture, RenderTexture rt)
    {
        CS.SetTexture(kernelBlitIOTex, "InputTex", texture);
        CS.SetTexture(kernelBlitIOTex, "OutputRT", rt);
        CS.Dispatch(kernelBlitIOTex, N / 8, N / 8, N / 8);
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

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow)
            {
                ComputeFFT(kernelIFFTDepth, ref InputRT);
            }
            else
            {
                ComputeFFT(kernelIFFTDepthEnd, ref InputRT);
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
        kernelFFTDepth = CS.FindKernel("FFTDepth");

        kernelIFFTHorizontal = CS.FindKernel("IFFTHorizontal");
        kernelIFFTVertical = CS.FindKernel("IFFTVertical");
        kernelIFFTDepth = CS.FindKernel("IFFTDepth");

        kernelIFFTHorizontalEnd = CS.FindKernel("IFFTHorizontalEnd");
        kernelIFFTVerticalEnd = CS.FindKernel("IFFTVerticalEnd");
        kernelIFFTDepthEnd = CS.FindKernel("IFFTDepthEnd");

        kernelBlitIORT = CS.FindKernel("BlitIORT");
        kernelBlitDebugOutput = CS.FindKernel("BlitDebugOutput");
        kernelIFFTFlip = CS.FindKernel("IFFTFlip");

        kernelBlitIOTex = CS.FindKernel("BlitIOTex");

        kernelBlitSliceOf3DTexture = CS.FindKernel("BlitSliceOf3DTexture");
    }

    void ComputeFFT(int kernel, ref RenderTexture input)
    {
        CS.SetTexture(kernel, "InputRT", input);
        CS.SetTexture(kernel, "OutputRT", OutputRT);

        CS.Dispatch(kernel, N / 8, N / 8, N / 8);
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

    float[] ReadRenderTextureRaw3D(RenderTexture t, TextureFormat fmt)
    {
        var res = new float[t.width * t.width * t.volumeDepth*4];

        RenderTexture tmp = CreateRenderTexture(t.width, RenderTextureFormat.ARGBFloat);
        CS.SetTexture(kernelBlitSliceOf3DTexture, "InputRT", t);
        CS.SetTexture(kernelBlitSliceOf3DTexture, "Sliced", tmp);
        for(int i = 0; i < t.volumeDepth; ++i)
        {
            CS.SetInt("layer", i);
            CS.Dispatch(kernelBlitSliceOf3DTexture, t.width / 8, t.width / 8, 1);
            var k = ReadRenderTextureRaw(tmp, TextureFormat.RGBAFloat);
            // k.CopyTo(res, t.width * t.width * i);
            System.Array.Copy(k, 0, res, t.width * t.width * i, k.Length);
            
        }
        return res;
    }

    float[] ReadRenderTextureRaw(RenderTexture t, TextureFormat fmt)
    {
        if (t.dimension == UnityEngine.Rendering.TextureDimension.Tex3D) return ReadRenderTextureRaw3D(t, fmt);

        var lastRT = RenderTexture.active;
        RenderTexture.active = t;
        Texture2D tmp_texture_2d = new Texture2D(t.width, t.height, fmt, false);
        // Graphics.ConvertTexture()
        //tmp_texture_2d.Apply();
        Graphics.CopyTexture(t, tmp_texture_2d);
        tmp_texture_2d.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
        
        var res  = tmp_texture_2d.GetPixelData<float>(0);
        var arr = new float[res.Length];
        res.CopyTo(arr);
        RenderTexture.active = lastRT;
        return arr;
    }

    struct ComplexArray
    {
        public float[,,] real;
        public float[,,] imag;
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
        var depth = t.volumeDepth;

        var colors = ReadRenderTextureRaw(t, fmt);
        var index = 0;

        InputType ipt;
        ipt.data.real = new float[depth, heigh, width];
        ipt.data.imag = new float[depth, heigh, width];

        //for(var ch = 0;  ch < channels; ++ch)
        //{
        // System.Func<Color, float> get_con = (Color color) => color.a;
        // if (ch == 0) get_con = (Color color) => color.r;
        // if (ch == 1) get_con = (Color color) => color.g;
        // if (ch == 2) get_con = (Color color) => color.b;

        for (int k = 0; k < depth; ++k)
        {
            for (int i = 0; i < heigh; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                    
                        ipt.data.real[k, i, j] = colors[index];
                        ++index;
                        ipt.data.imag[k, i, j] = colors[index];
                        ++index;
                        ++index;
                        ++index;
                    
                }
                
            }
        }

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
    
    void myRunTest()
    {
        var txt = File.ReadAllText("test/input.json");
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<InputType>(txt);
        //N = Mathf.FloorToInt(Mathf.Pow(input.data.real.Length, ));
        N = 16;
        FFTPow = Mathf.FloorToInt(Mathf.Log(N, 2));
        CS.SetInt("N", N);
        
        var textureIn = CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
        var textureOut = CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
        OutputRT = textureOut;

        Texture3D texture = new Texture3D(N, N, N, TextureFormat.RGFloat, false);
        RenderTexture.active = textureIn;
        var pxdata = texture.GetPixelData<float>(0);
        int index = 0;
        for(int k = 0; k < N; ++k)
        {
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {

                    pxdata[index] = input.data.real[k, i, j];
                    ++index;
                    pxdata[index] = input.data.imag[k, i, j];
                    ++index;
                    // ++index;
                    // ++index;
                    // texture.SetPixel(i, j, new Color(input.data.real[i, j], input.data.imag[i, j], input.data.real[i, j], input.data.imag[i, j]));
                }
            };
        }
        
        texture.Apply(updateMipmaps: false);
        RenderTexture.active = null;
        Blit3D(texture, textureIn);
        // Graphics.Blit(texture, textureIn);
        InputRTMat.SetTexture("_MainTex", textureIn);
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/last_input.json");
        

        fft(ref textureIn);
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result.json");


        Blit3D(texture, textureIn);
        //Graphics.Blit(texture, textureIn);
        ifft(ref textureIn);
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result_ifft.json");

        Debug.Log(input);
    }

    void Start()
    {
        init();
        myRunTest();

    }
}
