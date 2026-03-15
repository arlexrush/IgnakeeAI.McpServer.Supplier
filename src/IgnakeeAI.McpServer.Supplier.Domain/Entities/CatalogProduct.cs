namespace IgnakeeAI.McpServer.Supplier.Domain.Entities
{
    /// <summary>
    /// Producto del catálogo del proveedor. Es la entidad central del dominio.
    /// Cada proveedor alimenta esta tabla con su inventario real, ya sea desde
    /// una base de datos, un Excel, un CSV o un conector ERP.
    ///
    /// CAMPOS CLAVE PARA LA VALORACIÓN INTELIGENTE:
    ///   - QualityRating: permite al agente comparar calidades entre proveedores.
    ///   - IsOnSale/SalePrice: el agente prioriza ofertas vigentes.
    ///   - PackSize/PackPrice: el agente calcula presentación óptima para la medición.
    ///   - Specification: permite al agente validar compatibilidad técnica.
    ///   - Presentation: permite al agente sugerir presentaciones más eficientes.
    /// </summary>
    public class CatalogProduct
    {
        public int Id { get; set; }

        /// <summary>Código interno del proveedor (SKU).</summary>
        public string ItemCode { get; set; } = default!;

        /// <summary>Descripción completa del producto.</summary>
        public string Description { get; set; } = default!;

        /// <summary>Categoría (ej. "cementos", "aceros", "pinturas", "áridos").</summary>
        public string Category { get; set; } = default!;

        /// <summary>Palabras clave para búsqueda (separadas por coma).</summary>
        public string Keywords { get; set; } = string.Empty;

        /// <summary>Unidad de medida (ej. "kg", "m2", "ud", "l").</summary>
        public string Unit { get; set; } = default!;

        /// <summary>Precio unitario actual.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Moneda ISO 4217.</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>Tamaño del pack de venta (ej. 25 para un saco de 25 kg).</summary>
        public decimal? PackSize { get; set; }

        /// <summary>Precio del pack completo.</summary>
        public decimal? PackPrice { get; set; }

        /// <summary>Especificación técnica (ej. "CEM II/B-L 32.5R según UNE-EN 197-1").</summary>
        public string? Specification { get; set; }

        /// <summary>Presentación (ej. "Saco 25 kg", "Granel", "Bidón 20 l").</summary>
        public string? Presentation { get; set; }

        /// <summary>Stock disponible en unidades.</summary>
        public int? AvailableStock { get; set; }

        /// <summary>Días estimados de entrega.</summary>
        public int? LeadTimeDays { get; set; }

        /// <summary>URL del producto en el catálogo online del proveedor.</summary>
        public string? ProductUrl { get; set; }

        /// <summary>Producto en oferta.</summary>
        public bool IsOnSale { get; set; }

        /// <summary>Precio con descuento (si está en oferta).</summary>
        public decimal? SalePrice { get; set; }

        /// <summary>Índice de calidad relativo (1-5).</summary>
        public int? QualityRating { get; set; }

        /// <summary>Puede sustituir a otros de la misma categoría.</summary>
        public bool IsSubstitute { get; set; }

        /// <summary>Fecha hasta la que el precio es válido.</summary>
        public DateTime? ValidUntil { get; set; }

        /// <summary>Última actualización del registro.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Activo en catálogo.</summary>
        public bool IsActive { get; set; } = true;

        // ── Métodos de dominio ──────────────────────────────────────

        /// <summary>Precio efectivo considerando ofertas.</summary>
        public decimal EffectivePrice =>
            IsOnSale && SalePrice.HasValue ? SalePrice.Value : UnitPrice;

        /// <summary>Porcentaje de ahorro si está en oferta.</summary>
        public decimal? DiscountPercent =>
            IsOnSale && SalePrice.HasValue && UnitPrice > 0
                ? Math.Round((1 - SalePrice.Value / UnitPrice) * 100, 1)
                : null;
    }
}
