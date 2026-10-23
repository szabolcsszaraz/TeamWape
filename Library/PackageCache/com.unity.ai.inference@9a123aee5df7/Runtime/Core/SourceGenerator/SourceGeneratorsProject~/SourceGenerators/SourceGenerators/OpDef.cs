using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SourceGenerators;

/// <summary>
/// Intermediate representation of an operator, with its name, inputs, outputs and arguments.
/// This can be generated from different sources, e.g. Layer class with attribute or native_functions.yaml.
/// Then it can be used to generate the necessary partial classes and methods.
/// </summary>
public class OpDef
{
    public string m_Name;
    public string m_Category;
    public bool m_IsRandom;
    public bool m_IsInputsVariadic;
    public OpInput[] m_Inputs;
    public bool m_IsOutputsVariadic;
    public OpOutput[] m_Outputs;
    public OpField[] m_Fields;

    /// <summary>
    /// Construct OpDef from classSymbol, used when the Layer class is already defined in code.
    /// </summary>
    public OpDef(INamedTypeSymbol classSymbol)
    {
        m_Name = classSymbol.Name;
        m_IsRandom = OperatorUtilities.InheritsFrom(classSymbol, "RandomLayer");

        if (OperatorUtilities.TryGetAttribute(classSymbol, "Operator", out var operatorAttribute))
        {
            foreach (var namedArg in operatorAttribute.NamedArguments)
            {
                if (namedArg.Key.Equals("category"))
                    m_Category = (string)namedArg.Value.Value;
            }
        }

        var publicFields = OperatorUtilities.GetAllPublicFields(classSymbol);
        m_Fields = new OpField[publicFields.Count];
        for (var i = 0; i < publicFields.Count; i++)
            m_Fields[i] = new OpField(publicFields[i]);

        if (OperatorUtilities.TryGetAttribute(classSymbol, "Inputs", out var inputsAttribute))
        {
            string[] names = null;
            int[] cpuRead = null;
            int[] noDataDependency = null;

            foreach (var namedArg in inputsAttribute.NamedArguments)
            {
                if (namedArg.Key.Equals("isVariadic") && (bool)namedArg.Value.Value)
                    m_IsInputsVariadic = true;

                if (namedArg.Key.Equals("names"))
                    names = namedArg.Value.Values.Select(n => (string)n.Value).ToArray();

                if (namedArg.Key.Equals("inputCPURead") && !namedArg.Value.IsNull)
                    cpuRead = namedArg.Value.Values.Select(n => (int)n.Value).ToArray();

                if (namedArg.Key.Equals("inputNoDataDependency") && !namedArg.Value.IsNull)
                    noDataDependency = namedArg.Value.Values.Select(n => (int)n.Value).ToArray();
            }

            if (names != null)
            {
                m_Inputs = new OpInput[names.Length];
                for (var i = 0; i < names.Length; i++)
                {
                    m_Inputs[i] = new OpInput
                    {
                        name = names[i],
                        isCpuRead = cpuRead != null && cpuRead.Contains(i),
                        isNoDataDependency = noDataDependency != null && noDataDependency.Contains(i),
                    };
                }
            }
        }

        if (OperatorUtilities.TryGetAttribute(classSymbol, "Outputs", out var outputsAttribute))
        {
            string[] names = null;

            foreach (var namedArg in outputsAttribute.NamedArguments)
            {
                if (namedArg.Key.Equals("isVariadic") && (bool)namedArg.Value.Value)
                    m_IsOutputsVariadic = true;

                if (namedArg.Key.Equals("names"))
                    names = namedArg.Value.Values.Select(n => (string)n.Value).ToArray();
            }

            if (names != null)
            {
                m_Outputs = new OpOutput[names.Length];
                for (var i = 0; i < names.Length; i++)
                {
                    m_Outputs[i] = new OpOutput
                    {
                        name = names[i]
                    };
                }
            }
        }
    }

    public void OpenClass(IndentedTextWriter codeWriter, bool isInheritFromLayer)
    {
        codeWriter.WriteLine(isInheritFromLayer ? $"partial class {m_Name} : Layer" : $"partial class {m_Name}");
        codeWriter.WriteLine('{');
        codeWriter.Indent++;
    }

    public void WriteOpName(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine($"public override string opName => \"{m_Name}\";");
    }

    public void WriteCategory(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine($"internal override string category => \"{m_Category}\";");
    }

