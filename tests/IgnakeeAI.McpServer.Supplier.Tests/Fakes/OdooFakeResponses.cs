using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Genera respuestas JSON-RPC realistas que simulan la API de Odoo v14+.
    /// Los datos están basados en un catálogo de materiales de construcción
    /// con la estructura real que devuelve product.product/search_read.
    /// </summary>
    public static class OdooFakeResponses
    {
        // ── Autenticación ────────────────────────────────────────────────────────

        /// <summary>Respuesta de autenticación exitosa (uid = 2).</summary>
        public static string AuthenticateSuccess() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = 2
        });

        /// <summary>Respuesta de autenticación fallida (credenciales inválidas).</summary>
        public static string AuthenticateInvalidCredentials() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = false
        });

        /// <summary>Respuesta de autenticación con error de servidor.</summary>
        public static string AuthenticateServerError() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            error = new
            {
                code = 200,
                message = "Odoo Server Error",
                data = new { message = "Database 'mi_empresa' does not exist." }
            }
        });

        // ── Productos (search_read) ──────────────────────────────────────────────

        /// <summary>
        /// Catálogo realista de materiales de construcción.
        /// Simula la respuesta de product.product/search_read con campos Many2one
        /// (categ_id, uom_id) devueltos como arrays [id, "nombre"] — formato real de Odoo.
        /// </summary>
        public static string SearchReadProducts() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = new object[]
            {
                new
                {
                    id = 101,
                    default_code = "CEM-001",
                    name = "Cemento Portland CEM II/B-L 32.5R - Saco 25 kg",
                    categ_id = new object[] { 7, "Cementos" },
                    list_price = 4.85,
                    uom_id = new object[] { 1, "kg" },
                    qty_available = 12000.0,
                    description_sale = "cemento,portland,CEM II,32.5R,saco,construcción",
                    sale_ok = true
                },
                new
                {
                    id = 102,
                    default_code = "ACE-010",
                    name = "Acero corrugado B500SD Ø12 mm - Barra 12 m",
                    categ_id = new object[] { 3, "Aceros" },
                    list_price = 8.40,
                    uom_id = new object[] { 5, "m" },
                    qty_available = 3500.0,
                    description_sale = "acero,corrugado,B500SD,armadura,estructura",
                    sale_ok = true
                },
                new
                {
                    id = 103,
                    default_code = "LAD-005",
                    name = "Ladrillo perforado métrico 24x11.5x10 cm",
                    categ_id = new object[] { 12, "Cerámicos" },
                    list_price = 0.28,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 45000.0,
                    description_sale = "ladrillo,perforado,métrico,tabiquería,cerámica",
                    sale_ok = true
                },
                new
                {
                    id = 104,
                    default_code = "PIN-020",
                    name = "Pintura plástica blanca mate interior 15 L",
                    categ_id = new object[] { 9, "Pinturas" },
                    list_price = 42.90,
                    uom_id = new object[] { 8, "l" },
                    qty_available = 820.0,
                    description_sale = "pintura,plástica,blanca,mate,interior,pared,techo",
                    sale_ok = true
                },
                new
                {
                    id = 105,
                    default_code = "ARI-003",
                    name = "Arena lavada 0/4 mm - Big Bag 1500 kg",
                    categ_id = new object[] { 15, "Áridos" },
                    list_price = 38.50,
                    uom_id = new object[] { 1, "kg" },
                    qty_available = 95000.0,
                    description_sale = "arena,lavada,árido,fino,mortero,hormigón",
                    sale_ok = true
                },
                new
                {
                    id = 106,
                    default_code = "TUB-015",
                    name = "Tubo PVC evacuación Ø110 mm - 3 m",
                    categ_id = new object[] { 20, "Fontanería" },
                    list_price = 7.65,
                    uom_id = new object[] { 5, "m" },
                    qty_available = 2200.0,
                    description_sale = "tubo,PVC,evacuación,saneamiento,desagüe",
                    sale_ok = true
                },
                new
                {
                    id = 107,
                    default_code = "IMP-008",
                    name = "Lámina impermeabilizante bituminosa LBM-40-FV",
                    categ_id = new object[] { 18, "Impermeabilización" },
                    list_price = 5.20,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 6800.0,
                    description_sale = "lámina,impermeabilizante,bituminosa,cubierta,terraza",
                    sale_ok = true
                },
                new
                {
                    id = 108,
                    default_code = "AIS-012",
                    name = "Panel lana de roca 40 mm - 1200x600 mm",
                    categ_id = new object[] { 22, "Aislamientos" },
                    list_price = 6.35,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 4100.0,
                    description_sale = "aislamiento,lana,roca,térmico,acústico,panel",
                    sale_ok = true
                },
                new
                {
                    id = 109,
                    default_code = "HER-002",
                    name = "Herraje para puerta corredera - Juego 2 uds",
                    categ_id = new object[] { 25, "Herrajes" },
                    list_price = 15.75,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 1500.0,
                    description_sale = "herraje,puerta,corredera,juego,accesorio",
                    sale_ok = true
                },
                new
                {
                    id = 110,
                    default_code = "VID-006",
                    name = "Vidrio templado 6 mm - Placa 2000x1000 mm",
                    categ_id = new object[] { 30, "Vidrios" },
                    list_price = 55.00,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 1200.0,
                    description_sale = "vidrio,templado,plano,transparente,seguridad",
                    sale_ok = true
                },
                new
                {
                    id = 111,
                    default_code = "CER-004",
                    name = "Cerámica de gres porcelánico - Caja 1.44 m2",
                    categ_id = new object[] { 35, "Suelos" },
                    list_price = 18.90,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 3000.0,
                    description_sale = "cerámica,gres,porcelánico,suelo,interior,exterior",
                    sale_ok = true
                },
                new
                {
                    id = 112,
                    default_code = "MAM-007",
                    name = "Marmol blanco Macael - Placa 3000x2000 mm",
                    categ_id = new object[] { 40, "Mármoles" },
                    list_price = 120.00,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 500.0,
                    description_sale = "mármol,blanco,Macael,plano,decoración,alta gama",
                    sale_ok = true
                },
                new
                {
                    id = 113,
                    default_code = "GRA-009",
                    name = "Grava lavada 4/12 mm - Big Bag 1500 kg",
                    categ_id = new object[] { 15, "Áridos" },
                    list_price = 42.00,
                    uom_id = new object[] { 1, "kg" },
                    qty_available = 75000.0,
                    description_sale = "grava,lavada,árido,medio,hormigón,drenaje",
                    sale_ok = true
                },
                new
                {
                    id = 114,
                    default_code = "MAL-003",
                    name = "Malla electrosoldada 150x150 mm - Rollo 25 m2",
                    categ_id = new object[] { 3, "Aceros" },
                    list_price = 28.50,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 2000.0,
                    description_sale = "malla,electrosoldada,armadura,hormigón,refuerzo",
                    sale_ok = true
                },
                new
                {
                    id = 115,
                    default_code = "TAR-011",
                    name = "Tarima flotante laminada - Caja 2.22 m2",
                    categ_id = new object[] { 35, "Suelos" },
                    list_price = 19.95,
                    uom_id = new object[] { 3, "m2" },
                    qty_available = 1800.0,
                    description_sale = "tarima,flotante,laminada,suelo,interior,decoración",
                    sale_ok = true
                },
                new
                {
                    id = 116,
                    default_code = "ALU-004",
                    name = "Perfil de aluminio en L 40x40 mm - 3 m",
                    categ_id = new object[] { 45, "Aluminio" },
                    list_price = 12.30,
                    uom_id = new object[] { 5, "m" },
                    qty_available = 2700.0,
                    description_sale = "perfil,aluminio,L,estructura,acabado",
                    sale_ok = true
                },
                new
                {
                    id = 117,
                    default_code = "MOT-002",
                    name = "Motorreductor para persiana - 230 V",
                    categ_id = new object[] { 50, "Automatismos" },
                    list_price = 85.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 600.0,
                    description_sale = "motorreductor,persiana,automatismo,accesorio",
                    sale_ok = true
                },
                new
                {
                    id = 118,
                    default_code = "BOM-001",
                    name = "Bomba de agua sumergible - 1.5 kW",
                    categ_id = new object[] { 20, "Fontanería" },
                    list_price = 150.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 300.0,
                    description_sale = "bomba,agua,sumergible,fontanería,riego",
                    sale_ok = true
                },
                new
                {
                    id = 119,
                    default_code = "GEN-001",
                    name = "Producto genérico sin descripción de venta",
                    categ_id = new object[] { 1, "General" },
                    list_price = 10.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 100.0,
                    description_sale = "",
                    sale_ok = true
                },
                new
                {
                    id = 120,
                    default_code = false as object, // Producto sin código → debe omitirse
                    name = "Producto sin SKU",
                    categ_id = false as object,      // Categoría vacía → debe usar "general"
                    list_price = 0.0,
                    uom_id = false as object,         // Unidad vacía → debe usar "ud"
                    qty_available = 0.0,
                    description_sale = "",
                    sale_ok = true
                },
                new
                {
                    id = 121,
                    default_code = "DIS-001",
                    name = "Producto no vendible (sale_ok = false)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 5.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 50.0,
                    description_sale = "producto,no vendible,descontinuado",
                    sale_ok = false
                },
                new
                {
                    id = 122,
                    default_code = "ERR-001",
                    name = "Producto con datos erróneos (precio negativo)",
                    categ_id = new object[] { 1, "General" },
                    list_price = -10.00, // Precio negativo → debe corregirse a 0
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 20.0,
                    description_sale = "producto,datos erróneos,precio negativo",
                    sale_ok = true
                },
                new
                {
                    id = 123,
                    default_code = "ERR-002",
                    name = "Producto con datos erróneos (stock negativo)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 15.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = -5.0, // Stock negativo → debe corregirse a 0
                    description_sale = "producto,datos erróneos,stock negativo",
                    sale_ok = true
                },
                new
                {
                    id = 124,
                    default_code = "ERR-003",
                    name = "Producto con datos erróneos (unidad vacía)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 20.00,
                    uom_id = false as object, // Unidad vacía → debe usar "ud"
                    qty_available = 10.0,
                    description_sale = "producto,datos erróneos,unidad vacía",
                    sale_ok = true
                },
                new
                {
                    id = 125,
                    default_code = "ERR-004",
                    name = "Producto con datos erróneos (categoría vacía)",
                    categ_id = false as object, // Categoría vacía → debe usar "general"
                    list_price = 25.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 5.0,
                    description_sale = "producto,datos erróneos,categoría vacía",
                    sale_ok = true
                },
                new
                {
                    id = 126,
                    default_code = "ERR-005",
                    name = "Producto con datos erróneos (campo de texto vacío)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 30.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 15.0,
                    description_sale = false as object, // Campo de texto vacío → debe corregirse a ""
                    sale_ok = true
                },
                 new
                {
                    id = 127,
                    default_code = "ERR-006",
                    name = "Producto con datos erróneos (campo de texto null)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 35.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 25.0,
                    description_sale = false as object, // Campo de texto null → debe corregirse a ""
                    sale_ok = true
                 },
                new
                {
                    id = 128,
                    default_code = "ERR-007",
                    name = "Producto con datos erróneos (campo booleano null)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 40.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 30.0,
                    description_sale = "producto,datos erróneos,campo booleano null",
                    sale_ok = false as object // Campo booleano null → debe corregirse a false
                },
                new
                {
                    id = 129,
                    default_code = "ERR-008",
                    name = "Producto con datos erróneos (campo booleano vacío)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 45.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 35.0,
                    description_sale = "producto,datos erróneos,campo booleano vacío",
                    sale_ok = false as object // Campo booleano vacío → debe corregirse a false
                },
                new
                {
                    id = 130,
                    default_code = "ERR-009",
                    name = "Producto con datos erróneos (campo booleano true)",
                    categ_id = new object[] { 1, "General" },
                    list_price = 50.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 40.0,
                    description_sale = "producto,datos erróneos,campo booleano true",
                    sale_ok = true // Campo booleano true → debe mantenerse como true
                }
            }
        });

        /// <summary>Respuesta vacía (catálogo sin productos vendibles).</summary>
        public static string SearchReadEmpty() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = Array.Empty<object>()
        });

        /// <summary>
        /// Productos con campos vacíos/false — simula el comportamiento real de Odoo
        /// cuando un campo de texto no tiene valor (devuelve false en lugar de null).
        /// </summary>
        public static string SearchReadWithNullableFields() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = new object[]
            {
                new
                {
                    id = 201,
                    default_code = "GEN-001",
                    name = "Producto genérico sin descripción de venta",
                    categ_id = new object[] { 1, "General" },
                    list_price = 10.00,
                    uom_id = new object[] { 1, "ud" },
                    qty_available = 100.0,
                    description_sale = false as object, // Odoo devuelve false para campos vacíos
                    sale_ok = true
                },
                new
                {
                    id = 202,
                    default_code = false as object, // Producto sin código → debe omitirse
                    name = "Producto sin SKU",
                    categ_id = false as object,      // Categoría vacía → debe usar "general"
                    list_price = 0.0,
                    uom_id = false as object,         // Unidad vacía → debe usar "ud"
                    qty_available = 0.0,
                    description_sale = false as object,
                    sale_ok = true
                }
            }
        });

        /// <summary>Error de lectura de productos (permisos insuficientes).</summary>
        public static string SearchReadAccessError() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            error = new
            {
                code = 200,
                message = "Odoo Server Error",
                data = new
                {
                    message = "Access Denied: You do not have access to 'product.product' (product.product)."
                }
            }
        });

        /// <summary>
        /// Producto con campos numéricos en false (comportamiento real de Odoo en algunos casos).
        /// Debe mapearse sin excepción:
        /// - list_price = false  -> 0
        /// - qty_available = false -> null
        /// </summary>
        public static string SearchReadWithFalseNumericFields() => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (int?)null,
            result = new object[]
            {
        new
        {
            id = 301,
            default_code = "NUM-001",
            name = "Producto con numéricos false",
            categ_id = new object[] { 1, "General" },
            list_price = false as object,
            uom_id = new object[] { 1, "ud" },
            qty_available = false as object,
            description_sale = "producto,numéricos,false",
            sale_ok = true
        }
            }
        });
    }
}
