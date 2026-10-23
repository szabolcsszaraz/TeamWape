using System;
using System.Runtime.InteropServices;

namespace Unity.InferenceEngine.Tokenization
{
    unsafe readonly struct DisposablePointer : IDisposable
    {
        public static DisposablePointer Alloc<T>(int length, out T* pointer) where T : unmanaged
        {
            var ptr = Marshal.AllocHGlobal(sizeof(T) * length);
            pointer = (T*) ptr;
            return new(ptr);
        }

        public static DisposablePointer AllocSpan<T>(int count, out Span<T> span)
            where T : unmanaged
        {
            var ptrHandle = Alloc<T>(count, out var ptr);
            span = new(ptr, count);
            return ptrHandle;
        }

        readonly IntPtr m_Pointer;

        DisposablePointer(IntPtr pointer)
        {
            m_Pointer = pointer;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(m_Pointer);
        }
    }
}
