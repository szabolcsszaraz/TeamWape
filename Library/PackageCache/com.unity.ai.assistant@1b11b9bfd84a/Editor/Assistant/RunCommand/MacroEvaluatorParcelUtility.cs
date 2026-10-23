using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    interface IMacroEvaluatorParcelSerializer
    {
        string CreateParcel(string assemblyPath, string typeName, string methodName, Type[] parameterTypes, object[] arguments);
    }

    static class MacroEvaluatorParcelUtility
    {
        static readonly IMacroEvaluatorParcelSerializer m_Serializer;

        static MacroEvaluatorParcelUtility()
        {
#if UNITY_6000_4_OR_NEWER
            m_Serializer = new DataContractMacroEvaluatorParcelSerializer();
#else
            m_Serializer = new BinaryFormatterMacroEvaluatorParcelSerializer();
#endif
        }

        public static string CreateParcel(string assemblyPath, string typeName, string methodName, Type[] parameterTypes, object[] arguments)
        {
            return m_Serializer.CreateParcel(assemblyPath, typeName, methodName, parameterTypes, arguments);
        }
    }

    class BinaryFormatterMacroEvaluatorParcelSerializer : IMacroEvaluatorParcelSerializer
    {
        public string CreateParcel(string assemblyPath, string typeName, string methodName, Type[] parameterTypes, object[] arguments)
        {
            using var stream = new MemoryStream();

#pragma warning disable SYSLIB0011, UAC0023
            var formatter = new BinaryFormatter { AssemblyFormat = FormatterAssemblyStyle.Simple };

            formatter.Serialize(stream, "com.unity3d.automation");
            formatter.Serialize(stream, assemblyPath);
            formatter.Serialize(stream, typeName);
            formatter.Serialize(stream, methodName);
            formatter.Serialize(stream, parameterTypes.Select(GetBinaryFormatterTypeName).ToArray());
            formatter.Serialize(stream, arguments);
#pragma warning restore SYSLIB0011, UAC0023

            return Convert.ToBase64String(stream.ToArray());
        }

        static string GetBinaryFormatterTypeName(Type type)
        {
            return type.AssemblyQualifiedName;
        }
    }

    class DataContractMacroEvaluatorParcelSerializer : IMacroEvaluatorParcelSerializer
    {
        public string CreateParcel(string assemblyPath, string typeName, string methodName, Type[] parameterTypes, object[] arguments)
        {
            var parameterTypeNames = parameterTypes.Select(GetDataContractTypeName).ToArray();
            byte[] remoteMethodCallParcelData;
            using (var stream = new MemoryStream())
            {
                var remoteMethodCallParcel = new EvalRemoteMethodCallParcel
                {
                    AssemblyPath = assemblyPath,
                    TypeName = typeName,
                    MethodName = methodName,
                    ParameterTypeNames = parameterTypeNames,
                    Arguments = arguments
                };
                var serializer = new DataContractSerializer(typeof(EvalRemoteMethodCallParcel), parameterTypes);
                serializer.WriteObject(stream, remoteMethodCallParcel);
                remoteMethodCallParcelData = stream.ToArray();
            }

            using (var stream = new MemoryStream())
            {
                var dataParcel = new EvalDataParcel
                {
                    KnownTypeAssemblyPaths = parameterTypes.Select(GetKnownTypeAssemblyPath).ToArray(),
                    KnownTypeNames = parameterTypeNames,
                    RemoteMethodCallParcelData = remoteMethodCallParcelData
                };
                var serializer = new DataContractSerializer(typeof(EvalDataParcel));
                serializer.WriteObject(stream, dataParcel);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        static string GetDataContractTypeName(Type type)
        {
            if (ShouldRedirectToNetStandard(type))
                return $"{type.FullName}, netstandard";

            return type.AssemblyQualifiedName;
        }

        static string GetKnownTypeAssemblyPath(Type type)
        {
            if (ShouldRedirectToNetStandard(type))
                return string.Empty;

            return AssemblyUtils.GetAssemblyPath(type.Assembly);
        }

        static bool ShouldRedirectToNetStandard(Type type)
        {
            return type.Assembly == typeof(object).Assembly || type.Assembly == typeof(Console).Assembly;
        }

        [DataContract(Name = "EvalDataParcel", Namespace = "com.unity3d.automation")]
        class EvalDataParcel
        {
            [DataMember]
            public string[] KnownTypeAssemblyPaths { get; set; } = Array.Empty<string>();

            [DataMember]
            public string[] KnownTypeNames { get; set; } = Array.Empty<string>();

            [DataMember]
            public byte[] RemoteMethodCallParcelData { get; set; } = Array.Empty<byte>();
        }

        [DataContract(Name = "EvalRemoteMethodCallParcel", Namespace = "com.unity3d.automation")]
        class EvalRemoteMethodCallParcel
        {
            [DataMember]
            public string AssemblyPath { get; set; } = string.Empty;

            [DataMember]
            public string TypeName { get; set; } = string.Empty;

            [DataMember]
            public string MethodName { get; set; } = string.Empty;

            [DataMember]
            public string[] ParameterTypeNames { get; set; } = Array.Empty<string>();

            [DataMember]
            public object[] Arguments { get; set; } = Array.Empty<object>();
        }
    }
}
