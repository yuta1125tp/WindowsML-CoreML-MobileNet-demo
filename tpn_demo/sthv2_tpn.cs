// This file was automatically generated by VS extension Windows Machine Learning Code Generator v3
// from model file sthv2_tpn.onnx
// Warning: This file may get overwritten if you add add an onnx file with the same name
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.AI.MachineLearning;
namespace tpn_demo
{
    
    public sealed class sthv2_tpnInput
    {
        public TensorFloat input; // shape(1,8,3,256,256)
    }
    
    public sealed class sthv2_tpnOutput
    {
        public TensorFloat output; // shape(1,174)
    }
    
    public sealed class sthv2_tpnModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<sthv2_tpnModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            sthv2_tpnModel learningModel = new sthv2_tpnModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }
        public async Task<sthv2_tpnOutput> EvaluateAsync(sthv2_tpnInput input)
        {
            binding.Bind("input", input.input);
            var result = await session.EvaluateAsync(binding, "0");
            var output = new sthv2_tpnOutput();
            output.output = result.Outputs["output"] as TensorFloat;
            return output;
        }
    }
}

