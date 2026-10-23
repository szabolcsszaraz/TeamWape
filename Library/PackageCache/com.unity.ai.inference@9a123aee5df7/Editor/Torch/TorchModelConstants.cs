using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Razorvine.Pickle;
using Razorvine.Pickle.Objects;
using UnityEngine;
using StreamReader = System.IO.StreamReader;

namespace Unity.InferenceEngine.Editor.Torch
{
    static class TorchModelConstants
    {
        static TorchModelConstants()
        {
            Unpickler.registerConstructor("torch._utils", "_rebuild_tensor", new TensorObjectConstructor());
            Unpickler.registerConstructor("torch._utils", "_rebuild_tensor_v2", new TensorObjectConstructor());
            Unpickler.registerConstructor("torch._utils", "_rebuild_parameter", new ParameterObjectConstructor());
            Unpickler.registerConstructor("collections", "OrderedDict", new OrderedDictObjectConstructor());
        }

        public static void LoadWeightsFromConfig(Stream configStream, ZipArchive archive, string basePath, Dictionary<string, Tensor> tensors)
        {
            LoadTensorsFromConfig(configStream, archive, basePath, tensors);
        }

        public static void LoadConstantsFromConfig(Stream configStream, ZipArchive archive, string basePath, Dictionary<string, Tensor> tensors)
        {
            LoadTensorsFromConfig(configStream, archive, basePath, tensors);
        }

        static void LoadTensorsFromConfig(Stream configStream, ZipArchive archive, string basePath, Dictionary<string, Tensor> tensors)
        {
            using var reader = new StreamReader(configStream);
            var configJson = reader.ReadToEnd();
            var config = JsonConvert.DeserializeObject<JObject>(configJson);

            if (config == null || !config.ContainsKey("config"))
                return;

            var tensorConfigs = config["config"] as JObject;
            if (tensorConfigs == null)
                return;

            foreach (var kvp in tensorConfigs)
            {
                var tensorName = kvp.Key;
                var tensorConfig = kvp.Value as JObject;
                var tensorMeta = tensorConfig?["tensor_meta"] as JObject;

                if (tensorConfig == null || tensorMeta == null)
                    continue;

                var pathName = tensorConfig["path_name"]?.ToString();
                if (string.IsNullOrEmpty(pathName))
                    continue;

                var weightPath = $"{basePath}/{pathName}";
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(weightPath));
                if (entry == null)
                    continue;

                var sizesArray = tensorMeta["sizes"] as JArray;
                if (sizesArray == null)
                    continue;

                var shape = new int[sizesArray.Count];
                for (int i = 0; i < sizesArray.Count; i++)
                    shape[i] = (sizesArray[i] as JObject)?["as_int"]?.Value<int>() ?? 0;

                var configDtype = tensorMeta["dtype"]?.Value<int>() ?? 0;

                using var entryStream = entry.Open();
                tensors[tensorName] = LoadRawTensor(entryStream, configDtype, shape);
            }
        }

