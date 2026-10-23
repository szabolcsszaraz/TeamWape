using System;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents helper functions to iterate through arguments of nodes, which can be nested.
    /// </summary>
    static class GraphUtils
    {
        /// <summary>
        /// Iterates through a list of arguments, replacing nodes by using 'fn'.
        /// </summary>
        public static Argument[] MapArg(Argument[] args, Func<Node, Node> fn)
        {
            if (args is null)
                return null;

            var result = new Argument[args.Length];
            for (var i = 0; i < args.Length; i++)
                result[i] = MapArg(args[i], fn);

            return result;
        }

        public static Argument MapArg(Argument arg, Func<Node, Node> fn)
        {
            if (arg is null)
                return null;

            if (arg.IsArguments)
                return MapArg(arg.AsArguments, fn);

            if (arg.IsNode)
                return fn(arg.AsNode);

            return arg;
        }

        /// <summary>
        /// Iterates through a list of arguments, invoking 'fn' at each visited node.
        /// </summary>
        public static void VisitArg(Argument[] args, Action<Node> fn, bool reverse = false)
        {
            if (args is null)
                return;

            if (reverse)
            {
                for (var i = args.Length - 1; i >= 0; i--)
                    VisitArg(args[i], fn);
            }
            else
            {
                for (var i = 0; i < args.Length; i++)
                    VisitArg(args[i], fn);
            }
        }

        public static void VisitArg(Argument arg, Action<Node> fn, bool reverse = false)
        {
            if (arg is null)
                return;

            if (arg.IsNode)
                fn(arg.AsNode);

            if (arg.IsArguments)
                VisitArg(arg.AsArguments, fn, reverse);
        }
    }
}
