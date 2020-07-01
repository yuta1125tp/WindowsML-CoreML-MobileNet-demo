from coremltools.models.utils import load_spec
from winmltools import convert_coreml
from winmltools.utils import save_model#, save_text

from pathlib import Path

mlmodel_list = []
for mlmodel in Path("./").glob("*.mlmodel"):
    # ml model load
    model_coreml = load_spec(mlmodel)
    # convert coreml models to onnx model
    tgt_opset = 8
    model_onnx = convert_coreml(model_coreml, tgt_opset, name=mlmodel.stem.lower())

    # save onnx format
    save_model(model_onnx, f"{mlmodel.stem.lower()}.onnx")
    # save_text(model_onnx, f"{mlmodel.stem.lower()}.txt")
