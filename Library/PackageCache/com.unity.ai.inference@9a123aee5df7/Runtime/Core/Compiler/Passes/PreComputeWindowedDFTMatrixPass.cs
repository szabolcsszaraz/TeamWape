using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Helper to generate and connect a constant WindowedDFT tensor to DFT and STFT.
    /// </summary>
    class PreComputeWindowedDFTMatrixPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            //D.LogWarning("Running PreComputeWindowedDFTMatrixPass");
            using var ops = new CPUOps();

            var stftNodes = gm.graph.FindNodes(Node.kOpCallFunction, "STFT");
            foreach (var stftNode in stftNodes)
            {
                // stftNode.args[] : (signal, frameStep, window, frameLength, windowedDFTMatrix, onesided);
                var windowedDFTMatrixNode = (Node) stftNode.args[4];
                var windowNode = (Node)stftNode.args[2];
                var frameLengthNode = (Node)stftNode.args[3];
                if (windowedDFTMatrixNode != null || (windowNode != null && windowNode.op != Node.kOpGetAttr) || frameLengthNode.op != Node.kOpGetAttr)
                    continue;

                // Note/TODO: could be more resilient, by having multiple formats generated, saved and and given to STFT
                // signal must have some known shape, at least known to be real or complex:
                var signalShape = ((Node)stftNode.args[0]).partialTensor.shape;
                if (!(signalShape.hasRank && signalShape[-1].isValue))
                {
                    D.Log("PreComputeWindowedDFTMatrixPass optimization: can't figure out statically whether the signal will be real or complex, skipping optimization of STFT node");
                    continue;
                }

                var frameLength = (gm.attributes[frameLengthNode.target].ToTensor() as Tensor<int>)[0];
                bool alternateRealImaOnRows = signalShape[-1].value == 1;
                bool onesided = ((bool)stftNode.args[5]);
                Tensor<float> window = windowNode != null ? gm.attributes[windowNode.target].ToTensor() as Tensor<float> : null;

                using Tensor windowedDFTMatrix = ops.WindowedDFTMatrix(window, dftLength: frameLength, inputFrameLength: frameLength, inverse: false, onesided, alternateRealImaOnRows);
                windowedDFTMatrixNode = GraphPassUtil.AddConstant(gm, stftNode, windowedDFTMatrix.ReadbackAndClone());

                stftNode.args[4] = windowedDFTMatrixNode;
                stftNode.UpdateArgs(stftNode.args);
            }

            var dftNodes = gm.graph.FindNodes(Node.kOpCallFunction, "DFT");
            foreach (var dftNode in dftNodes)
            {
                // dftNode.args[] : (input, dftLength, axis, dftMatrix, inverse, onesided);
                var inputNode = (Node)dftNode.args[0];
                var dftLengthNode = (Node)dftNode.args[1];
                var axisNode = (Node)dftNode.args[2];
                var dftMatrixNode = (Node)dftNode.args[3];

                if (dftMatrixNode != null)
                    continue;

                int axisValue = -2;
                if (axisNode != null)
                {
                    if (axisNode.op != Node.kOpGetAttr)
                        continue;
                    else
                        axisValue = (gm.attributes[axisNode.target].ToTensor() as Tensor<int>)[0];
                }

                var inputShape = inputNode.partialTensor.shape;
                if (!(inputShape.hasRank && inputShape[-1].isValue))
                {
                    D.Log("PreComputeWindowedDFTMatrixPass optimization: can't figure out statically whether the signal will be real or complex, skipping optimization of DFT node");
                    continue;
                }
                if (!(inputShape.hasRank && inputShape[axisValue].isValue))
                {
                    D.Log("PreComputeWindowedDFTMatrixPass optimization: can't figure out statically whether the input signal on axis will be real or complex, skipping optimization of DFT node");
                    continue;
                }

                int origSignalLength = inputShape[axisValue].value;

                if (dftLengthNode != null && dftLengthNode.op != Node.kOpGetAttr)
                {
                    D.Log("PreComputeWindowedDFTMatrixPass optimization: can't figure out statically dftLength parameter, skipping optimization of DFT node");
                    continue;
                }

                int dftLengthValue = dftLengthNode == null ? origSignalLength : (gm.attributes[dftLengthNode.target].ToTensor() as Tensor<int>)[0];

                // Note/TODO: could be more resilient, by having multiple formats generated, saved and and given to DFT
                // signal must have some known shape, at least known to be real or complex:
                bool alternateRealImaOnRows = inputShape[-1].value == 1;
                bool inverse = ((bool)dftNode.args[4]);
                // IMPORTANT: still even if for inverse, we don't bake the normalizer in the matrix as some backends do it as a scaling after the application of dftMatrix for better precision
                bool onesided = ((bool)dftNode.args[5]);

                int signalFrameLengthToUse = Math.Min(dftLengthValue, origSignalLength);
                // int outputXformSignalLength = onesided ? dftLengthValue / 2 + 1 : dftLengthValue;
                // ...this will be calculated as such from dftLength and onesided in the call below:
                using Tensor windowedDFTMatrix = ops.WindowedDFTMatrix(window: null, dftLength: dftLengthValue, inputFrameLength: signalFrameLengthToUse, inverse: inverse, onesided, alternateRealImaOnRows);
                dftMatrixNode = GraphPassUtil.AddConstant(gm, dftNode, windowedDFTMatrix.ReadbackAndClone());

                dftNode.args[3] = dftMatrixNode;
                dftNode.UpdateArgs(dftNode.args);
            }

        }
    }
}
