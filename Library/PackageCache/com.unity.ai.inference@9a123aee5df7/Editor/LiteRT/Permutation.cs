using System;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Editor
{
    /// <summary>
    /// Represents a permutation of tensor axes, used for transpose operations.
    /// The axes are assumed to be the values [0, 1, 2 ... rank - 1] in a certain order.
    /// </summary>
    unsafe struct Permutation : IEquatable<Permutation>
    {
        public int rank;
        fixed int m_Perm[8];

        public Permutation(params int[] perm)
        {
            rank = perm.Length;
            for (var i = 0; i < rank; i++)
                m_Perm[i] = perm[i];
        }

        public int this[int axis]
        {
            get
            {
                Assert.IsTrue(axis >= -rank && axis < rank);
                axis = (axis + rank) % rank;
                return m_Perm[axis];
            }
        }

        /// <summary>
        /// Returns the identity permutation [0, 1, 2, ... rank - 1] for a given rank.
        /// </summary>
        public static Permutation Identity(int rank)
        {
            var ret = new Permutation
            {
                rank = rank
            };
            for (var i = 0; i < rank; i++)
                ret.m_Perm[i] = i;
            return ret;
        }

        /// <summary>
        /// Returns the permutation that swaps the final two axes for a given rank.
        /// [0, 1, 2 ... rank - 1, rank - 2]
        /// </summary>
        public static Permutation Transpose(int rank)
        {
            var ret = new Permutation
            {
                rank = rank
            };
            for (var i = 0; i < rank - 2; i++)
                ret.m_Perm[i] = i;
            ret.m_Perm[rank - 2] = rank - 1;
            ret.m_Perm[rank - 1] = rank - 2;
            return ret;
        }

        /// <summary>
        /// Returns the permutation that moves the last dim to the second dim for a given rank.
        /// [0, rank - 1, 1, 2 ... , rank - 2]
        /// </summary>
        public static Permutation ChannelFirst(int rank)
        {
            var ret = new Permutation
            {
                rank = rank
            };
            ret.m_Perm[1] = rank - 1;
            for (var i = 2; i < rank; i++)
                ret.m_Perm[i] = i - 1;
            return ret;
        }

        /// <summary>
        /// Whether the permutation is the identity permutation.
        /// </summary>
        public bool IsIdentity()
        {
            for (var i = 0; i < rank; i++)
                if (m_Perm[i] != i)
                    return false;
            return true;
        }

        /// <summary>
        /// The inverse permutation.
        /// x.Transpose(p).Transpose(p.Inverse()) = x
        /// x.Transpose(p.Inverse()).Transpose(p) = x
        /// </summary>
        public Permutation Inverse()
        {
            var ret = new Permutation
            {
                rank = rank
            };
            for (var i = 0; i < rank; i++)
                ret.m_Perm[m_Perm[i]] = i;
            return ret;
        }

        /// <summary>
        /// The single permutation that results from applying this permutation then another permutation.
        /// x.Transpose(perm).Transpose(other) = x.Transpose(perm.Compound(other))
        /// </summary>
        public Permutation Compound(Permutation other)
        {
            var ret = new Permutation
            {
                rank = rank
            };
            for (var i = 0; i < rank; i++)
                ret.m_Perm[i] = other.m_Perm[m_Perm[i]];
            return ret;
        }

        public int[] ToArray()
        {
            var ret = new int[rank];
            for (var i = 0; i < rank; i++)
                ret[i] = m_Perm[i];
            return ret;
        }

        public static bool operator ==(Permutation a, Permutation b)
        {
            if (a.rank != b.rank)
                return false;
            for (var i = 0; i < a.rank; ++i)
            {
                if (a.m_Perm[i] != b.m_Perm[i])
                    return false;
            }

            return true;
        }

        public static bool operator !=(Permutation a, Permutation b)
        {
            return !(a == b);
        }

        public bool Equals(Permutation other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is Permutation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(rank, HashCode.Combine(m_Perm[0], m_Perm[1], m_Perm[2], m_Perm[3], m_Perm[4], m_Perm[5], m_Perm[6], m_Perm[7]));
        }
    }
}
