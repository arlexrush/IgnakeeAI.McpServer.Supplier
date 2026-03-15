using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>
    /// Factory compartida para todas las suites de integración.
    /// Sustituye la BD real por SQLite en memoria (sin migraciones),
    /// desactiva la migración automática al arrancar y siembra un catálogo
    /// de test representativo con todos los tipos de producto relevantes.
    /// </summary>
    public class SupplierApiFactory : WebApplicationFactory<Api.Program>
    {
        // ── Conexión SQLite compartida ────────────────────────────────────────────
        // Se mantiene abierta durante toda la vida de la factory para que la BD
        // en memoria persista entre el DbContext del seed y el de la aplicación.
        private readonly SqliteConnection _keepAliveConnection;

        public SupplierApiFactory()
        {
            _keepAliveConnection = new SqliteConnection("Data Source=:memory:");
            _keepAliveConnection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            //builder.ConfigureServices(services =>
            //{
            //    // ── Reemplazar BD real por SQLite en memoria ──────────────────────
            //    // Se elimina cualquier registro previo de DbContextOptions para
            //    // garantizar que toda la aplicación use la misma conexión compartida.
            //    var descriptor = services.SingleOrDefault(
            //        d => d.ServiceType == typeof(DbContextOptions<SupplierCatalogDbContext>));

            //    if (descriptor is not null)
            //        services.Remove(descriptor);

            //    services.AddDbContext<SupplierCatalogDbContext>(options =>
            //        options.UseSqlite(_keepAliveConnection));

            //    // ── Desactivar migraciones automáticas al arrancar ─────────────────
            //    // services.Configure<Microsoft.Extensions.Configuration.IConfiguration>(_ => { });
            //});

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["DatabaseProvider"] = "sqlite",
                    // ["ConnectionStrings:Catalog"] = $"Data Source=:memory:;Cache=Shared;Mode=Memory",
                    // ["ConnectionStrings:Catalog"] = "Data Source=:memory:",
                    ["Erp:Provider"] = "",
                    ["SUPPLIER_VENDOR_NAME"] = "Proveedor Test",
                    ["SUPPLIER_CONTACT_EMAIL"] = "test@proveedor.local",
                    ["SUPPLIER_CONTACT_PHONE"] = "+34 900 000 000",
                    ["SUPPLIER_CONTACT_ADDRESS"] = "Calle Test 1, Madrid",
                    ["SUPPLIER_BUSINESS_HOURS"] = "L-V 08:00-18:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                // ── Eliminar TODOS los registros del DbContext ────────────────────
                // Se deben eliminar tanto DbContextOptions<T> (registrado por
                // AddDbContext) como DbContext (el propio tipo scoped) para evitar
                // que quede algún descriptor apuntando a la BD de disco.
                services.RemoveAll<DbContextOptions<SupplierCatalogDbContext>>();
                services.RemoveAll<SupplierCatalogDbContext>();

                // ── Registrar el DbContext con la conexión compartida abierta ─────
                // Al reutilizar _keepAliveConnection, SQLite en memoria mantiene
                // la misma BD entre el scope del seed y los scopes de las requests.
                services.AddDbContext<SupplierCatalogDbContext>(options =>
                    options.UseSqlite(_keepAliveConnection));
            });

        }

        /// <summary>
        /// Crea y siembra la BD SQLite en memoria para un test concreto.
        /// Devuelve el scope para que el test pueda acceder al DbContext si necesita
        /// verificar el estado persistido directamente.
        /// </summary>
        public async Task<IServiceScope> SeedDatabaseAsync(
            IEnumerable<CatalogProduct>? products = null)
        {
            var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupplierCatalogDbContext>();

            await db.Database.EnsureCreatedAsync();

            // ── Limpiar datos previos antes de sembrar ────────────────────────────
            // Necesario porque IClassFixture comparte la instancia de factory entre
            // todos los tests de la clase, y InitializeAsync se llama por cada test.
            db.Products.RemoveRange(db.Products);
            await db.SaveChangesAsync();

            var catalog = products ?? BuildDefaultCatalog();
            db.Products.AddRange(catalog);
            await db.SaveChangesAsync();

            return scope;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                // Cerrar la conexión SQLite compartida al liberar la factory
                _keepAliveConnection.Dispose();
            }
        }

        // ── Catálogo por defecto para tests ──────────────────────────────────────

        public static IEnumerable<CatalogProduct> BuildDefaultCatalog()
        {
            var now = DateTime.UtcNow;
            return
            [
                new CatalogProduct
                {
                    ItemCode = "CEM-STD",
                    Description = "Cemento estándar para albañilería",
                    Category = "cementos",
                    Keywords = "cemento,estandar,albañileria",
                    Unit = "kg",
                    UnitPrice = 5.00m,
                    Currency = "EUR",
                    QualityRating = 3,
                    IsOnSale = false,
                    PackSize = 20,
                    PackPrice = 92m,
                    AvailableStock = 5000,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-PREMIUM",
                    Description = "Cemento premium estructural 42.5R",
                    Category = "cementos",
                    Keywords = "cemento,premium,estructural,obra",
                    Unit = "kg",
                    UnitPrice = 8.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = false,
                    PackSize = 25,
                    PackPrice = 170m,
                    AvailableStock = 3000,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-OFFER",
                    Description = "Cemento oferta especial de temporada",
                    Category = "cementos",
                    Keywords = "cemento,oferta,promo",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ACE-001",
                    Description = "Acero corrugado B500SD Ø12 mm",
                    Category = "aceros",
                    Keywords = "acero,corrugado,b500sd,armadura",
                    Unit = "m",
                    UnitPrice = 8.40m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = false,
                    PackSize = 12,
                    PackPrice = 95m,
                    AvailableStock = 2000,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ACE-002",
                    Description = "Acero corrugado B500SD Ø16 mm",
                    Category = "aceros",
                    Keywords = "acero,corrugado,b500sd,armadura",
                    Unit = "m",
                    UnitPrice = 11.20m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = false,
                    PackSize = 12,
                    PackPrice = 125m,
                    AvailableStock = 1500,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ACE-003",
                    Description = "Acero corrugado B500SD Ø20 mm",
                    Category = "aceros",
                    Keywords = "acero,corrugado,b500sd,armadura",
                    Unit = "m",
                    UnitPrice = 14.00m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = false,
                    PackSize = 12,
                    PackPrice = 150m,
                    AvailableStock = 1000,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "LAD-ALU",
                    Description = "Lámina de aluminio 2 mm para cubiertas",
                    Category = "láminas",
                    Keywords = "lamina,aluminio,cubierta,techo",
                    Unit = "m2",
                    UnitPrice = 15.00m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = false,
                    PackSize = 50,
                    PackPrice = 700m,
                    AvailableStock = 800,
                    LeadTimeDays = 5,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "LAD-STEEL",
                    Description = "Lámina de acero galvanizado 1.5 mm",
                    Category = "láminas",
                    Keywords = "lamina,acero,galvanizado,cubierta",
                    Unit = "m2",
                    UnitPrice = 12.00m,
                    Currency = "EUR",
                    QualityRating = 3,
                    IsOnSale = false,
                    PackSize = 50,
                    PackPrice = 550m,
                    AvailableStock = 1000,
                    LeadTimeDays = 5,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "LAD-COPPER",
                    Description = "Lámina de cobre 1 mm para tejados",
                    Category = "láminas",
                    Keywords = "lamina,cobre,tejado,techo",
                    Unit = "m2",
                    UnitPrice = 25.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = false,
                    PackSize = 50,
                    PackPrice = 1200m,
                    AvailableStock = 500,
                    LeadTimeDays = 7,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "LAD-PLASTIC",
                    Description = "Lámina de plástico corrugado 3 mm",
                    Category = "láminas",
                    Keywords = "lamina,plastico,corrugado,cubierta",
                    Unit = "m2",
                    UnitPrice = 10.00m,
                    Currency = "EUR",
                    QualityRating = 2,
                    IsOnSale = true,
                    SalePrice = 7.50m,
                    PackSize = 50,
                    PackPrice = 400m,
                    AvailableStock = 1500,
                    LeadTimeDays = 5,
                    UpdatedAt = now,
                    IsActive = true
                }, 
                new CatalogProduct
                {
                    ItemCode = "CEM-EXPIRED",
                    Description = "Cemento con oferta expirada",
                    Category = "cementos",
                    Keywords = "cemento,oferta,expirada",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    ValidUntil = now.AddDays(-1), // Oferta expirada
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-FUTURE",
                    Description = "Cemento con oferta futura",
                    Category = "cementos",
                    Keywords = "cemento,oferta,futura",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    ValidUntil = now.AddDays(7), // Oferta válida durante una semana
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-NOSTOCK",
                    Description = "Cemento sin stock disponible",
                    Category = "cementos",
                    Keywords = "cemento,nostock,agotado",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 0, // Sin stock
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                }, 
                new CatalogProduct
                {
                    ItemCode = "CEM-LOWSTOCK",
                    Description = "Cemento con stock muy limitado",
                    Category = "cementos",
                    Keywords = "cemento,lowstock,limitado",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 5, // Stock muy limitado
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-SUBSTITUTE",
                    Description = "Cemento sustituto recomendado",
                    Category = "cementos",
                    Keywords = "cemento,sustituto,recomendado",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true,
                    IsSubstitute = true // Producto marcado como sustituto
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-LOWRATING",
                    Description = "Cemento con baja calificación",
                    Category = "cementos",
                    Keywords = "cemento,bajarating,calificación",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 2, // Baja calificación
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-HIGHRATING",
                    Description = "Cemento con alta calificación",
                    Category = "cementos",
                    Keywords = "cemento,highrating,calificación",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 5, // Alta calificación
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                }, 
                new CatalogProduct
                {
                    ItemCode = "CEM-INACTIVE",
                    Description = "Cemento inactivo no disponible para venta",
                    Category = "cementos",
                    Keywords = "cemento,inactivo,descontinuado",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = false // Producto inactivo
                },
                 new CatalogProduct
                {
                    ItemCode = "CEM-NEGATIVEPRICE",
                    Description = "Cemento con precio negativo",
                    Category = "cementos",
                    Keywords = "cemento,negativo,precio",
                    Unit = "kg",
                    UnitPrice = -6.00m, // Precio negativo
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-ZEROPRICE",
                    Description = "Cemento con precio cero",
                    Category = "cementos",
                    Keywords = "cemento,cero,precio",
                    Unit = "kg",
                    UnitPrice = 0.00m, // Precio cero
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-NEGATIVESTOCK",
                    Description = "Cemento con stock negativo",
                    Category = "cementos",
                    Keywords = "cemento,negativo,stock",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = -1200, // Stock negativo
                    LeadTimeDays = 1,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-LEADTIMEZERO",
                    Description = "Cemento con tiempo de entrega cero",
                    Category = "cementos",
                    Keywords = "cemento,leadtimezero,entrega",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125m,
                    AvailableStock = 1200,
                    LeadTimeDays = 0, // Tiempo de entrega cero
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ARENA-001",
                    Description = "Arena fina para construcción",
                    Category = "áridos",
                    Keywords = "arena,fina,construcción",
                    Unit = "kg",
                    UnitPrice = 3.00m,
                    SalePrice = 2.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 50,
                    PackPrice = 125m,
                    AvailableStock = 1000,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "GRAVA-001",
                    Description = "Grava de río para drenaje",
                    Category = "áridos",
                    Keywords = "grava,río,drenaje",
                    Unit = "kg",
                    UnitPrice = 4.00m,
                    SalePrice = 3.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 50,
                    PackPrice = 150m,
                    AvailableStock = 800,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "GRAVA-002",
                    Description = "Grava gruesa para construcción",
                    Category = "áridos",
                    Keywords = "grava,gruesa,construcción",
                    Unit = "kg",
                    UnitPrice = 5.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 50,
                    PackPrice = 175m,
                    AvailableStock = 600,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "PLADURA-001",
                    Description = "Pladur para construcción",
                    Category = "pladur",
                    Keywords = "pladur,construcción",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 5.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 165m,
                    AvailableStock = 500,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "PLADURA-002",
                    Description = "Pladur resistente a la humedad",
                    Category = "pladur",
                    Keywords = "pladur,resistente,humedad",
                    Unit = "kg",
                    UnitPrice = 8.00m,
                    SalePrice = 7.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 200m,
                    AvailableStock = 300,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "PLADURA-003",
                    Description = "Pladur resistente al fuego",
                    Category = "pladur",
                    Keywords = "pladur,resistente,fuego",
                    Unit = "kg",
                    UnitPrice = 10.00m,
                    SalePrice = 9.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 250m,
                    AvailableStock = 200,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                 new CatalogProduct
                {
                    ItemCode = "PLADURA-004",
                    Description = "Pladur resistente a impactos",
                    Category = "pladur",
                    Keywords = "pladur,resistente,impactos",
                    Unit = "kg",
                    UnitPrice = 12.00m,
                    SalePrice = 11.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 300m,
                    AvailableStock = 100,
                    LeadTimeDays = 2,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-001",
                    Description = "Azulejo cerámico para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,cerámico,alicatado",
                    Unit = "m2",
                    UnitPrice = 20.00m,
                    SalePrice = 18.00m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 190m,
                    AvailableStock = 500,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-002",
                    Description = "Azulejo porcelánico para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,porcelánico,alicatado",
                    Unit = "m2",
                    UnitPrice = 30.00m,
                    SalePrice = 27.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 290m,
                    AvailableStock = 300,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-003",
                    Description = "Azulejo de piedra natural para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,piedra natural,alicatado",
                    Unit = "m2",
                    UnitPrice = 40.00m,
                    SalePrice = 36.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 390m,
                    AvailableStock = 200,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-004",
                    Description = "Azulejo de vidrio para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,vidrio,alicatado",
                    Unit = "m2",
                    UnitPrice = 50.00m,
                    SalePrice = 45.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 490m,
                    AvailableStock = 100,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-005",
                    Description = "Azulejo de cemento para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,cemento,alicatado",
                    Unit = "m2",
                    UnitPrice = 25.00m,
                    SalePrice = 22.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 240m,
                    AvailableStock = 400,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "ALICATADO-006",
                    Description = "Azulejo de cerámica reciclada para alicatado",
                    Category = "alicatado",
                    Keywords = "azulejo,cerámica reciclada,alicatado",
                    Unit = "m2",
                    UnitPrice = 35.00m,
                    SalePrice = 31.50m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = true,
                    PackSize = 10,
                    PackPrice = 340m,
                    AvailableStock = 150,
                    LeadTimeDays = 3,
                    UpdatedAt = now,
                    IsActive = true
                }
            ];
        }
    }
}
