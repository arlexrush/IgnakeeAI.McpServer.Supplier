using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IgnakeeAI.McpServer.Supplier.Api
{
    public static class AdminCatalogEndPoint
    {
        public static RouteGroupBuilder MapAdminCatalogEndpoints(this WebApplication app)
        {
            var admin = app.MapGroup("/admin")
                .RequireAuthorization("SupplierAdminPolicy");

            /// <summary>
            /// POST /admin/sync/erp, sincroniza el catálogo con el ERP configurado. 
            /// Requiere que el conector ERP esté disponible y correctamente configurado. 
            /// Devuelve un resumen de la sincronización, incluyendo el número de productos sincronizados y la marca de tiempo. 
            /// Si no hay un conector ERP configurado o si el conector no está disponible, 
            /// devuelve un error detallado para ayudar a diagnosticar el problema. 
            /// Esta operación es idempotente y puede ser ejecutada periódicamente para mantener el catálogo actualizado con los datos del ERP.
            /// </summary>
            admin.MapPost("/sync/erp", async (IServiceProvider sp, CatalogSyncAuditWriter auditWriter, CancellationToken cancellationToken) =>
            {
                var startedAt = DateTimeOffset.UtcNow;
                var connector = sp.GetService<IErpConnector>();
                if (connector is null)
                    return Results.BadRequest(new { error = "No hay conector ERP configurado. Revisa Erp:Provider en appsettings.json." });

                if (!await connector.IsAvailableAsync())
                    return Results.BadRequest(new { error = $"El conector {connector.ErpName} no está disponible. Revisa la configuración." });

                try
                {
                    // Bloque de sincronización validado sin cambios funcionales.
                    var count = await connector.SyncProductsAsync(cancellationToken);
                    await auditWriter.WriteAsync(
                        CatalogSyncAuditSources.Erp,
                        connector.ErpName,
                        count,
                        count,
                        0,
                        0,
                        startedAt,
                        true,
                        cancellationToken: cancellationToken);

                    return Results.Ok(new
                    {
                        erp = connector.ErpName,
                        productsSynced = count,
                        syncedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    await auditWriter.WriteAsync(
                        CatalogSyncAuditSources.Erp,
                        connector.ErpName,
                        0,
                        0,
                        0,
                        0,
                        startedAt,
                        false,
                        ex.GetType().Name,
                        cancellationToken);
                    throw;
                }
            }).WithTags("Admin");

            /// <summary>
            /// POST /admin/sync/excel, importa un catálogo desde un archivo Excel (.xlsx). 
            /// Requiere que el conector Excel esté disponible y correctamente configurado. 
            /// Devuelve un resumen de la importación, incluyendo el número de productos importados y el nombre del archivo. 
            /// Si no se envía un archivo o si el archivo no es válido, devuelve un error detallado para ayudar a diagnosticar el problema. 
            /// Esta operación es idempotente y puede ser ejecutada periódicamente para mantener el catálogo actualizado con los datos del Excel.
            /// </summary>
            admin.MapPost("/sync/excel", async (IFormFile? file, ExcelCatalogConnector connector, CatalogSyncAuditWriter auditWriter, CancellationToken cancellationToken) =>
            {
                if (file is null)
                    return Results.BadRequest(new { error = "Envía un archivo .xlsx en el form-data con key 'file'." });

                var tempPath = Path.Combine(Path.GetTempPath(), $"catalog_{Guid.NewGuid()}.xlsx");

                await using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    var startedAt = DateTimeOffset.UtcNow;
                    var count = await connector.ImportAsync(tempPath, cancellationToken);
                    await auditWriter.WriteAsync(
                        CatalogSyncAuditSources.Excel,
                        null,
                        count,
                        count,
                        0,
                        0,
                        startedAt,
                        true,
                        cancellationToken: cancellationToken);
                    return Results.Ok(new { source = "excel", fileName = file.FileName, productsImported = count });
                }
                finally
                {
                    File.Delete(tempPath);
                }
            }).DisableAntiforgery()
            .WithTags("Admin");

            /// <summary>
            /// POST /admin/sync/csv, importa un catálogo desde un archivo CSV (.csv). 
            /// Requiere que el conector CSV esté disponible y correctamente configurado. 
            /// Devuelve un resumen de la importación, incluyendo el número de productos importados y el nombre del archivo. 
            /// Si no se envía un archivo o si el archivo no es válido, devuelve un error detallado para ayudar a diagnosticar el problema. 
            /// Esta operación es idempotente y puede ser ejecutada periódicamente para mantener el catálogo actualizado con los datos del CSV.
            /// </summary>
            admin.MapPost("/sync/csv", async (IFormFile? file, CsvCatalogConnector connector, CatalogSyncAuditWriter auditWriter, CancellationToken cancellationToken) =>
            {
                if (file is null)
                    return Results.BadRequest(new { error = "Envía un archivo .csv en el form-data con key 'file'." });

                var tempPath = Path.Combine(Path.GetTempPath(), $"catalog_{Guid.NewGuid()}.csv");

                await using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    var startedAt = DateTimeOffset.UtcNow;
                    var count = await connector.ImportAsync(tempPath, cancellationToken);
                    await auditWriter.WriteAsync(
                        CatalogSyncAuditSources.Csv,
                        null,
                        count,
                        count,
                        0,
                        0,
                        startedAt,
                        true,
                        cancellationToken: cancellationToken);
                    return Results.Ok(new { source = "csv", fileName = file.FileName, productsImported = count });
                }
                finally
                {
                    File.Delete(tempPath);
                }
            }).DisableAntiforgery()
            .WithTags("Admin");

            /// <summary>
            /// GET /admin/catalog/stats, devuelve estadísticas del catálogo, incluyendo el número total de productos activos, 
            /// el número de productos en oferta y una distribución de productos por categoría. 
            /// Esta información es útil para monitorear la salud del catálogo y tomar decisiones informadas sobre promociones y gestión de inventario.
            /// </summary>
            admin.MapGet("/catalog/stats", async (SupplierCatalogDbContext db) =>
            {
                var total = await db.Products.CountAsync(p => p.IsActive);
                var onSale = await db.Products.CountAsync(p => p.IsActive && p.IsOnSale);
                var categories = await db.Products
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.Category)
                    .Select(g => new { category = g.Key, count = g.Count() })
                    .ToListAsync();

                return Results.Ok(new { totalProducts = total, productsOnSale = onSale, categories });
            }).WithTags("Admin");

            admin.MapGet("/sync/audits", async (SupplierCatalogDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.SyncAudits
                    .AsNoTracking()
                    .OrderByDescending(audit => audit.CompletedAt)
                    .Take(100)
                    .ToListAsync(cancellationToken)))
                .WithTags("Admin");

            return admin;
        }

    }
}