    public void WriteProfilerMarker(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine($"static readonly ProfilerMarker k_ProfilerMarker = new(\"InferenceEngine.Layer.{m_Name}\");");
        codeWriter.WriteLine();

        codeWriter.WriteLine("public override ProfilerMarker profilerMarker => k_ProfilerMarker;");
    }

    public void CloseClass(IndentedTextWriter codeWriter)
    {
        codeWriter.Indent--;
        codeWriter.WriteLine('}');
    }

    public void WriteConstructor(IndentedTextWriter codeWriter)
    {
        codeWriter.Write($"public {m_Name}(");

        for (var i = 0; i < m_Fields.Length; i++)
        {
            var field = m_Fields[i];
            if (i > 0)
                codeWriter.Write(", ");
            codeWriter.Write($"{field.type.Signature()} {field.name}");
        }

        codeWriter.WriteLine(")");
        codeWriter.WriteLine("{");

        codeWriter.Indent++;
        foreach (var field in m_Fields)
        {
            codeWriter.WriteLine($"this.{field.name} = {field.name};");
        }

        if (m_IsRandom)
            codeWriter.WriteLine("ResetSeed();");
        codeWriter.Indent--;

        codeWriter.WriteLine("}");
    }

    public void WritePartialInference(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine("internal override void InferPartial(PartialInferenceContext ctx)");
        codeWriter.WriteLine("{");
        codeWriter.Indent++;
        var args = new List<string>();
        if (m_IsInputsVariadic)
        {
            codeWriter.WriteLine("var inputTensors = new PartialTensor[inputs.Length];");
            codeWriter.WriteLine("for (var i = 0; i < inputs.Length; i++)");
            codeWriter.WriteLine("    inputTensors[i] = ctx.GetPartialTensor(inputs[i]);");
            args.Add("inputTensors");
        }
        else
        {
            for (var i = 0; i < m_Inputs.Length; i++)
            {
                codeWriter.WriteLine($"var {m_Inputs[i].name} = ctx.GetPartialTensor(inputs[{i}]);");
                args.Add(m_Inputs[i].name);
            }
        }

        for (var i = 0; i < m_Fields.Length; i++)
        {
            args.Add(m_Fields[i].name);
        }

        if (m_IsOutputsVariadic || m_Outputs.Length > 1)
        {
            codeWriter.WriteLine($"var outputTensors = InferPartial({string.Join(", ", args)});");
            codeWriter.WriteLine("for (var i = 0; i < outputTensors.Length; i++)");
            codeWriter.WriteLine("    ctx.AddPartialTensor(outputs[i], outputTensors[i]);");
        }
        else
        {
            codeWriter.WriteLine($"var outputTensor = InferPartial({string.Join(", ", args)});");
            codeWriter.WriteLine("ctx.AddPartialTensor(outputs[0], outputTensor);");
        }
        codeWriter.Indent--;

        codeWriter.WriteLine("}");
    }

    public void WriteToString(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine("public override string ToString()");
        codeWriter.WriteLine('{');
        codeWriter.Indent++;

        codeWriter.Write("return $\"{opName} - ");
        codeWriter.Write("inputs: [{string.Join(\", \", inputs)}], outputs: [{string.Join(\", \", outputs)}]");
        foreach (var field in m_Fields)
        {
            codeWriter.Write($", {field.name}: ");
            if (field.type.isArray)
                codeWriter.Write($"[{{({field.name} == null ? \"null\" : string.Join(\", \", {field.name}))}}]");
            else
                codeWriter.Write($"{{{field.name}}}");
        }

        codeWriter.Write("\";");
        codeWriter.WriteLine();
        codeWriter.Indent--;
        codeWriter.WriteLine('}');
    }

    public void WriteGetOutputNames(IndentedTextWriter codeWriter)
    {
        if (m_IsOutputsVariadic)
        {
            codeWriter.WriteLine($"internal override string[] GetOutputNames()");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine("var outputNames = new string[outputs.Length];");
            codeWriter.WriteLine("for (var i = 0; i < outputNames.Length; i++)");
            codeWriter.Indent++;
            codeWriter.WriteLine("outputNames[i] = $\"output_{i}\";");
            codeWriter.Indent--;
            codeWriter.WriteLine($"return outputNames;");

            codeWriter.Indent--;
            codeWriter.WriteLine("}");
        }
        else if (m_Outputs != null)
        {
            if (m_Outputs.Length > 0)
            {
                codeWriter.Write("static string[] k_OutputNames = new[] { ");
                for (var i = 0; i < m_Outputs.Length; i++)
                {
                    if (i > 0)
                        codeWriter.Write(", ");
                    codeWriter.Write($"\"{m_Outputs[i].name}\"");
                }

                codeWriter.WriteLine(" };");
            }
            else
            {
                codeWriter.Write("static string[] k_OutputNames = Array.Empty<string>();");
            }

            codeWriter.WriteLine();
            codeWriter.WriteLine($"internal override string[] GetOutputNames()");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine($"return k_OutputNames;");

            codeWriter.Indent--;
            codeWriter.WriteLine("}");
        }
    }