        static Tensor LoadRawTensor(Stream stream, int dtype, int[] shape)
        {
            var tensorShape = new TensorShape(shape);
            using var reader = new BinaryReader(stream);

            // PyTorch ScalarType enum values from:
            // https://github.com/pytorch/pytorch/blob/v2.9.1/c10/core/ScalarType.h
            switch (dtype)
            {
                case 1: // uint8
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = reader.ReadByte();
                    return tensor;
                }
                case 2: // int8
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = (sbyte)reader.ReadByte();
                    return tensor;
                }
                case 3: // int16
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = reader.ReadInt16();
                    return tensor;
                }
                case 4: // int32
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = reader.ReadInt32();
                    return tensor;
                }
                case 5: // int64
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = (int)reader.ReadInt64();
                    return tensor;
                }
                case 6: // float16
                {
                    var tensor = new Tensor<float>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = Mathf.HalfToFloat(reader.ReadUInt16());
                    return tensor;
                }
                case 7: // float32
                {
                    var tensor = new Tensor<float>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = reader.ReadSingle();
                    return tensor;
                }
                case 8: // float64
                {
                    var tensor = new Tensor<float>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = (float)reader.ReadDouble();
                    return tensor;
                }
                case 12: // bool
                {
                    var tensor = new Tensor<int>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = reader.ReadBoolean() ? 1 : 0;
                    return tensor;
                }
                case 13: // bfloat16
                {
                    var tensor = new Tensor<float>(tensorShape);
                    for (var i = 0; i < tensorShape.length; i++)
                        tensor[i] = BitConverter.Int32BitsToSingle((int)((uint)reader.ReadUInt16() << 16));
                    return tensor;
                }
                default:
                    throw new NotSupportedException($"Unsupported tensor dtype: {dtype}");
            }
        }
    }

    class TensorObjectConstructor : IObjectConstructor
    {
        public object construct(object[] args)
        {
            // Arg 0: returned from our custom pickler
            var tensorStream = (TensorStream)args[0];

            // var constructor = new TensorConstructorArgs
            // {
            //     ArchiveIndex = tensorStream.ArchiveIndex,
            //     Data = tensorStream.ArchiveEntry!.Open(),
            //     StorageType = tensorStream.StorageType,
            //     // Arg 1: storage_offset
            //     StorageOffset = (int)args[1],
            //     // Arg 2: tensor_shape
            //     Shape = ,
            //     // Arg 3: stride
            //     Stride = ((object[])args[3]).Select(i => (int)i).ToArray(),
            //     // Arg 4: requires_grad
            //     RequiresGrad = (bool)args[4],
            // };

            // Arg 5: backward_hooks, we don't support adding them in and it's not recommended
            // in PyTorch to serialize them.

            using var Data = tensorStream.ArchiveEntry!.Open();

            var shape = new TensorShape(((object[])args[2]).Select(i => (int)i).ToArray());
            switch (tensorStream.StorageType)
            {
                case "DoubleStorage":
                {
                    var tensor = new Tensor<float>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = (float)reader.ReadDouble();
                    return tensor;
                }
                case "FloatStorage":
                {
                    var tensor = new Tensor<float>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = reader.ReadSingle();
                    return tensor;
                }
                case "HalfStorage":
                {
                    var tensor = new Tensor<float>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = Mathf.HalfToFloat(reader.ReadUInt16());
                    return tensor;
                }
                case "LongStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = (int)reader.ReadInt64();
                    return tensor;
                }
                case "IntStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = reader.ReadInt32();
                    return tensor;
                }
                case "ShortStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = reader.ReadInt16();
                    return tensor;
                }
                case "CharStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = (sbyte)reader.ReadByte();
                    return tensor;
                }
                case "ByteStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = reader.ReadByte();
                    return tensor;
                }
                case "BoolStorage":
                {
                    var tensor = new Tensor<int>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = reader.ReadBoolean() ? 1 : 0;
                    return tensor;
                }
                case "BFloat16Storage":
                {
                    var tensor = new Tensor<float>(shape);
                    using var reader = new BinaryReader(Data);
                    for (var i = 0; i < shape.length; i++)
                        tensor[i] = BitConverter.Int32BitsToSingle((int)((uint)reader.ReadUInt16() << 16));
                    return tensor;
                }
                default:
                    throw new NotSupportedException("Unsupported tensor type: " + tensorStream.StorageType);
            }
        }
    }

    class ParameterObjectConstructor : IObjectConstructor
    {
        public object construct(object[] args)
        {
            return new Parameter
            {
                tensor = (Tensor)args[0],
                requiresGrad = (bool)args[1],
                backwardHooks = (OrderedDict)args[2],
            };
        }
    }

    class Parameter
    {
        public Tensor tensor;
        public bool requiresGrad;
        public object backwardHooks;
    }

    class OrderedDictObjectConstructor : IObjectConstructor
    {
        public object construct(object[] args)
        {
            return new OrderedDict();
        }
    }

    class OrderedDict : Hashtable { }

    class CustomUnpickler : Unpickler
    {
        readonly Dictionary<string, (ZipArchiveEntry, int)> m_Entries;
        readonly bool m_SkipTensorRead;

        public CustomUnpickler(Dictionary<string, (ZipArchiveEntry, int)> entries, bool skipTensorRead)
        {
            m_Entries = entries;
            m_SkipTensorRead = skipTensorRead;
        }

        protected internal override object persistentLoad(object pid)
        {
            // The persistentLoad function in pickler is a way of pickling a key and then loading
            // the data yourself from another source. The `torch.save` function uses this functionality
            // and lists for the pid a tuple with the following items:
            var opid = (object[])pid;

            // Tuple Item0: "storage"
            if ((string)opid[0] != "storage")
                throw new NotImplementedException("Unknown persistent id loaded");

            // Tuple Item1: storage_type (e.g., torch.LongTensor), which is broken into module=torch, name=LongTensor
            string storageType = ((ClassDictConstructor)opid[1]).name;
            // Tuple Item2: key (filename in the archive)
            string archiveKey = (string)opid[2];
            // Tuple Item3: location (cpu/gpu), but we always load onto CPU.
            // Tuple Item4: numElems (the number of elements in the tensor)

            // Retrieve the entry from the archive
            var entry = m_Entries[archiveKey];

            // Send this back, so our TensorObjectConstructor can create our torch.tensor from the object.
            return new TensorStream
            {
                ArchiveIndex = entry!.Item2,
                ArchiveEntry = entry!.Item1,
                StorageType = storageType,
                SkipTensorRead = m_SkipTensorRead,
            };
        }
    }

    class TensorStream
    {
        public int ArchiveIndex { get; set; }
        public ZipArchiveEntry ArchiveEntry { get; set; }
        public string StorageType { get; set; }
        public bool SkipTensorRead { get; set; }
    }
}
