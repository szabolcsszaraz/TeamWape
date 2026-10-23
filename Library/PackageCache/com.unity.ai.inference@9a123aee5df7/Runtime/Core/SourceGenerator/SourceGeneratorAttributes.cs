namespace Unity.InferenceEngine
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    class OperatorAttribute : System.Attribute
    {
        public string category;
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    class InputsAttribute : System.Attribute
    {
        public string[] names;
        public int[] inputCPURead;
        public int[] inputNoDataDependency;
        public bool isVariadic;
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    class OutputsAttribute : System.Attribute
    {
        public string[] names;
        public bool isVariadic;
    }
}
