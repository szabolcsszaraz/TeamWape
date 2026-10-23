
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a pseudo-random number generator used by Sentis.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class Random
    {
        const uint k_DefaultSeed = 0x6E624EB7u;

        /// <summary>
        /// Static global Mathematics.Random used for random values when no seed provided
        /// </summary>
        static Mathematics.Random s_Random = new (k_DefaultSeed);

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnLoad()
        {
            s_Random = new Mathematics.Random(k_DefaultSeed);
        }
#endif
        /// <summary>
        /// Sets the global Sentis random state for random values without an explicit seed.
        /// </summary>
        /// <param name="seed">The seed to set the state to</param>
        public static void SetSeed(int seed)
        {
            var uintSeed = (seed == 0) ? k_DefaultSeed : (uint)seed;
            s_Random = new Mathematics.Random(uintSeed);
        }

        // Local Mathematics.Random used for random values when seed is provided
        Mathematics.Random? m_Random;

        internal Random() { }

        internal Random(int seed)
        {
            var uintSeed = (seed == 0) ? k_DefaultSeed : (uint)seed;
            m_Random = new Mathematics.Random(uintSeed);
        }

        // Returns int with random bytes to be used as seed for Random Op
        internal int NextSeed()
        {
            if (m_Random.HasValue)
            {
                var random = m_Random.Value;
                var result = random.NextInt(int.MinValue, int.MaxValue);
                m_Random = random; // Update the struct state
                return result;
            }
            else
            {
                return s_Random.NextInt(int.MinValue, int.MaxValue);
            }
        }

        // Returns uint with random bytes to be used as seed inside Op and be passed to a job or as a seed for Mathematics.Random
        internal static uint GetSeed(int? seed)
        {
            return seed.HasValue ? (uint)seed.Value : (uint)s_Random.NextInt(int.MinValue, int.MaxValue);
        }
    }
}
