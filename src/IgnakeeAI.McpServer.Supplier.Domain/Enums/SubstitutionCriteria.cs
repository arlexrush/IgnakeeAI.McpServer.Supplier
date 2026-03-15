namespace IgnakeeAI.McpServer.Supplier.Domain.Enums
{
    /// <summary>
    /// Criterios de búsqueda de alternativas/sustitutos.
    /// El agente Aristóteles envía este criterio para optimizar la partida.
    /// </summary>
    public enum SubstitutionCriteria
    {
        /// <summary>Productos más baratos en la misma categoría.</summary>
        Cheaper,

        /// <summary>Productos con mayor calidad (QualityRating ≥ 4).</summary>
        Better,

        /// <summary>Productos actualmente en oferta.</summary>
        OnSale,

        /// <summary>Presentaciones que minimizan el desperdicio para la cantidad requerida.</summary>
        OptimalPack,

        /// <summary>Todos los criterios combinados.</summary>
        Any
    }
}