    public void WriteSetOutputs(IndentedTextWriter codeWriter)
    {
        if (m_IsOutputsVariadic)
        {
            codeWriter.WriteLine($"public {m_Name} SetOutputs(int[] outputs)");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine($"this.outputs = outputs;");
            codeWriter.WriteLine($"return this;");
            codeWriter.Indent--;
            codeWriter.WriteLine('}');
            codeWriter.WriteLine();
        }

        if (m_Outputs != null)
        {
            codeWriter.Write($"public {m_Name} SetOutputs(");
            for (var i = 0; i < m_Outputs.Length; i++)
            {
                if (i > 0)
                    codeWriter.Write(", ");
                codeWriter.Write($"int {m_Outputs[i].name}");
            }

            codeWriter.WriteLine($")");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.Write($"this.outputs = new int[] {{ ");
            for (var i = 0; i < m_Outputs.Length; i++)
            {
                if (i > 0)
                    codeWriter.Write(", ");
                codeWriter.Write(m_Outputs[i].name);
            }

            codeWriter.WriteLine($" }};");
            codeWriter.WriteLine($"return this;");
            codeWriter.Indent--;
            codeWriter.WriteLine('}');
            codeWriter.WriteLine();
        }

        if (m_IsOutputsVariadic || m_Outputs?.Length > 1)
        {
            codeWriter.WriteLine("internal override bool IsOutputList => true;");
            codeWriter.WriteLine();
        }
    }

    public void WriteGetInputNames(IndentedTextWriter codeWriter)
    {
        if (m_IsInputsVariadic)
        {
            codeWriter.WriteLine($"internal override string[] GetInputNames()");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine("var inputNames = new string[inputs.Length];");
            codeWriter.WriteLine("for (var i = 0; i < inputNames.Length; i++)");
            codeWriter.Indent++;
            codeWriter.WriteLine("inputNames[i] = $\"input_{i}\";");
            codeWriter.Indent--;
            codeWriter.WriteLine($"return inputNames;");

            codeWriter.Indent--;
            codeWriter.WriteLine("}");
        }
        else if (m_Inputs != null)
        {
            if (m_Inputs.Length > 0)
            {
                codeWriter.Write("static string[] k_InputNames = new[] { ");
                for (var i = 0; i < m_Inputs.Length; i++)
                {
                    if (i > 0)
                        codeWriter.Write(", ");
                    codeWriter.Write($"\"{m_Inputs[i].name}\"");
                }

                codeWriter.WriteLine(" };");
            }
            else
            {
                codeWriter.Write("static string[] k_InputNames = Array.Empty<string>();");
            }

            codeWriter.WriteLine();
            codeWriter.WriteLine($"internal override string[] GetInputNames()");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine($"return k_InputNames;");

            codeWriter.Indent--;
            codeWriter.WriteLine("}");
        }
    }

    public void WriteSetInputs(IndentedTextWriter codeWriter)
    {
        if (m_IsInputsVariadic)
        {
            codeWriter.WriteLine($"public {m_Name} SetInputs(int[] inputs)");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.WriteLine($"this.inputs = inputs;");
            codeWriter.WriteLine($"return this;");
            codeWriter.Indent--;
            codeWriter.WriteLine('}');
            codeWriter.WriteLine();
        }

        if (m_Inputs != null)
        {
            codeWriter.Write($"public {m_Name} SetInputs(");
            for (var i = 0; i < m_Inputs.Length; i++)
            {
                if (i > 0)
                    codeWriter.Write(", ");
                codeWriter.Write($"int {m_Inputs[i].name}");
            }

            codeWriter.WriteLine($")");
            codeWriter.WriteLine('{');
            codeWriter.Indent++;
            codeWriter.Write($"this.inputs = new int[] {{ ");
            for (var i = 0; i < m_Inputs.Length; i++)
            {
                if (i > 0)
                    codeWriter.Write(", ");
                codeWriter.Write(m_Inputs[i].name);
            }

            codeWriter.WriteLine($" }};");
            codeWriter.WriteLine($"return this;");
            codeWriter.Indent--;
            codeWriter.WriteLine('}');
            codeWriter.WriteLine();
        }

        var indicesCPURead = new List<int>();
        for (var i = 0; m_Inputs != null && i < m_Inputs.Length; i++)
        {
            if (m_Inputs[i].isCpuRead)
                indicesCPURead.Add(i);
        }

        if (indicesCPURead.Count > 0)
        {
            codeWriter.WriteLine($"internal override bool IsInputCPURead(int i) => i is {string.Join(" or ", indicesCPURead)};");
            codeWriter.WriteLine();
        }

        var indicesNoDataDependency = new List<int>();
        for (var i = 0; m_Inputs != null && i < m_Inputs.Length; i++)
        {
            if (m_Inputs[i].isNoDataDependency)
                indicesNoDataDependency.Add(i);
        }

        if (indicesNoDataDependency.Count > 0)
        {
            codeWriter.WriteLine($"internal override bool IsInputNoDataDependency(int i) => i is {string.Join(" or ", indicesNoDataDependency)};");
            codeWriter.WriteLine();
        }
    }

