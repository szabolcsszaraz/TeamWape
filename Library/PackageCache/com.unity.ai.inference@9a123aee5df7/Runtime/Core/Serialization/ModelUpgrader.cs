using System;
using System.Collections.Generic;
using System.Linq;
using SentisFlatBuffer;
using Unity.InferenceEngine.Google.FlatBuffers;

namespace Unity.InferenceEngine
{
    static class ModelUpgrader
    {
        static T[] GetArray<T>(int length, Func<int, T> indexer)
        {
            var array = new T[length];
            for (var i = 0; i < length; i++)
                array[i] = indexer(i);
            return array;
        }

        static int AddTensorValue(FlatBufferBuilder builder, List<Offset<EValue>> valuesOffsets)
        {
            var index = valuesOffsets.Count;
            valuesOffsets.Add(EValue.CreateEValue(builder, KernelTypes.Tensor, SentisFlatBuffer.Tensor.CreateTensor(builder).Value));
            return index;
        }

        static int GetOperatorIndex(List<string> operators, string op)
        {
            var index = operators.IndexOf(op);
            if (index < 0)
            {
                index = operators.Count;
                operators.Add(op);
            }

            return index;
        }

        static int AddBoolValue(FlatBufferBuilder builder, List<Offset<EValue>> valuesOffsets, bool v)
        {
            var val = Bool.CreateBool(builder, v);
            valuesOffsets.Add(EValue.CreateEValue(builder, KernelTypes.Bool, val.Value));
            return valuesOffsets.Count - 1;
        }

        static int AddIntValue(FlatBufferBuilder builder, List<Offset<EValue>> valuesOffsets, int v)
        {
            var val = Int.CreateInt(builder, v);
            valuesOffsets.Add(EValue.CreateEValue(builder, KernelTypes.Int, val.Value));
            return valuesOffsets.Count - 1;
        }

        static int AddIntListValue(FlatBufferBuilder builder, List<Offset<EValue>> valuesOffsets, int[] intArray)
        {
            var val = IntList.CreateIntList(builder, IntList.CreateItemsVector(builder, intArray));
            valuesOffsets.Add(EValue.CreateEValue(builder, KernelTypes.IntList, val.Value));
            return valuesOffsets.Count - 1;
        }

        static int AddFloatValue(FlatBufferBuilder builder, List<Offset<EValue>> valuesOffsets, float v)
        {
            var val = Float.CreateFloat(builder, v);
            valuesOffsets.Add(EValue.CreateEValue(builder, KernelTypes.Float, val.Value));
            return valuesOffsets.Count - 1;
        }

