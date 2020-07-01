import onnxruntime
import argparse
from pathlib import Path
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

parser = argparse.ArgumentParser()
parser.add_argument('onnxfile', type=Path, help='onnxfile to show info')
args = parser.parse_args()

if not args.onnxfile.exists():
    logger.info(f'onnxfile does not exist: {args.onnxfile}')
    exit(1)
if not args.onnxfile.suffix!='onnx':
    logger.info(f'given file is not onnx file: {args.onnxfile}')
    exit(1)
session = onnxruntime.InferenceSession(str(args.onnxfile))

logger.info(session.get_inputs())
for inp in session.get_inputs():
    logger.info(inp.name)
    logger.info(inp.shape)

logger.info(session.get_outputs())

for out in session.get_outputs():
    logger.info(out.name)
    logger.info(out.shape)