    public void WriteSerializeFields(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine("internal override void SerializeFields(FlatBufferBuilder builder, List<Offset<EValue>> values)");
        codeWriter.WriteLine('{');
        codeWriter.Indent++;

        for (var j = 0; j < m_Fields.Length; j++)
        {
            var field = m_Fields[j];
            if (field.type.isArray)
            {
                if (field.type == OpFieldType.IntArray)
                    codeWriter.WriteLine($"values.Add({field.name} is null ? EValue.CreateEValue(builder, KernelTypes.NONE) : EValue.CreateEValue(builder, KernelTypes.IntList, IntList.CreateIntList(builder, IntList.CreateItemsVector(builder, {field.name})).Value));");
                else if (field.type == OpFieldType.FloatArray)
                    codeWriter.WriteLine($"values.Add({field.name} is null ? EValue.CreateEValue(builder, KernelTypes.NONE) : EValue.CreateEValue(builder, KernelTypes.FloatList, FloatList.CreateFloatList(builder, FloatList.CreateItemsVector(builder, {field.name})).Value));");
                else if (field.type.isEnum)
                    codeWriter.WriteLine($"values.Add({field.name} is null ? EValue.CreateEValue(builder, KernelTypes.NONE) : EValue.CreateEValue(builder, KernelTypes.IntList, IntList.CreateIntList(builder, IntList.CreateItemsVector(builder, System.Array.ConvertAll({field.name}, value => (int)value))).Value));");
                else
                    codeWriter.WriteLine($"{field.name} type not implemented");
            }
            else
            {
                if (field.type == OpFieldType.Int)
                    codeWriter.WriteLine($"values.Add(EValue.CreateEValue(builder, KernelTypes.Int, Int.CreateInt(builder, {field.name}).Value));");
                else if (field.type == OpFieldType.Float)
                    codeWriter.WriteLine($"values.Add(EValue.CreateEValue(builder, KernelTypes.Float, Float.CreateFloat(builder, {field.name}).Value));");
                else if (field.type == OpFieldType.Bool)
                    codeWriter.WriteLine($"values.Add(EValue.CreateEValue(builder, KernelTypes.Bool, Bool.CreateBool(builder, {field.name}).Value));");
                else if (field.type == OpFieldType.Byte)
                    codeWriter.WriteLine($"values.Add(EValue.CreateEValue(builder, KernelTypes.Int, Int.CreateInt(builder, (int){field.name}).Value));");
                else if (field.type == OpFieldType.String)
                    codeWriter.WriteLine($"values.Add({field.name} is null ? EValue.CreateEValue(builder, KernelTypes.NONE) : EValue.CreateEValue(builder, KernelTypes.String, SentisFlatBuffer.String.CreateString(builder, builder.CreateString({field.name})).Value));");
                else if (field.type.isEnum)
                    codeWriter.WriteLine($"values.Add(EValue.CreateEValue(builder, KernelTypes.Int, Int.CreateInt(builder, (int){field.name}).Value));");
                else
                    codeWriter.WriteLine($"{field.name} type not implemented");
            }
        }

        codeWriter.Indent--;
        codeWriter.WriteLine('}');
    }

