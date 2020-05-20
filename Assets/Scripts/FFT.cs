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
    

    int kernelBlitSliceOf3DTexture;
    int kernelBlitSliceOf3DTextureFloat4;
    int kernelBlitSliceOf3DTextureFloat1;

    int N;
    int FFTPow;

    public void SetN(int n)
    {
        N = n;
        FFTPow = Mathf.FloorToInt(Mathf.Log(N, 2));
        CS.SetInt("N", N);
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
        CS.SetTexture(kernelBlitIOTex, "InputTex", texture);
        CS.SetTexture(kernelBlitIOTex, "OutputRT", rt);
        CS.Dispatch(kernelBlitIOTex, N / 8, N / 8, N / 8);
    }

    static public RenderTexture CreateRenderTexture(int size, RenderTextureFormat type = RenderTextureFormat.ARGBFloat)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, type);
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
        CS.SetInt("N", InputRT.width);
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTHorizontal, ref InputRT, ref Output);
        }

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTVertical, ref InputRT, ref Output);
        }

        //进行深度FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTDepth, ref InputRT, ref Output);
        }
    }

    public void fftshift(ref RenderTexture input, ref RenderTexture Output)
    {
        CS.SetInt("N", input.width);
        ComputeFFT(kernelFFTShift, ref input, ref Output);
    }



    [System.Obsolete("This implemenation is possibly buggy")]
    public void ifft_c(ref RenderTexture InputRT)
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
    [System.Obsolete("This implemenation is possibly buggy")]
    public void ifft_a(ref RenderTexture InputRT)
    {
        CS.SetInt("N", InputRT.width);
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTHorizontal, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelIFFTVertical, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
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
        CS.SetInt("N", InputRT.width);

        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTHorizontal, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            ComputeFFT(kernelFFTVertical, ref InputRT);
        }
        ComputeFFT(kernelIFFTFlip, ref InputRT);

        ComputeFFT(kernelIFFTConj, ref InputRT);
        //进行纵向FFT
        for (int m = 1; m <= FFTPow; m++)
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
        CS.SetInt("N", InputRT.width);

        ComputeFFT(kernelIFFTConj, ref InputRT, ref Output);
        //进行横向FFT
        for (int m = 1; m <= FFTPow; m++)
        {
            CS.SetInt("FFTPass", m);
            int ns = 1 << (m - 1);
            CS.SetInt("Ns", ns);
            //最后一次进行特殊处理
            if (m != FFTPow)
            {
                ComputeFFT(kernelFFTHorizontal, ref InputRT, ref Output);
            }
            else
            {
                ComputeFFT(kernelFFTHorizontal_IEND, ref InputRT, ref Output);
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
                ComputeFFT(kernelFFTVertical, ref InputRT, ref Output);
            }
            else
            {
                ComputeFFT(kernelFFTVertical_IEND, ref InputRT, ref Output);
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

    void ComputeFFT(int kernel, ref RenderTexture input, ref RenderTexture output)
    {
        CS.SetTexture(kernel, "InputRT", input);
        CS.SetTexture(kernel, "OutputRT", output);

        CS.Dispatch(kernel, N / 8, N / 8, N / 8);
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
        var res = new float[t.width * t.width * t.volumeDepth*4];

        RenderTexture tmp = CreateRenderTexture(t.width, RenderTextureFormat.ARGBFloat);
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
            CS.Dispatch(kernel, t.width / 8, t.width / 8, 1);
            var k = ReadRenderTextureRaw(tmp, TextureFormat.RGBAFloat);
            // k.CopyTo(res, t.width * t.width * i);
            System.Array.Copy(k, 0, res, t.width * t.width * i * 4, k.Length);
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

    struct Float4Array
    {
        public float[,,] f1;
        public float[,,] f2;
        public float[,,] f3;
        public float[,,] f4;
    }

    [System.Serializable]
    struct Float4Type
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

    public void ExportFloat4_3D(ComputeBuffer c, string filename)
    {
        float[] color = new float[c.count * 4];
        c.GetData(color);

        ExportColorAsFloat4(color, filename, c.count, 1, 1);
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
        SetN(textureIn.width);
        Blit3D(texture, textureIn);
        text = texture;
        return textureIn;
    }
    
    public void myRunTest()
    {
        Texture3D texture;
        var textureIn = LoadJson3D("test/input.json", out texture);
        var textureOut = CreateRenderTexture3D(N, N, N, RenderTextureFormat.RGFloat);
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

