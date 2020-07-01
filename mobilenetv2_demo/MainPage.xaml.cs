using mobilenetv2_demo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace mobilenetv2_demo
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture mediaCapture;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);  //複数のスレッドで検出しないようにするためのsemaphore
        private ThreadPoolTimer timer;

        private mobilenetv2Model ModelGen;
        private mobilenetv2Input ModelInput = new mobilenetv2Input();
        private mobilenetv2Output ModelOutput;

        public MainPage()
        {
            this.InitializeComponent();
            _ = LoadModelAsync();
        }

        private async Task LoadModelAsync()
        {
            // Load a machine learning model
            StorageFile modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/mobilenetv2.onnx"));
            ModelGen = await mobilenetv2Model.CreateFromStreamAsync(modelFile as IRandomAccessStreamReference);
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
        private async void CurrentVideoFrame(ThreadPoolTimer timer)
        {
            //複数スレッドでの同時実行を抑制
            if (!semaphore.Wait(0))
            {
                return;
            }

            try
            {
                //AIモデルのインプットデータは解像度224x224,BGRA8にする必要がある。
                using (VideoFrame previewFrame = new VideoFrame(BitmapPixelFormat.Bgra8, 224, 224))
                {
                    await this.mediaCapture.GetPreviewFrameAsync(previewFrame);

                    if (ModelGen != null && previewFrame !=null)
                    {
                        ModelInput.image = Windows.AI.MachineLearning.ImageFeatureValue.CreateFromVideoFrame(previewFrame);

                        // Evaluate the model
                        ModelOutput = await ModelGen.EvaluateAsync(ModelInput);

                        // Convert output to datatype
                        var batchIdx = 0;
                        IDictionary<string, float> vectorImage = ModelOutput.classLabelProbs[batchIdx];
                        var scoreList = vectorImage.Values.ToList();
                        var labelList = vectorImage.Keys.ToList();

                        //IReadOnlyList<float> vectorImage = ModelOutput.Plus214_Output_0.GetAsVectorView();
                        //IList<float> imageList = vectorImage.ToList();

                        // Query to check for highest probability digit
                        var maxValue = scoreList.Max();
                        var maxIndex = scoreList.IndexOf(maxValue);

                        var label = labelList[maxIndex];

                        // Display the results on UI Thread.
                        var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            string result = "";
                            //予測結果を表示
                            result = result + "Class: " + label + ", Prob: " + maxValue;
                            this.msgTbk.Text = result;
                        });

                    }
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