        public static Program UpgradeFlatbuffer(Program program, uint toVersion, Func<ExecutionPlan, Chain, FlatBufferBuilder, List<string>, List<Offset<Chain>>, List<Offset<EValue>>, bool> OnChain = null, Func<int, EValue, FlatBufferBuilder, List<Offset<EValue>>, bool> OnValue = null)
        {
            var executionPlan = program.ExecutionPlan.Value;

            var builder = new FlatBufferBuilder(1);

            var inputs = executionPlan.GetInputsArray();
            var inputsName = GetArray(executionPlan.InputsNameLength, executionPlan.InputsName);
            var outputs = executionPlan.GetOutputsArray();
            var outputsName = GetArray(executionPlan.OutputsNameLength, executionPlan.OutputsName);
            var operators = GetArray(executionPlan.OperatorsLength, i => executionPlan.Operators(i).Value.Name).ToList();

            var valuesOffsets = new List<Offset<EValue>>();
            for (var i = 0; i < executionPlan.ValuesLength; i++)
            {
                var valOffset = 0;
                var value = executionPlan.Values(i).Value;

                // check if upgrade code handles case
                if (OnValue != null && OnValue(i, value, builder, valuesOffsets))
                    continue;

                switch (value.ValType)
                {
                    case KernelTypes.NONE:
                        valOffset = default;
                        break;
                    case KernelTypes.Null:
                        valOffset = default;
                        break;
                    case KernelTypes.Int:
                        valOffset = Int.CreateInt(builder, value.ValAsInt().IntVal).Value;
                        break;
                    case KernelTypes.Float:
                        valOffset = Float.CreateFloat(builder, value.ValAsFloat().FloatVal).Value;
                        break;
                    case KernelTypes.Bool:
                        valOffset = Bool.CreateBool(builder, value.ValAsBool().BoolVal).Value;
                        break;
                    case KernelTypes.Byte:
                        valOffset = SentisFlatBuffer.Byte.CreateByte(builder, value.ValAsByte().ByteVal).Value;
                        break;
                    case KernelTypes.Tensor:
                        var inputDesc = value.ValAsTensor();
                        var dynamicSizesOffset = default(VectorOffset);
                        var fixedSizesOffset = default(VectorOffset);
                        var sizes = inputDesc.GetFixedSizesArray();
                        if (sizes != null)
                            fixedSizesOffset = SentisFlatBuffer.Tensor.CreateFixedSizesVector(builder, inputDesc.GetFixedSizesArray());
                        if (inputDesc.ShapeDynamism == TensorShapeDynamism.DYNAMIC_UNBOUND)
                        {
                            var dimOffsets = new Offset<EDim>[inputDesc.DynamicSizesLength];
                            for (var j = 0; j < inputDesc.DynamicSizesLength; j++)
                            {
                                var dim = inputDesc.DynamicSizes(j).Value;
                                var dimValOffset = dim.ValType switch
                                {
                                    SymbolicDim.NONE => 0,
                                    SymbolicDim.Int => Int.CreateInt(builder, dim.ValAsInt().IntVal).Value,
                                    SymbolicDim.Byte => SentisFlatBuffer.Byte.CreateByte(builder, dim.ValAsByte().ByteVal).Value,
                                    _ => throw new ArgumentOutOfRangeException()
                                };
                                dimOffsets[j] = EDim.CreateEDim(builder, dim.ValType, dimValOffset);
                            }

                            dynamicSizesOffset = SentisFlatBuffer.Tensor.CreateDynamicSizesVector(builder, dimOffsets);
                        }

                        var val = SentisFlatBuffer.Tensor.CreateTensor(
                            builder,
                            scalar_type: inputDesc.ScalarType,
                            length_byte: inputDesc.LengthByte,
                            fixed_sizesOffset: fixedSizesOffset,
                            constant_buffer_idx: inputDesc.ConstantBufferIdx,
                            storage_offset: inputDesc.StorageOffset,
                            shape_dynamism: inputDesc.ShapeDynamism,
                            dynamic_sizesOffset: dynamicSizesOffset,
                            has_dynamic_rank: inputDesc.HasDynamicRank
                        );
                        valOffset = val.Value;
                        break;
                    case KernelTypes.String:
                        valOffset = SentisFlatBuffer.String.CreateString(builder, builder.CreateString(value.ValAsString().StringVal)).Value;
                        break;
                    case KernelTypes.IntList:
                        valOffset = IntList.CreateIntList(builder, IntList.CreateItemsVector(builder, value.ValAsIntList().GetItemsArray())).Value;
                        break;
                    case KernelTypes.FloatList:
                        valOffset = FloatList.CreateFloatList(builder, FloatList.CreateItemsVector(builder, value.ValAsFloatList().GetItemsArray())).Value;
                        break;
                    case KernelTypes.BoolList:
                        valOffset = BoolList.CreateBoolList(builder, BoolList.CreateItemsVector(builder, value.ValAsBoolList().GetItemsArray())).Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                valuesOffsets.Add(EValue.CreateEValue(builder, value.ValType, valOffset));
            }

            var chainsOffsets = new List<Offset<Chain>>();
            for (var i = 0; i < executionPlan.ChainsLength; i++)
            {
                var chain = executionPlan.Chains(i).Value;

                // check if upgrade code handles case
                if (OnChain != null && OnChain(executionPlan, chain, builder, operators, chainsOffsets, valuesOffsets))
                    continue;

                var chainInputs = chain.GetInputsArray();
                var chainOutputs = chain.GetOutputsArray();
                var instructionsOffsets = new List<Offset<Instruction>>();
                for (var j = 0; j < chain.InstructionsLength; j++)
                {
                    var instruction = chain.Instructions(j).Value;
                    var instrArgsOffset = 0;
                    switch (instruction.InstrArgsType)
                    {
                        case InstructionArguments.NONE:
                            break;
                        case InstructionArguments.KernelCall:
                            var kernelCall = instruction.InstrArgsAsKernelCall();
                            var args = kernelCall.GetArgsArray();
                            instrArgsOffset = KernelCall.CreateKernelCall(builder, kernelCall.OpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, instrArgsOffset);
                    instructionsOffsets.Add(instructionOffset);
                }

                var chainOffset = Chain.CreateChain(
                    builder,
                    chainInputs == null ? default : Chain.CreateInputsVector(builder, chainInputs),
                    chainOutputs == null ? default : Chain.CreateOutputsVector(builder, chainOutputs),
                    Chain.CreateInstructionsVector(builder, instructionsOffsets.ToArray()));
                chainsOffsets.Add(chainOffset);
            }

            var symbolicDimsName = GetArray(executionPlan.SymbolicDimNamesLength, executionPlan.SymbolicDimNames);

            var programExecutionPlan = ExecutionPlan.CreateExecutionPlan(builder,
                nameOffset: builder.CreateString(executionPlan.Name),
                valuesOffset: ExecutionPlan.CreateValuesVector(builder, valuesOffsets.ToArray()),
                inputsOffset: ExecutionPlan.CreateInputsVector(builder, inputs),
                inputs_nameOffset: ExecutionPlan.CreateInputsNameVector(builder, inputsName.Select(i => builder.CreateString(i)).ToArray()),
                outputsOffset: ExecutionPlan.CreateOutputsVector(builder, outputs),
                outputs_nameOffset: ExecutionPlan.CreateOutputsNameVector(builder, outputsName.Select(i => builder.CreateString(i)).ToArray()),
                chainsOffset: ExecutionPlan.CreateChainsVector(builder, chainsOffsets.ToArray()),
                operatorsOffset: ExecutionPlan.CreateOperatorsVector(builder, operators.Select(i => Operator.CreateOperator(builder, builder.CreateString(i))).ToArray()),
                backend_partitioningOffset: BackendPartitioning.CreateBackendPartitioning(builder, BackendPartitioning.CreateChainsVector(builder, Array.Empty<int>()), SentisFlatBuffer.BackendType.CPU),
                symbolic_dim_namesOffset: ExecutionPlan.CreateSymbolicDimNamesVector(builder, symbolicDimsName.Select(i => builder.CreateString(i)).ToArray())
            );

            var dataSegments = new Offset<DataSegment>[program.SegmentsLength];
            for (var i = 0; i < program.SegmentsLength; i++)
            {
                var segment = program.Segments(i).Value;
                dataSegments[i] = DataSegment.CreateDataSegment(builder, segment.Offset, segment.Size);
            }

            var programDataSegments = Program.CreateSegmentsVector(builder, dataSegments);

            var programOffset = Program.CreateProgram(builder,
                version: toVersion,
                execution_planOffset: programExecutionPlan,
                segmentsOffset: programDataSegments);
            builder.FinishSizePrefixed(programOffset.Value);

            var modelDescription = builder.DataBuffer.ToSizedArray();
            var bb = new ByteBuffer(modelDescription, sizeof(int));
            return Program.GetRootAsProgram(bb);
        }

        static bool UpgradeChainV1toV2(ExecutionPlan executionPlan, Chain chain, FlatBufferBuilder builder, List<string> operators, List<Offset<Chain>> chainsOffsets, List<Offset<EValue>> valuesOffsets)
        {
            var instruction = chain.Instructions(0).Value;
            if (instruction.InstrArgsType != InstructionArguments.KernelCall)
                return false;
            var kernelCall = instruction.InstrArgsAsKernelCall();
            var k = operators[kernelCall.OpIndex];
            switch (k)
            {
                case "Max":
                case "Min":
                {
                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, kernelCall.OpIndex, ExecutionPlan.CreateInputsVector(builder, Array.Empty<int>())).Value);

                    var lhs = chain.Inputs(0);
                    for (var j = 1; j < chain.InputsLength; j++)
                    {
                        var rhs = chain.Inputs(j);
                        var ret = j == chain.InputsLength - 1 ? chain.Outputs(0) : AddTensorValue(builder, valuesOffsets);
                        chainsOffsets.Add(Chain.CreateChain(
                            builder,
                            Chain.CreateInputsVector(builder, new[] { lhs, rhs }),
                            Chain.CreateOutputsVector(builder, new[] { ret }),
                            Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                        );
                        lhs = ret;
                    }
                    return true;
                }
                case "Sum":
                {
                    var opIndex = GetOperatorIndex(operators, "Add");
                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, opIndex, ExecutionPlan.CreateInputsVector(builder, Array.Empty<int>())).Value);

                    var lhs = chain.Inputs(0);
                    for (var j = 1; j < chain.InputsLength; j++)
                    {
                        var rhs = chain.Inputs(j);
                        var ret = j == chain.InputsLength - 1 ? chain.Outputs(0) : AddTensorValue(builder, valuesOffsets);
                        chainsOffsets.Add(Chain.CreateChain(
                            builder,
                            Chain.CreateInputsVector(builder, new[] { lhs, rhs }),
                            Chain.CreateOutputsVector(builder, new[] { ret }),
                            Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                        );
                        lhs = ret;
                    }
                    return true;
                }
                case "Mean":
                {
                    var opIndex = GetOperatorIndex(operators, "Add");
                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, opIndex, ExecutionPlan.CreateInputsVector(builder, Array.Empty<int>())).Value);

                    var lhs = chain.Inputs(0);
                    for (var j = 1; j < chain.InputsLength; j++)
                    {
                        var rhs = chain.Inputs(j);
                        var ret = AddTensorValue(builder, valuesOffsets);
                        chainsOffsets.Add(Chain.CreateChain(
                            builder,
                            Chain.CreateInputsVector(builder, new[] { lhs, rhs }),
                            Chain.CreateOutputsVector(builder, new[] { ret }),
                            Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                        );
                        lhs = ret;
                    }

                    var scalarMadOpIndex = GetOperatorIndex(operators, "ScalarMad");
                    var args = new[]
                    {
                        AddIntValue(builder, valuesOffsets, (int)DataType.Float),
                        AddFloatValue(builder, valuesOffsets, 1 / (float)chain.InputsLength),
                        AddFloatValue(builder, valuesOffsets, 0),
                        AddIntValue(builder, valuesOffsets, 0),
                        AddIntValue(builder, valuesOffsets, 0),
                    };
                    var scalarMadInstructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, scalarMadOpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value);
                    chainsOffsets.Add(Chain.CreateChain(
                        builder,
                        Chain.CreateInputsVector(builder, new[] { lhs }),
                        Chain.CreateOutputsVector(builder, new[] { chain.Outputs(0) }),
                        Chain.CreateInstructionsVector(builder, new[] { scalarMadInstructionOffset }))
                    );

                    return true;
                }
                default:
                    return false;
            }
        }

        static bool UpgradeChainV4toV5(ExecutionPlan executionPlan, Chain chain, FlatBufferBuilder builder, List<string> operators, List<Offset<Chain>> chainsOffsets, List<Offset<EValue>> valuesOffsets)
        {
            var instruction = chain.Instructions(0).Value;
            if (instruction.InstrArgsType != InstructionArguments.KernelCall)
                return false;
            var kernel = instruction.InstrArgsAsKernelCall();
            var k = operators[kernel.OpIndex];
            switch (k)
            {
                case "RoiAlign": // RoiAlign has an additional "coordinateTransformMode" argument that defaults to OutputHalfPixel i.e. 0 when upgrading.
                {
                    var input = chain.Inputs(0);
                    var rois = chain.Inputs(1);
                    var batchIndices = chain.Inputs(2);
                    var output = chain.Outputs(0);

                    var argsList = kernel.GetArgsArray().ToList();
                    argsList.Add(AddIntValue(builder, valuesOffsets, 0));
                    var args = argsList.ToArray();

                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, kernel.OpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value);

                    chainsOffsets.Add(Chain.CreateChain(
                        builder,
                        Chain.CreateInputsVector(builder, new[] { input, rois, batchIndices }),
                        Chain.CreateOutputsVector(builder, new[] { output }),
                        Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                    );
                    return true;
                }
                case "OneHot": // OneHot has an additional "allowNegativeIndexes" argument that defaults to true when upgrading.
                {
                    var indices = chain.Inputs(0);
                    var depth = chain.Inputs(1);
                    var values = chain.Inputs(2);
                    var output = chain.Outputs(0);

                    var argsList = kernel.GetArgsArray().ToList();
                    argsList.Add(AddBoolValue(builder, valuesOffsets, true));
                    var args = argsList.ToArray();

                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, kernel.OpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value);

                    chainsOffsets.Add(Chain.CreateChain(
                        builder,
                        Chain.CreateInputsVector(builder, new[] { indices, depth, values }),
                        Chain.CreateOutputsVector(builder, new[] { output }),
                        Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                    );
                    return true;
                }
                default:
                    return false;
            }
        }

        static bool UpgradeChainV5toV6(ExecutionPlan executionPlan, Chain chain, FlatBufferBuilder builder, List<string> operators, List<Offset<Chain>> chainsOffsets, List<Offset<EValue>> valuesOffsets)
        {
            var instruction = chain.Instructions(0).Value;
            if (instruction.InstrArgsType != InstructionArguments.KernelCall)
                return false;
            var kernel = instruction.InstrArgsAsKernelCall();
            var k = operators[kernel.OpIndex];
            switch (k)
            {
                case "ConvTranspose": // ConvTranspose now supports dilations and group
                {
                    var input = chain.Inputs(0);
                    var weights = chain.Inputs(1);
                    var bias = chain.Inputs(2); // bias: optional
                    var output = chain.Outputs(0);

                    var argsListOrig = kernel.GetArgsArray().ToList();
                    var argsList = new List<int>();
                    argsList.Add(argsListOrig[0]); // autopad
                    argsList.Add(AddIntListValue(builder, valuesOffsets, new[] { 1, 1, 1, 1, 1, 1, 1, 1 })); // dilations
                    argsList.Add(AddIntValue(builder, valuesOffsets, 1)); // group
                    argsList.AddRange(argsListOrig.GetRange(1, argsListOrig.Count - 1));
                    var args = argsList.ToArray();

                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, kernel.OpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value);

                    chainsOffsets.Add(Chain.CreateChain(
                        builder,
                        Chain.CreateInputsVector(builder, new[] { input, weights, bias, }),
                        Chain.CreateOutputsVector(builder, new[] { output }),
                        Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                    );
                    return true;
                }
                default:
                    return false;
            }
        }

        static bool UpgradeChainV7toV8(ExecutionPlan executionPlan, Chain chain, FlatBufferBuilder builder,
            List<string> operators, List<Offset<Chain>> chainsOffsets, List<Offset<EValue>> valuesOffsets)
        {
            var instruction = chain.Instructions(0).Value;
            if (instruction.InstrArgsType != InstructionArguments.KernelCall)
                return false;
            var kernel = instruction.InstrArgsAsKernelCall();
            var k = operators[kernel.OpIndex];
            switch (k)
            {
                case "Swish": // Swish has an additional "alpha" argument that defaults to 1.0.
                {
                    var input = chain.Inputs(0);
                    var output = chain.Outputs(0);

                    var argsList = kernel.GetArgsArray().ToList();
                    argsList.Add(AddFloatValue(builder, valuesOffsets, 1.0f));
                    var args = argsList.ToArray();

                    var instructionOffset = Instruction.CreateInstruction(builder, instruction.InstrArgsType, KernelCall.CreateKernelCall(builder, kernel.OpIndex, ExecutionPlan.CreateInputsVector(builder, args)).Value);

                    chainsOffsets.Add(Chain.CreateChain(
                        builder,
                        Chain.CreateInputsVector(builder, new[] { input }),
                        Chain.CreateOutputsVector(builder, new[] { output }),
                        Chain.CreateInstructionsVector(builder, new[] { instructionOffset }))
                    );

                    return true;
                }
                default:
                    return false;
            }
        }

        public static Program Upgrade(Program program)
        {
            if (program.Version == 1)
                program = UpgradeFlatbuffer(program, 2, UpgradeChainV1toV2);
            if (program.Version == 2)
                program = UpgradeFlatbuffer(program, 3);
            if (program.Version == 3)
                program = UpgradeFlatbuffer(program, 4);
            if (program.Version == 4)
                program = UpgradeFlatbuffer(program, 5, UpgradeChainV4toV5);
            if (program.Version == 5)
                program = UpgradeFlatbuffer(program, 6, UpgradeChainV5toV6);
            if (program.Version == 6)
                program = UpgradeFlatbuffer(program, 7);
            if (program.Version == 7)
                program = UpgradeFlatbuffer(program, 8, UpgradeChainV7toV8);
            return program;
        }
    }
}
