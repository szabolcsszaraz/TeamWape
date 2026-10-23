using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Unity.InferenceEngine.Serialization
{
    // see https://huggingface.co/docs/safetensors/index
    static class Safetensors
    {
        public static Dictionary<string, Tensor> Load(Stream stream)
        {
            using var binaryReader = new BinaryReader(stream);
            var headerSize = binaryReader.ReadUInt64();
            var headerBytes = binaryReader.ReadBytes((int)headerSize);
            var headerJson = Encoding.UTF8.GetString(headerBytes);
            var safetensorsInfo = JsonConvert.DeserializeObject<SafetensorsInfo>(headerJson);
            // create sorted set of the tensor infos so we can iterate in order when reading from the stream
            var sortedTensorInfos = new SortedSet<(string, TensorInfo)>(Comparer<(string, TensorInfo)>.Create((x, y) =>
            {
                var cmp = x.Item2.data_offsets[0].CompareTo(y.Item2.data_offsets[0]);
                if (cmp != 0)
                    return cmp;
                // if starts are the same compare the ends, this deals with tensors of length 0 correctly
                cmp = x.Item2.data_offsets[1].CompareTo(y.Item2.data_offsets[1]);
                // in last resort, compare the names
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Item1, y.Item1);
            }));
            foreach (var (name, tensorInfo) in safetensorsInfo.tensors)
                sortedTensorInfos.Add((name, tensorInfo));

            // iterate through sorted infos and read from stream
            var tensors = new Dictionary<string, Tensor>();
            ulong currentOffset = 0;
            foreach (var (name, tensorInfo) in sortedTensorInfos)
            {
                Logger.AssertIsTrue(tensorInfo.shape.Count <= 8, "Tensors of rank greater than 8 are not supported.");
                var shape = new int[tensorInfo.shape.Count];
                for (var i = 0; i < tensorInfo.shape.Count; i++)
                {
                    Logger.AssertIsTrue(tensorInfo.shape[i] <= int.MaxValue, "Tensors with dims of size greater than int.MaxValue are not supported.");
                    shape[i] = (int)tensorInfo.shape[i];
                }

                var tensorShape = new TensorShape(shape);
                Tensor tensor = tensorInfo.dtype switch
                {
                    "F32" => new Tensor<float>(tensorShape, null),
                    "I32" => new Tensor<int>(tensorShape, null),
                    "I16" => new Tensor<short>(tensorShape, null),
                    "U8" => new Tensor<byte>(tensorShape, null),
                    _ => throw new Exception($"Tensor \"{name}\": type {tensorInfo.dtype} is not supported."),
                };

                Logger.AssertIsTrue(tensorInfo.data_offsets[0] == currentOffset, "Safetensor error: the byte buffer needs to be entirely indexed, cannot be shared, and cannot contain holes.");
                var dataLength = tensorInfo.data_offsets[1] - tensorInfo.data_offsets[0];
                Logger.AssertIsTrue(dataLength <= int.MaxValue, "Tensors with byte length greater than int.MaxValue are not supported.");

                // create byte array of the right length, note this may be longer than dataLength due to 4 byte padding rules
                var data = new byte[sizeof(int) * tensor.count];
                var bytesRead = stream.Read(data, 0, (int)dataLength);
                Logger.AssertIsTrue(bytesRead == (int)dataLength, "Safetensor error: the byte buffer couldn't be read from.");
                currentOffset = tensorInfo.data_offsets[1];

                tensor.dataOnBackend = new CPUTensorData(new NativeTensorArrayFromManagedArray(data, 0, tensor.count));
                tensors[name] = tensor;
            }

            return tensors;
        }

        public static void Save(Stream stream, Dictionary<string, Tensor> tensors, Dictionary<string, string> metadata = null)
        {
            var safetensorsInfo = new SafetensorsInfo();
            if (metadata != null)
                safetensorsInfo.metadata = metadata;
            ulong currentOffset = 0;
            var tensorNames = new List<string>();
            foreach (var (name, tensor) in tensors)
            {
                tensorNames.Add(name);
                var tensorInfo = new TensorInfo();
                tensorInfo.dtype = tensor.dataType switch
                {
                    DataType.Float => "F32",
                    DataType.Int => "I32",
                    DataType.Short => "I16",
                    DataType.Byte => "U8",
                    _ => throw new Exception($"Tensor \"{name}\": type {tensor.dataType} is not supported."),
                };
                tensorInfo.shape = new List<long>();
                for (var i = 0; i < tensor.shape.rank; i++)
                    tensorInfo.shape.Add(tensor.shape[i]);
                tensorInfo.data_offsets = new List<ulong>();
                tensorInfo.data_offsets.Add(currentOffset);
                var dataLength = 4 * tensor.count;
                currentOffset += (ulong)dataLength;
                tensorInfo.data_offsets.Add(currentOffset);
                safetensorsInfo.tensors[name] = tensorInfo;
            }
            var headerJson = JsonConvert.SerializeObject(safetensorsInfo);
            var headerBytes = Encoding.UTF8.GetBytes(headerJson);
            using var binaryWriter = new BinaryWriter(stream);
            binaryWriter.Write((ulong)headerBytes.Length);
            binaryWriter.Write(headerBytes);

            foreach (var tensorName in tensorNames)
            {
                var tensor = tensors[tensorName];
                var tensorInfo = safetensorsInfo.tensors[tensorName];
                if (tensor.shape.length > 0)
                {
                    var dataOnBackend = tensor.dataOnBackend as CPUTensorData;
                    var dataLength = tensorInfo.data_offsets[1] - tensorInfo.data_offsets[0];
                    var dataSpan = dataOnBackend.Download<byte>((int)dataLength);
                    binaryWriter.Write(dataSpan);
                }
            }
        }
    }
}
