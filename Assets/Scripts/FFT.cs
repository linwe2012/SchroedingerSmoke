using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

// TODO: 目前 FFT 只支持 N*N*N 的三维 fft

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

    int kernelFFTHorizontal_IEND;
    int kernelFFTVertical_IEND;
    int kernelFFTDepth_IEND;

    int kernelIFFTFlip;
    int kernelFFTShift;

    int kernelBlitIOTex;
    int kernelBlitIORT;
    int kernelBlitDebugOutput;
    int kernelIFFTConj;
    int kernelBlitIOTexFloat4;



    int kernelBlitSliceOf3DTexture;
    int kernelBlitSliceOf3DTextureFloat4;
    int kernelBlitSliceOf3DTextureFloat1;


    Vector3Int N = new Vector3Int();
    Vector3Int FFTPow = new Vector3Int();

    public void SetN(Vector3Int n)
    {
        N = n;
        FFTPow[0] = Mathf.FloorToInt(Mathf.Log(N[0], 2));
        FFTPow[1] = Mathf.FloorToInt(Mathf.Log(N[1], 2));
        FFTPow[2] = Mathf.FloorToInt(Mathf.Log(N[2], 2));
        // CS.SetInt("N", N);
    }
    
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

    // Graphics.Blit 貌似支持 2D Texture
    public void Blit3D(Texture3D texture, RenderTexture rt)
    {
        switch(rt.format)
        {
            case RenderTextureFormat.ARGBFloat:
                CS.SetTexture(kernelBlitIOTexFloat4, "InputTex3DFloat4", texture);
                CS.SetTexture(kernelBlitIOTexFloat4, "InputFloat4RT", rt);
                CS.Dispatch(kernelBlitIOTexFloat4, rt.width / 8, rt.height / 8, rt.volumeDepth / 8);
                break;

            case RenderTextureFormat.RGFloat:
            default:
                CS.SetTexture(kernelBlitIOTex, "InputTex", texture);
                CS.SetTexture(kernelBlitIOTex, "OutputRT", rt);
                CS.Dispatch(kernelBlitIOTex, rt.width / 8, rt.height / 8, rt.volumeDepth / 8);
            break;

        }
        
    }

    static public RenderTexture CreateRenderTexture(int size, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, type);
        rt.enableRandomWrite = true;
        // rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    static public RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, type);
        rt.enableRandomWrite = true;
        // rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    static public RenderTexture CreateRenderTexture1D(int size, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(size, 1, 0, type);
        rt.enableRandomWrite = true;
        // rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    static public RenderTexture CreateRenderTexture3D(int width, int height, int volumn, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
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

    public void fft(ref RenderTexture InputRT, ref RenderTexture Output)
    {
        CS.SetInt("N", N[0]);
        //进行横向FFT
        for (int m = 1; m <= FFTPow[0]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTHorizontal, ref InputRT, ref Output);
        }

        //进行纵向FFT
        CS.SetInt("N", N[1]);
        for (int m = 1; m <= FFTPow[1]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTVertical, ref InputRT, ref Output);
        }

        //进行深度FFT
        CS.SetInt("N", N[2]);
        for (int m = 1; m <= FFTPow[2]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTDepth, ref InputRT, ref Output);
        }
    }

    public void fftshift(ref RenderTexture input, ref RenderTexture Output)
    {
        int[] N3D = { N[0], N[1], N[2] };
        CS.SetInts("N3D", N3D);
        ComputeFFT(kernelFFTShift, ref input, ref Output);
    }

    [System.Obsolete("This implemenation is possibly buggy")]
    public void ifft_c(ref RenderTexture InputRT)
    {
        CS.SetInt("N", N[0]);
        //进行横向FFT
        for (int m = 1; m <= FFTPow[0]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[0])
            {
                ComputeFFT(kernelIFFTHorizontal, ref InputRT);
            }
            else
            {
                ComputeFFT(kernelIFFTHorizontalEnd, ref InputRT);
            }
        }

        //进行纵向FFT
        CS.SetInt("N", N[1]);
        for (int m = 1; m <= FFTPow[1]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[1])
            {
                ComputeFFT(kernelIFFTVertical, ref InputRT);
            }
            else
            {
                ComputeFFT(kernelIFFTVerticalEnd, ref InputRT);
            }
        }

        //进行纵向FFT
        CS.SetInt("N", N[2]);
        for (int m = 1; m <= FFTPow[2]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[2])
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
    [System.Obsolete("This implemenation is possibly buggy")]
    public void ifft_a(ref RenderTexture InputRT)
    {
        CS.SetInt("N", N[0]);
        //进行横向FFT
        for (int m = 1; m <= FFTPow[0]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTHorizontal, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        //进行纵向FFT
        CS.SetInt("N", N[1]);
        for (int m = 1; m <= FFTPow[1]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTVertical, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        //进行纵向FFT
        CS.SetInt("N", N[2]);
        for (int m = 1; m <= FFTPow[2]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTDepth, ref InputRT);
        }

        ComputeFFT(kernelIFFTFlip, ref InputRT);
    }

    public void ifft_b(ref RenderTexture InputRT)
    {
        CS.SetInt("N", N[0]);
        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行横向FFT
        for (int m = 1; m <= FFTPow[0]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTHorizontal, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行纵向FFT
        CS.SetInt("N", N[1]);
        for (int m = 1; m <= FFTPow[1]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTVertical, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行纵向FFT
        CS.SetInt("N", N[2]);
        for (int m = 1; m <= FFTPow[2]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTDepth, ref InputRT);
        }

        ComputeFFT(kernelIFFTFlip, ref InputRT);
    }


    public void ifft(ref RenderTexture InputRT, ref RenderTexture Output)
    {
        

        ComputeFFT(kernelIFFTConj, ref InputRT, ref Output);
        CS.SetInt("N", N[0]);
        //进行横向FFT
        for (int m = 1; m <= FFTPow[0]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[0])
            {
                ComputeFFT(kernelFFTHorizontal, ref InputRT, ref Output);
            }
            else
            {
                ComputeFFT(kernelFFTHorizontal_IEND, ref InputRT, ref Output);
            }
        }

        CS.SetInt("N", N[1]);
        //进行纵向FFT
        for (int m = 1; m <= FFTPow[1]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[1])
            {
                ComputeFFT(kernelFFTVertical, ref InputRT, ref Output);
            }
            else
            {
                ComputeFFT(kernelFFTVertical_IEND, ref InputRT, ref Output);
            }
        }

        CS.SetInt("N", N[2]);
        //进行纵向FFT
        for (int m = 1; m <= FFTPow[2]; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow[2])
            {
                ComputeFFT(kernelFFTDepth, ref InputRT, ref Output);
            }
            else
            {
                ComputeFFT(kernelFFTDepth_IEND, ref InputRT, ref Output);
            }
        }
    }

    public void init()
    {
        kernelFFTHorizontal = CS.FindKernel("FFTHorizontal");
        kernelFFTVertical = CS.FindKernel("FFTVertical");
        kernelFFTDepth = CS.FindKernel("FFTDepth");

        kernelFFTHorizontal_IEND = CS.FindKernel("FFTHorizontal_IEND");
        kernelFFTVertical_IEND = CS.FindKernel("FFTVertical_IEND");
        kernelFFTDepth_IEND = CS.FindKernel("FFTDepth_IEND");

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
        kernelFFTShift = CS.FindKernel("FFTShift");

        kernelBlitSliceOf3DTexture = CS.FindKernel("BlitSliceOf3DTexture");
        kernelBlitSliceOf3DTextureFloat4 = CS.FindKernel("BlitSliceOf3DTextureFloat4");
        kernelBlitSliceOf3DTextureFloat1 = CS.FindKernel("BlitSliceOf3DTextureFloat1");
        kernelIFFTConj = CS.FindKernel("IFFTConj");
        kernelBlitIOTexFloat4 = CS.FindKernel("BlitIOTexFloat4");
    }

    void ComputeFFT(int kernel, ref RenderTexture input)
    {
        CS.SetTexture(kernel, "InputRT", input);
        CS.SetTexture(kernel, "OutputRT", OutputRT);

        CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
        RenderTexture rt = input;
        input = OutputRT;
        OutputRT = rt;
    }

    void ComputeFFT(int kernel, ref RenderTexture input, ref RenderTexture output)
    {
        CS.SetTexture(kernel, "InputRT", input);
        CS.SetTexture(kernel, "OutputRT", output);

        CS.Dispatch(kernel, N[0] / 8, N[1] / 8, N[2] / 8);
        RenderTexture rt = input;
        input = output;
        output = rt;
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
        var res = new float[t.width * t.height * t.volumeDepth*4];

        RenderTexture tmp = CreateRenderTexture(t.width, t.height, RenderTextureFormat.ARGBFloat);
        int kernel;
        string input;
        switch(t.format)
        {
            case RenderTextureFormat.ARGBFloat:
                kernel = kernelBlitSliceOf3DTextureFloat4;
                input = "InputFloat4RT";
                break;
            case RenderTextureFormat.RFloat:
                kernel = kernelBlitSliceOf3DTextureFloat1;
                input = "InputFloat1RT";
                break;
            default:
                kernel = kernelBlitSliceOf3DTexture;
                input = "InputRT";
                break;
        }

        CS.SetTexture(kernel, input, t);
        CS.SetTexture(kernel, "Sliced", tmp);
        
        for(int i = 0; i < t.volumeDepth; ++i)
        {
            CS.SetInt("layer", i);
            CS.Dispatch(kernel, t.width / 8, t.height / 8, 1);
            var k = ReadRenderTextureRaw(tmp, TextureFormat.RGBAFloat);
            // k.CopyTo(res, t.width * t.width * i);
            System.Array.Copy(k, 0, res, t.width * t.height * i * 4, k.Length);
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

    public struct Float4Array
    {
        public float[,,] f1;
        public float[,,] f2;
        public float[,,] f3;
        public float[,,] f4;
    }

    public struct Float4Array1D
    {
        public float[] f1;
        public float[] f2;
        public float[] f3;
        public float[] f4;
    }

    [System.Serializable]
    public struct Float4Type1D
    {
        public Float4Array1D data;
    }

    [System.Serializable]
    public struct Float4Type
    {
        public Float4Array data;
    }

    [System.Serializable]
    struct InputType
    {
        public ComplexArray data;
    }

    public void ExportComplex3D(RenderTexture t, string filename)
    {
        ExportArray(t, TextureFormat.RGBAFloat, 2, filename);
    }


    void ExportColorAsFloat4(float[] colors, string filename, int width, int heigh, int depth)
    {
        var index = 0;

        Float4Type ipt;
        ipt.data.f1 = new float[depth, heigh, width];
        ipt.data.f2 = new float[depth, heigh, width];
        ipt.data.f3 = new float[depth, heigh, width];
        ipt.data.f4 = new float[depth, heigh, width];

        for (int k = 0; k < depth; ++k)
        {
            for (int i = 0; i < heigh; ++i)
            {
                for (int j = 0; j < width; ++j)
                {

                    ipt.data.f1[k, i, j] = colors[index];
                    ++index;
                    ipt.data.f2[k, i, j] = colors[index];
                    ++index;
                    ipt.data.f3[k, i, j] = colors[index];
                    ++index;
                    ipt.data.f4[k, i, j] = colors[index];
                    ++index;

                }

            }
        }

        var chh = Newtonsoft.Json.JsonConvert.SerializeObject(ipt);
        File.WriteAllText(filename, chh);
    }

    

    public void ExportFloat4_3D(RenderTexture t, string filename)
    {
        var width = t.width;
        var heigh = t.height;
        var depth = t.volumeDepth;

        var colors = ReadRenderTextureRaw(t, TextureFormat.RGBAFloat);


        ExportColorAsFloat4(colors, filename, width, heigh, depth);
    }

    public void ExportFloat1_3D(RenderTexture t, string filename)
    {
        var width = t.width;
        var heigh = t.height;
        var depth = t.volumeDepth;

        var colors = ReadRenderTextureRaw(t, TextureFormat.RGBAFloat);
        var index = 0;

        Float4Type ipt;
        ipt.data.f1 = new float[depth, heigh, width];
        ipt.data.f2 = new float[1, 1, 1];
        ipt.data.f3 = new float[1, 1, 1];
        ipt.data.f4 = new float[1, 1, 1];

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

                    ipt.data.f1[k, i, j] = colors[index];
                    ++index;
                    ++index;
                    ++index;
                    ++index;
                }

            }
        }

        var chh = Newtonsoft.Json.JsonConvert.SerializeObject(ipt);
        File.WriteAllText(filename, chh);
    }

    public void ExportArray(RenderTexture t, TextureFormat fmt, int channels, string filename)
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

    public void ExportFloat4(ComputeBuffer c, string filename)
    {
        float[] color = new float[c.count * 4];
        c.GetData(color);

        Float4Type1D ipt;
        ipt.data.f1 = new float[c.count];
        ipt.data.f2 = new float[c.count];
        ipt.data.f3 = new float[c.count];
        ipt.data.f4 = new float[c.count];

        int index = 0;
        for (int i = 0; i < c.count; ++i)
        {
            ipt.data.f1[i] = color[index];
            ++index;
            ipt.data.f2[i] = color[index];
            ++index;
            ipt.data.f3[i] = color[index];
            ++index;
            ipt.data.f4[i] = color[index];
            ++index;
        }

        var chh = Newtonsoft.Json.JsonConvert.SerializeObject(ipt);
        File.WriteAllText(filename, chh);
    }

    public ComputeBuffer LoadJson_Float4ComputeBuffer(string filename)
    {
        if(!File.Exists(filename))
        {
            Debug.LogError("File: " + filename + " not exists");
        }
        var txt = File.ReadAllText(filename);
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<Float4Type1D>(txt);
        var d1 = input.data.f1.Length;
        var d2 = input.data.f2.Length;
        var d3 = input.data.f3.Length;
        var d4 = input.data.f3.Length;

        var compute_buffer = new ComputeBuffer(d1, 4 * sizeof(float));

        float[] arr = new float[d1 * 4];

        int index = 0;
        for(int i = 0; i < d1; ++i)
        {
            arr[index] = input.data.f1[i];
            index++;

            arr[index] = input.data.f2[i];
            index++;

            arr[index] = input.data.f3[i];
            index++;

            arr[index] = input.data.f4[i];
            index++;
        }

        compute_buffer.SetData(arr);
        return compute_buffer;
    }

    public RenderTexture LoadJson3D(string filename, out Texture3D text)
    {
        var txt = File.ReadAllText(filename);
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<InputType>(txt);
        var depth = input.data.real.GetLength(0);
        var height = input.data.real.GetLength(1);
        var width = input.data.real.GetLength(2);

        var textureIn = CreateRenderTexture3D(width, height, depth, RenderTextureFormat.RGFloat);

        Texture3D texture = new Texture3D(width, height, depth, TextureFormat.RGFloat, false);

        RenderTexture.active = textureIn;
        var pxdata = texture.GetPixelData<float>(0);
        int index = 0;
        for (int k = 0; k < depth; ++k)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {

                    pxdata[index] = input.data.real[k, i, j];
                    ++index;
                    pxdata[index] = input.data.imag[k, i, j];
                    ++index;
                }
            };
        }
        texture.Apply(updateMipmaps: false);
        RenderTexture.active = null;
        SetN(new Vector3Int(textureIn.width, textureIn.height, textureIn.volumeDepth));
        Blit3D(texture, textureIn);
        text = texture;
        return textureIn;
    }

    public RenderTexture LoadJson3D_Float4(string filename, out Texture3D text)
    {
        var txt = File.ReadAllText(filename);
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<Float4Type>(txt);
        var depth = input.data.f1.GetLength(0);
        var height = input.data.f1.GetLength(1);
        var width = input.data.f1.GetLength(2);

        var textureIn = CreateRenderTexture3D(width, height, depth, RenderTextureFormat.ARGBFloat);

        Texture3D texture = new Texture3D(width, height, depth, TextureFormat.RGBAFloat, false);

        RenderTexture.active = textureIn;
        var pxdata = texture.GetPixelData<float>(0);
        int index = 0;
        for (int k = 0; k < depth; ++k)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {

                    pxdata[index] = input.data.f1[k, i, j];
                    ++index;
                    pxdata[index] = input.data.f2[k, i, j];
                    ++index;
                    pxdata[index] = input.data.f3[k, i, j];
                    ++index;
                    pxdata[index] = input.data.f4[k, i, j];
                    ++index;

                }
            };
        }
        texture.Apply(updateMipmaps: false);
        RenderTexture.active = null;
        //SetN(new Vector3Int(textureIn.width, textureIn.height, textureIn.volumeDepth));
        Blit3D(texture, textureIn);
        text = texture;
        return textureIn;
    }

    public void myRunTest()
    {
        Texture3D texture;
        var textureIn = LoadJson3D("test/input.json", out texture);
        var textureOut = CreateRenderTexture3D(N[0], N[1], N[2], RenderTextureFormat.RGFloat);
        OutputRT = textureOut;
        /*
        var textureIn = CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
        var txt = File.ReadAllText("test/input.json");
        var input = Newtonsoft.Json.JsonConvert.DeserializeObject<InputType>(txt);
        //N = Mathf.FloorToInt(Mathf.Pow(input.data.real.Length, ));
        N = 16;
        FFTPow = Mathf.FloorToInt(Mathf.Log(N, 2));
        CS.SetInt("N", N);
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
        */

        // Graphics.Blit(texture, textureIn);
        InputRTMat.SetTexture("_MainTex", textureIn);
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/last_input.json");
        

        fft(ref textureIn, ref textureOut);
        
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result.json");

        
        Blit3D(texture, textureIn);
        //Graphics.Blit(texture, textureIn);
        ifft(ref textureIn, ref textureOut);
        
        //Blit2DToDebug(textureIn, DebugOutput);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result_ifft.json");

        fftshift(ref textureIn, ref textureOut);
        ExportArray(textureIn, TextureFormat.RGBAFloat, 2, "test/result_ifft_shift.json");

        textureIn.Release();
        textureIn = LoadJson3D("test/input0.json", out texture);
        ExportComplex3D(textureIn, "test/input0_a.json");

        fft(ref textureIn, ref textureOut);
        ExportComplex3D(textureIn, "test/result0.json");
        // Debug.Log(input);
        textureIn.Release();
        textureIn = LoadJson3D("test/isf.div.json", out texture);
        fft(ref textureIn, ref textureOut);
        ExportComplex3D(textureIn, "test/result.fftdiv.json");

        textureOut.Release();
        OutputRT = null;
    }
    /*
    void Start()
    {
        init();
        myRunTest();

    }*/
}

