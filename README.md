# WindowsML-CoreML-MobileNet-demo
Sample code of converting CoreML MobileNet into Windows Machine Learning.  
We prepare CoreML MobileNet for image classification from [here](https://coreml.store/mobilenet).  

![mobilenet_demo](mobilenet_demo.gif)

## Prepare model
Download MobileNet model from [Core ML Models](https://developer.apple.com/machine-learning/models/).  
Then you see [`MobileNetV2.mlmodel`](https://ml-assets.apple.com/coreml/models/Image/ImageClassification/MobileNetV2/MobileNetV2.mlmodel), move model file to `./convert_model` directory.  

We use `coremltools` for loading CoreML model and `winmltools` for converting CoreML model into ONNX format.  
First, install `coremltools` and `winmltools` by pip.  

```shell
# the latest version @ 2020/06/27 is winmltools==1.5.2, but it couses import error at onnxmltools
pip install coremltools winmltools==1.4.2
```

Second, execute python file for converting CoreML model.  

```shell
python coreml2onnx.py
```

After execution, you will get two files `mobilenet.onnx` and `mobilenet.txt`.  
Third, create C# code describing ONNX model.  

```shell
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.17125.0\x64\mlgen.exe" -i mobilenet.onnx -l CS -n mobilenet -o mobilenet.cs
```

`mobilenet.cs` describes three classes `MobilenetModelInput` and `MobilenetModelOutput`and `MobilenetModel`.  
Each class correspond `Network input(image)`, `Network output(class label and probability)` and `MobileNet Network and inference method`.  

Finally, move each files.  
`mobilenet.cs -> {solution dir}`  
`mobilenet.onnx -> {solution dir}/Assets`  

## Run demo
Start solution application.  

### Deploying the sample
- Select Build > Deploy Solution.

### Cite
- [Tutorial: Create a Windows Machine Learning UWP application (C#)](https://docs.microsoft.com/windows/ai/windows-ml/get-started-uwp)  
- [Github: MNIST ONNX UWP](https://github.com/Microsoft/Windows-Machine-Learning/tree/master/Samples/MNIST/UWP/cs)  
- [Qiita: 3分で分かる！ONNXフォーマットとWindows Machine Learning！](https://qiita.com/ymym3412/items/05a7cecf81309a3f131e)  