using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Media.Imaging;
//using System.Windows.Media.Imaging;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace tpn_demo
{
    /// <summary>
    /// unsafeを付ける必要があるかも
    /// https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/imaging#create-or-edit-a-softwarebitmap-programmatically
    /// </summary>
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }


    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        //[DllImport("CustomTensorization.dll")]
        //static extern void Test();

        private MediaCapture mediaCapture;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);  //複数のスレッドで検出しないようにするためのsemaphore
        private ThreadPoolTimer timer;

        private sthv2_tpnModel ModelGen;
        private sthv2_tpnInput ModelInput = new sthv2_tpnInput();
        private sthv2_tpnOutput ModelOutput;

        private List<string> labelList = new List<string>();
        private List<float> mean = new List<float>() { 123.675f, 116.28f, 103.53f };
        private List<float> std = new List<float>() { 58.395f, 57.12f, 57.375f };

        private List<VideoFrame> frameBuffer = new List<VideoFrame>();
        private int target_length = 8;
        private int frame_refresh_count = 0;

        public MainPage()
        {
            this.InitializeComponent();
            Debug.WriteLine($"OS : {(Environment.Is64BitOperatingSystem ? "64bit" : "32bit")}");
            Debug.WriteLine($"Environment.CurrentDirectory : {Environment.CurrentDirectory}"); 
            //Test();
            _ = LoadLabelList();
            _ = LoadModelAsync();
        }

        private async Task LoadLabelList()
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/category.txt"));
            labelList.Clear();
            using (var inputStream = await file.OpenReadAsync())
            using (var classicStream = inputStream.AsStreamForRead())
            using (var streamReader = new StreamReader(classicStream))
            {
                while (streamReader.Peek() >= 0)
                {
                    labelList.Add(String.Format("{0}", streamReader.ReadLine()));
                }
            }
        }

        private async Task LoadModelAsync()
        {
            // Load a machine learning model
            StorageFile modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/sthv2_tpn.onnx"));
            ModelGen = await sthv2_tpnModel.CreateFromStreamAsync(modelFile as IRandomAccessStreamReference);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Hello");
            await InitCameraAsync();
        }
        private async Task InitCameraAsync()
        {
            try
            {
                //mediaCaptureオブジェクトが有効な時は一度Disposeする
                if (mediaCapture != null)
                {
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                //キャプチャーの設定
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.VideoDeviceId = "";
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Video;

                //カメラデバイスの取得
                var cameraDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                if (cameraDevices.Count() == 0)
                {
                    Debug.WriteLine("No Camera");
                    return;
                }
                else if (cameraDevices.Count() == 1)
                {
                    Debug.WriteLine("count1\n");
                    captureInitSettings.VideoDeviceId = cameraDevices[0].Id;
                }
                else
                {
                    Debug.WriteLine("countelse\n");
                    captureInitSettings.VideoDeviceId = cameraDevices[1].Id;
                }

                //キャプチャーの準備
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync(captureInitSettings);

                VideoEncodingProperties vp = new VideoEncodingProperties();

                Debug.WriteLine("before camera size\n");
                //RasperryPiでは解像度が高いと映像が乱れるので小さい解像度にしている
                //ラズパイじゃなければ必要ないかも？
                vp.Height = 720;
                vp.Width = 1280;
                vp.Subtype = "RGB24";

                await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, vp);

                capture.Source = mediaCapture;

                //キャプチャーの開始
                await mediaCapture.StartPreviewAsync();

                Debug.WriteLine("Camera Initialized");

                //指定したFPS毎にタイマーを起動する。
                TimeSpan timerInterval = TimeSpan.FromMilliseconds(1);
                timer = ThreadPoolTimer.CreatePeriodicTimer(new TimerElapsedHandler(CurrentVideoFrame), timerInterval);

            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
        }

        unsafe private TensorFloat CustomTensorize(List<VideoFrame> frameList, List<float> mean, List<float> std, bool toRGB=false)
        {
            int temp_len = frameList.Count();
            SoftwareBitmap softwareBitmap = frameList[0].SoftwareBitmap;
            Int32 height = softwareBitmap.PixelHeight;
            Int32 width = softwareBitmap.PixelWidth;
            BitmapPixelFormat pixelFormat = softwareBitmap.BitmapPixelFormat;

            Int32 channels = BitmapPixelFormat.Gray8 == pixelFormat ? 1 : 3;

            List<Int64> shape = new List<Int64>() { 1, temp_len, channels, height, width }; // B,T,C,H,W

            // The channels of image stored in buffer is in order of BGRA-BGRA-BGRA-BGRA. 
            // Then we transform it to the order of BBBBB....GGGGG....RRRR....AAAA(dropped) 
            TensorFloat tf = TensorFloat.Create(shape);
            byte* pCPUTensorbyte;
            float* pCPUTensor;
            uint uCapacity;

            // The channels of image stored in buffer is in order of BGRA-BGRA-BGRA-BGRA. 
            // Then we transform it to the order of BBBBB....GGGGG....RRRR....AAAA(dropped) 
            var tfr = tf.CreateReference();
            var tfr2 = (IMemoryBufferByteAccess)tfr;
            tfr2.GetBuffer(out pCPUTensorbyte, out uCapacity);
            pCPUTensor = (float*)pCPUTensorbyte;

            for (Int32 t = 0; t < temp_len; t += 1)
            {
                VideoFrame frame = frameList[t];
                SoftwareBitmap softwareBitmap2 = frame.SoftwareBitmap;
                // 1. Get the access to buffer of softwarebitmap
                BitmapBuffer spBitmapBuffer = softwareBitmap2.LockBuffer(BitmapBufferAccessMode.Read);
                IMemoryBufferReference reference = spBitmapBuffer.CreateReference();

                byte* pData;
                uint size;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out pData, out size);

                // 2. Transform the data in buffer to a vector of float
                var offset = (height * width * channels) * t;
                if (BitmapPixelFormat.Bgra8 == pixelFormat)
                {
                    for (UInt32 i = 0; i < size; i += 4)
                    {
                        if (toRGB)
                        {
                            // suppose the model expects BGR image.
                            // index 0 is B, 1 is G, 2 is R, 3 is alpha(dropped).
                            UInt32 pixelInd = i / 4;
                            pCPUTensor[offset + (height * width * 0) + pixelInd] = (((float)pData[i + 2]) - mean[0]) / std[0];
                            pCPUTensor[offset + (height * width * 1) + pixelInd] = (((float)pData[i + 1]) - mean[1]) / std[1];
                            pCPUTensor[offset + (height * width * 2) + pixelInd] = (((float)pData[i + 0]) - mean[2]) / std[2];
                        }
                        else
                        {
                            // suppose the model expects BGR image.
                            // index 0 is B, 1 is G, 2 is R, 3 is alpha(dropped).
                            UInt32 pixelInd = i / 4;
                            pCPUTensor[offset + (height * width * 0) + pixelInd] = (((float)pData[i + 0]) - mean[0]) / std[0];
                            pCPUTensor[offset + (height * width * 1) + pixelInd] = (((float)pData[i + 1]) - mean[1]) / std[1];
                            pCPUTensor[offset + (height * width * 2) + pixelInd] = (((float)pData[i + 2]) - mean[2]) / std[2];
                        }
                    }
                }
                else if (BitmapPixelFormat.Rgba8 == pixelFormat)
                {
                    for (UInt32 i = 0; i < size; i += 4)
                    {
                        // suppose the model expects BGR image.
                        // index 0 is B, 1 is G, 2 is R, 3 is alpha(dropped).
                        if (toRGB)
                        {
                            // suppose the model expects BGR image.
                            // index 0 is B, 1 is G, 2 is R, 3 is alpha(dropped).
                            UInt32 pixelInd = i / 4;
                            pCPUTensor[offset + (height * width * 0) + pixelInd] = (((float)pData[i + 0]) - mean[0]) / std[0];
                            pCPUTensor[offset + (height * width * 1) + pixelInd] = (((float)pData[i + 1]) - mean[1]) / std[1];
                            pCPUTensor[offset + (height * width * 2) + pixelInd] = (((float)pData[i + 2]) - mean[2]) / std[2];
                        }
                        else
                        {
                            UInt32 pixelInd = i / 4;
                            pCPUTensor[offset + (height * width * 0) + pixelInd] = (((float)pData[i + 2]) - mean[0]) / std[0];
                            pCPUTensor[offset + (height * width * 1) + pixelInd] = (((float)pData[i + 1]) - mean[1]) / std[1];
                            pCPUTensor[offset + (height * width * 2) + pixelInd] = (((float)pData[i + 0]) - mean[2]) / std[2];
                        }
                    }
                }
                else if (BitmapPixelFormat.Gray8 == pixelFormat)
                {
                    for (UInt32 i = 0; i < size; i += 4)
                    {
                        // suppose the model expects BGR image.
                        // index 0 is B, 1 is G, 2 is R, 3 is alpha(dropped).
                        UInt32 pixelInd = i / 4;
                        float red = (float)pData[i + 2];
                        float green = (float)pData[i + 1];
                        float blue = (float)pData[i];
                        float gray = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
                        pCPUTensor[offset + pixelInd] = gray;
                    }
                }
            }

            // to prepend following error, copy to another instance and use it as model input.
            // The tensor has outstanding memory buffer references that must be closed prior to evaluation!
            TensorFloat ret = TensorFloat.CreateFromIterable(
                tf.Shape,
                tf.GetAsVectorView());

            return ret;
        }

        private async void CurrentVideoFrame(ThreadPoolTimer timer)
        {
            //複数スレッドでの同時実行を抑制
            if (!semaphore.Wait(0))
            {
                return;
            }

            try
            {
                // カメラから256x256の画像を取得
                VideoFrame previewFrame = new VideoFrame(BitmapPixelFormat.Bgra8, 256, 256);
                await this.mediaCapture.GetPreviewFrameAsync(previewFrame);

                if (frameBuffer.Count >= target_length)
                {
                    frameBuffer.RemoveAt(0); // 先頭をpop
                }
                frameBuffer.Add(previewFrame); // 末尾に追加
                frame_refresh_count += 1;

                if (ModelGen != null && previewFrame != null && frame_refresh_count == target_length)
                {
                    frame_refresh_count = 0; 
                    
                    ModelInput.input = CustomTensorize(frameBuffer, mean, std, true);

                    // Evaluate the model
                    ModelOutput = await ModelGen.EvaluateAsync(ModelInput);

                    // Convert output to datatype
                    var outtensor = ModelOutput.output;
                    IReadOnlyList<float> logits = outtensor.GetAsVectorView();
                    var exp_logits = logits.Select(l => Math.Exp(l));
                    var exp_logits_sum = exp_logits.Sum();
                    var probs = exp_logits.Select(el=>el / exp_logits_sum);

                    ////IReadOnlyList<float> vectorImage = ModelOutput.Plus214_Output_0.GetAsVectorView();
                    var prob_list = probs.ToList();

                    // Query to check for highest probability digit
                    var sorted_logt = prob_list.OrderBy(elm => -1*elm).ToList();

                    // Display the results on UI Thread.
                    var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        int topk = 5;
                        string result = "";
                        for (int k = 0; k < topk; k++)
                        {
                            var prob = sorted_logt[k];
                            string label = labelList[prob_list.IndexOf(prob)];
                            result = result + "\n"+"Prob: " + prob.ToString("0.00") + ", Label: " + label;
                        }
                        //予測結果を表示
                        this.msgTbk.Text = result;
                    });

                }
            }
        
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            finally
            {
                semaphore.Release();
            }
        }

    }
}