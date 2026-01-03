using System.Collections.Generic;
using Combat.Runtime.GraphIR;

namespace Combat.Runtime.Build
{
    using GraphIRModel = GraphIR.GraphIR;

    /// <summary>
    /// Graph transformer interface.
    /// All Support transformation logic must implement this interface.
    /// </summary>
    public interface IGraphTransformer
    {
        /// <summary>
        /// Check if this transformer can be applied to the given graph.
        /// Called before Apply() to determine if transformation should occur.
        /// </summary>
        /// <param name="graph">The GraphIR to check</param>
        /// <param name="context">Build context with tags and supports</param>
        /// <returns>True if transformation should be applied</returns>
        bool CanApply(GraphIRModel graph, BuildContext context);

        /// <summary>
        /// Apply transformation to the graph.
        /// MUST return a new GraphIR (do not modify the source graph directly).
        /// </summary>
        /// <param name="sourceGraph">The source GraphIR (should not be modified)</param>
        /// <param name="context">Build context with configuration</param>
        /// <returns>A new transformed GraphIR, or null if transformation failed</returns>
        GraphIRModel Apply(GraphIRModel sourceGraph, BuildContext context);
    }

    /// <summary>
    /// Interface for transformers that accept parameters.
    /// Allows SupportDefinition to inject configuration values.
    /// </summary>
    public interface IParameterizedTransformer
    {
        /// <summary>
        /// Set parameters from SupportDefinition.
        /// Called after transformer is instantiated, before CanApply().
        /// </summary>
        /// <param name="parameters">Parameter list from SupportDefinition</param>
        void SetParameters(List<SupportParam> parameters);
    }
}