    public void WriteDeserializeLayer(IndentedTextWriter codeWriter)
    {
        codeWriter.WriteLine($"internal static Layer DeserializeLayer(Chain chain, ExecutionPlan executionPlan)");
        codeWriter.WriteLine("{");
        codeWriter.Indent++;
        codeWriter.WriteLine("var kernel = chain.Instructions(0).Value.InstrArgsAsKernelCall();");
        for (var j = 0; j < m_Fields.Length; j++)
        {
            var field = m_Fields[j];
            if (field.type.isArray)
            {
                if (field.type == OpFieldType.IntArray)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.Val<IntList>()?.GetItemsArray();");
                else if (field.type == OpFieldType.FloatArray)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.Val<FloatList>()?.GetItemsArray();");
                else if (field.type.isEnum)
                    codeWriter.WriteLine($"var {field.name} = System.Array.ConvertAll(executionPlan.Values(kernel.Args({j})).Value.Val<IntList>()?.GetItemsArray(), value => ({field.type.typeName})value);");
                else
                    codeWriter.WriteLine($"{field.name} type not implemented");
            }
            else
            {
                if (field.type == OpFieldType.Int)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.ValAsInt().IntVal;");
                else if (field.type == OpFieldType.Float)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.ValAsFloat().FloatVal;");
                else if (field.type == OpFieldType.Bool)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.ValAsBool().BoolVal;");
                else if (field.type == OpFieldType.Byte)
                    codeWriter.WriteLine($"var {field.name} = (byte)executionPlan.Values(kernel.Args({j})).Value.ValAsInt().IntVal;");
                else if (field.type == OpFieldType.String)
                    codeWriter.WriteLine($"var {field.name} = executionPlan.Values(kernel.Args({j})).Value.ValAsString().StringVal;");
                else if (field.type.isEnum)
                    codeWriter.WriteLine($"var {field.name} = ({field.type.typeName})executionPlan.Values(kernel.Args({j})).Value.ValAsInt().IntVal;");
                else
                    codeWriter.WriteLine($"{field.name} type not implemented");
            }
        }

        codeWriter.Write($"var layer = new {m_Name}(");
        codeWriter.Write(string.Join(", ", m_Fields.Select(f => f.name)));
        codeWriter.WriteLine(");");
        codeWriter.WriteLine("layer.inputs = chain.GetInputsArray();");
        codeWriter.WriteLine("layer.outputs = chain.GetOutputsArray();");
        codeWriter.WriteLine("return layer;");
        codeWriter.Indent--;
        codeWriter.WriteLine("}");
    }
}

public class OpInput
{
    public string name;
    public bool isCpuRead;
    public bool isNoDataDependency;
}

public class OpOutput
{
    public string name;
}

public class OpField
{
    public OpFieldType type;
    public string name;

    public OpField(IFieldSymbol field)
    {
        name = field.Name;
        if (field.Type is IArrayTypeSymbol arrayType)
        {
            if (arrayType.ElementType.SpecialType == SpecialType.System_Int32)
                type = OpFieldType.IntArray;
            else if (arrayType.ElementType.SpecialType == SpecialType.System_Single)
                type = OpFieldType.FloatArray;
            else if (arrayType.ElementType.TypeKind == TypeKind.Enum)
                type = OpFieldType.EnumArray(arrayType.ElementType.Name);
        }
        else if (field.Type.SpecialType == SpecialType.System_Int32)
            type = OpFieldType.Int;
        else if (field.Type.SpecialType == SpecialType.System_Single)
            type = OpFieldType.Float;
        else if (field.Type.SpecialType == SpecialType.System_Boolean)
            type = OpFieldType.Bool;
        else if (field.Type.SpecialType == SpecialType.System_Byte)
            type = OpFieldType.Byte;
        else if (field.Type.SpecialType == SpecialType.System_String)
            type = OpFieldType.String;
        else if (field.Type.TypeKind == TypeKind.Enum)
            type = OpFieldType.Enum(field.Type.Name);
    }
}

public readonly record struct OpFieldType(string typeName, bool isArray, bool isEnum)
{
    public readonly string typeName = typeName;
    public readonly bool isArray = isArray;
    public readonly bool isEnum = isEnum;

    public string Signature()
    {
        return isArray ? typeName + "[]" : typeName;
    }

    public static OpFieldType Int = new("int", false, false);
    public static OpFieldType Float = new("float", false, false);
    public static OpFieldType Bool = new("bool", false, false);
    public static OpFieldType Byte = new("byte", false, false);
    public static OpFieldType String = new("string", false, false);
    public static OpFieldType IntArray = new("int", true, false);
    public static OpFieldType FloatArray = new("float", true, false);
    public static OpFieldType Enum(string name) => new(name, false, true);
    public static OpFieldType EnumArray(string name) => new(name, true, true);
}
