using System;

namespace Unity.AI.Mesh.Services.Stores.States
{
    [Serializable]
    record MeshSettingsState
    {
        public MeshPivotMode pivotMode = MeshPivotMode.Center;
    }
}