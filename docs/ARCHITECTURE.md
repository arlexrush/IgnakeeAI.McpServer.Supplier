# Arquitectura — IgnakeeAI MCP Supplier Server

## 1. Propósito del sistema

`IgnakeeAI.McpServer.Supplier` es un servidor MCP (Model Context Protocol) orientado a catálogo de proveedor para construcción, diseñado para:

- Exponer herramientas MCP para:
  - consulta de precio (`GetPrice`)
  - búsqueda de alternativas (`SearchAlternatives`)
  - disponibilidad (`CheckAvailability`)
  - datos de atención (`GetBusinessHours`)
- Centralizar información de catálogo desde múltiples fuentes:
  - base de datos local (principal)
  - importación por `CSV`
  - importación por `Excel`
  - sincronización desde ERP (`Odoo` o `SAP`)
- Ofrecer una integración simple por HTTP (`/mcp`) con salud del servicio (`/health`).

---

## 2. Estilo arquitectónico

Se aplica una arquitectura por capas con enfoque puertos/adaptadores (hexagonal ligera):

- **Dominio**: entidades y reglas derivadas (`CatalogProduct`, `EffectivePrice`, `DiscountPercent`).
- **Aplicación**: casos de uso y contratos (`CatalogSearchService`, `ICatalogRepository`, `ISupplierConfig`).
- **Infraestructura**: persistencia EF Core, conectores ERP/CSV/Excel, configuración.
- **Entrada MCP/API**: registro de tools MCP y exposición HTTP.

### 2.1 Vista de componentes

```mermaid
graph TD
    C["Cliente MCP (agente/LLM)"] --> A["API ASP.NET Core (.NET 8)"]
    A --> M["MCP Endpoint /mcp"]
    A --> H["Health Endpoint /health"]
    M --> T["MCP Tools"]
    T --> S["CatalogSearchService (Application)"]
    S --> R["ICatalogRepository (Puerto)"]
    R --> E["EfCatalogRepository (Adaptador)"]
    E --> D["SupplierCatalogDbContext"]
    D --> DB["SQLite / PostgreSQL / SQL Server / MySQL"]
    A --> AD["Admin Endpoints /admin/* (definidos)"]
    AD --> X["Conectores de sincronización"]
    X --> DB
```

Flujo de lectura contractual:

```text
Cliente MCP de Legio
        ?
HTTP MCP /mcp
        ?
McpTools
        ?
CatalogSearchService
        ?
ICatalogRepository
        ?
SupplierCatalogDbContext
```

El endpoint `/mcp` se autentica con una credencial de cliente MCP y scopes de
lectura. Los endpoints `/admin/*` están separados y requieren rol
`supplier-admin`. CORS se limita mediante `Cors:AllowedOrigins` y no sustituye
la autenticación.

---

## 3. Estructura de proyectos y responsabilidades

| Proyecto | Responsabilidad principal |
|---|---|
| `IgnakeeAI.McpServer.Supplier.Api` | Bootstrap del host, CORS, health checks, endpoint raíz y mapeo MCP |
| `IgnakeeAI.McpServer.Supplier.McpTools` | Herramientas MCP y serialización de respuestas |
| `IgnakeeAI.McpServer.Supplier.Application` | Orquestación funcional de búsqueda y contratos de puertos |
| `IgnakeeAI.McpServer.Supplier.Domain` | Modelo de dominio y enums de criterio |
| `IgnakeeAI.McpServer.Supplier.Infrastructure` | EF Core, repositorios, conectores, DI y configuración |
| `IgnakeeAI.McpServer.Supplier.Tests` | pruebas unitarias/integración en memoria (especial foco Odoo y tools) |

---

## 4. Flujo funcional principal

## 4.1 Consulta de precio (`GetPrice`)

1. El cliente invoca tool MCP.
2. `PricingTools` delega en `CatalogSearchService`.
3. El servicio intenta:
   - búsqueda exacta por `itemCode`
   - fallback por descripción (términos tokenizados)
4. `ICatalogRepository` devuelve `CatalogProduct`.
5. Se calcula precio efectivo (`EffectivePrice`) y se retorna `PriceResult` serializado a JSON.

```mermaid
sequenceDiagram
    participant U as Cliente MCP
    participant P as PricingTools
    participant S as CatalogSearchService
    participant R as ICatalogRepository
    participant D as EF/DB

    U->>P: GetPrice(itemDescription, itemCode)
    P->>S: GetPriceAsync(...)
    alt itemCode informado
        S->>R: FindByCodeAsync(itemCode)
    else sin itemCode o no encontrado
        S->>R: FindByDescriptionAsync(searchTerms)
    end
    R->>D: Query productos activos
    D-->>R: CatalogProduct o null
    R-->>S: resultado
    S-->>P: PriceResult
    P-->>U: JSON (found, unitPrice, contact...)
```

## 4.2 Búsqueda de alternativas (`SearchAlternatives`)

`CatalogSearchService` aplica estrategia por `SubstitutionCriteria`:

- `Cheaper`: compara contra precio de referencia.
- `Better`: prioriza `QualityRating >= 4`.
- `OnSale`: prioriza productos en oferta.
- `OptimalPack`: minimiza coste y desperdicio para una cantidad requerida.
- `Any`: combina estrategias y deduplica por `ItemCode`.

```mermaid
sequenceDiagram
    participant U as Cliente MCP
    participant A as AlternativeSearchTools
    participant S as CatalogSearchService
    participant R as ICatalogRepository

    U->>A: SearchAlternatives(...)
    A->>S: SearchAlternativesAsync(...)
    alt categoría no informada
        S->>R: InferCategoryAsync(terms)
    end
    S->>R: Consulta según criterio
    R-->>S: Lista CatalogProduct
    S-->>A: List<AlternativeMatch>
    A-->>U: JSON con alternatives + reason
```

## 4.3 Sincronización de catálogo (ERP/CSV/Excel)

Los conectores realizan upsert por `ItemCode` y persisten en catálogo local.

```mermaid
graph TD
    S1["Origen externo (ERP/CSV/Excel)"] --> S2["Conector correspondiente"]
    S2 --> S3["Mapeo a CatalogProduct"]
    S3 --> S4["Upsert por ItemCode"]
    S4 --> S5["SupplierCatalogDbContext.SaveChanges"]
    S5 --> S6["Catálogo local disponible para tools MCP"]
```

---

## 5. Modelo de dominio (núcleo)

Entidad central: `CatalogProduct`.

Campos relevantes para decisiones de compra:

- Identificación: `ItemCode`, `Description`, `Category`, `Keywords`.
- Precio/unidad: `UnitPrice`, `Currency`, `Unit`.
- Oferta: `IsOnSale`, `SalePrice`, `ValidUntil`.
- Calidad/sustitución: `QualityRating`, `Specification`, `Presentation`, `IsSubstitute`.
- Logística: `AvailableStock`, `LeadTimeDays`.
- Formatos: `PackSize`, `PackPrice`.
- Trazabilidad: `UpdatedAt`, `IsActive`, `ProductUrl`.

Reglas derivadas:

- `EffectivePrice`: usa `SalePrice` si está activa oferta.
- `DiscountPercent`: ahorro porcentual calculado.

---

## 6. Persistencia y base de datos

`SupplierCatalogDbContext` aplica configuraciones por ensamblado (`ApplyConfigurationsFromAssembly`).

Proveedor de BD configurable en runtime:

- `sqlite` (default)
- `postgresql`
- `sqlserver`
- `mysql`

Configuración en `DependencyInjection`:

- lectura de `DatabaseProvider`
- lectura de `ConnectionStrings:Catalog`
- creación de `DbContext` según provider seleccionado.

Índices definidos en `CatalogProductConfiguration`:

- `ix_product_category_active` sobre `(Category, IsActive)`
- `ix_product_itemcode` sobre `ItemCode`

Migraciones:

- se ejecutan automáticamente al iniciar el host (`db.Database.MigrateAsync()`).

---

## 7. Integraciones externas

## 7.1 Odoo (`OdooConnector`)

- Protocolo: JSON-RPC en `/jsonrpc`.
- Flujo:
  1. autenticación (`common.authenticate`)
  2. lectura (`product.product/search_read`)
  3. mapeo y persistencia local
- Consideraciones:
  - maneja valores `false` de Odoo para campos vacíos
  - permite ampliar campos custom `x_*`.

## 7.2 SAP (`SapConnector`)

- Protocolo: OData / Service Layer.
- Flujo:
  1. `Login`
  2. lectura paginada de `Items`
  3. mapeo y upsert
  4. `Logout`

## 7.3 CSV/Excel

- `CsvCatalogConnector`: delimitador `;`, mapeo por columnas nominales.
- `ExcelCatalogConnector`: hoja `Catalogo` (o primera hoja), columnas fijas A..Q.
- Ambos conectores:
  - hacen upsert por `ItemCode`
  - marcan `UpdatedAt` y `IsActive`.

---

## 8. Contrato MCP expuesto

Tools registradas desde ensamblado `McpTools`:

- `GetPrice(...)`
- `SearchAlternatives(...)`
- `CheckAvailability(...)`
- `GetBusinessHours()`

La versión de contrato es `1.0.0`. Los nombres son PascalCase y las respuestas
JSON se serializan en `camelCase`.

Endpoint MCP:

- `POST/transport MCP`: `/mcp` (HTTP transport registrado con `.WithHttpTransport()`).

Endpoint health:

- `GET /health`.

Endpoint raíz informativo:

- `GET /` devuelve metadata del servidor y tools declaradas.

`GET /health` comprueba MCP, tools, base de datos, catálogo y migraciones.

La ubicación operativa se configura mediante `Supplier:Location`, incluyendo
latitud y longitud validadas. El servidor no calcula distancias ni selecciona
proveedores; esas decisiones pertenecen a Legio/SmartRouting.

La trazabilidad usa los headers `X-Legio-*` y registra únicamente identificadores,
versión contractual, duración, ruta y estado. Nunca se registran API keys,
contraseñas, tokens ni connection strings.

Las sincronizaciones ERP, CSV y Excel actualizan el catálogo local mediante
upsert por `ItemCode`; las tools MCP nunca consultan directamente los sistemas
externos.

---

## 9. Configuración operativa

## 9.1 `appsettings.json`

Claves relevantes:

- `DatabaseProvider`
- `ConnectionStrings:Catalog`
- `Erp:Provider`
- `Erp:Odoo:*`
- `Erp:Sap:*`

## 9.2 Variables de entorno de proveedor

Consumidas por `SupplierConfig`:

- `SUPPLIER_CONTACT_EMAIL`
- `SUPPLIER_CONTACT_PHONE`
- `SUPPLIER_CONTACT_ADDRESS`
- `SUPPLIER_VENDOR_NAME`
- `SUPPLIER_BUSINESS_HOURS`

---

## 10. Despliegue (contenedor)

Base runtime:

- `mcr.microsoft.com/dotnet/aspnet:8.0`

Puerto expuesto:

- `5100`

Persistencia recomendada por volumen:

- `/app/data` para base SQLite (`/app/data/catalog.db`)

CI/CD:

- `github/workflows/ci.yml`: restore, build, test
- `github/workflows/release.yml`: buildx + push a GHCR por tag `v*`

---

## 11. Calidad, pruebas y validación

Cobertura funcional observable:

- `PricingToolsTests`: precio por código/descripcion/oferta/no encontrado.
- `AlternativeSearchTests`: criterios de sustitución.
- `OdooConnectorTests`: happy path, errores auth, catálogo vacío, upsert, nullables, comunicación JSON-RPC.

Buenas prácticas de validación en pipeline:

1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release --no-build`
4. build de imagen Docker multi-arquitectura (`amd64`, `arm64`).

---

## 12. Seguridad y hardening recomendado

Para producción:

- Restringir CORS (`AllowAnyOrigin` solo para desarrollo).
- Proteger endpoints `/admin/*` con autenticación/autorización.
- Mover credenciales ERP a secret manager.
- Activar TLS en perímetro (Ingress/Reverse Proxy).
- Registrar auditoría de sincronizaciones.
- Aplicar rate limiting sobre `/mcp` y `/admin/*`.
- Añadir timeouts/retries con políticas de resiliencia en `HttpClient`.

---

## 13. Riesgos y observaciones técnicas actuales

Observaciones detectadas en estado actual del código:

1. `AdminCatalogEndPoint` está implementado, pero no se observa llamado a `MapAdminCatalogEndpoints()` en `Program.cs`; por tanto, esos endpoints no quedarían expuestos hasta mapearse explícitamente.
2. El `Dockerfile` referencia `COPY --from=publish` sin etapa `publish` declarada; la publicación se ejecuta en la etapa `build`. Conviene corregir antes del release.
3. `ISupplierConfig` define propiedades no-null, mientras `SupplierConfig` expone algunas como nullable; revisar para coherencia de nullability.

Estas observaciones no invalidan la arquitectura, pero sí afectan operatividad/release si no se ajustan.

---

## 14. Runbook mínimo de operación

## 14.1 Arranque local

1. Configurar `appsettings.json` (BD + ERP opcional).
2. Exportar variables `SUPPLIER_*`.
3. Iniciar API.
4. Verificar:
   - `GET /health` => healthy
   - `GET /` => metadata
   - transporte MCP en `/mcp`

## 14.2 Sincronización de catálogo

- ERP: `POST /admin/sync/erp`
- Excel: `POST /admin/sync/excel` (`form-data`, `file`)
- CSV: `POST /admin/sync/csv` (`form-data`, `file`)
- Métricas básicas: `GET /admin/catalog/stats`

---

## 15. Evolución recomendada (roadmap)

1. Exponer OpenAPI para endpoints admin.
2. Programar sincronizaciones periódicas (`IHostedService` o scheduler externo).
3. Añadir multi-tenant (catálogo por proveedor).
4. Incorporar cache para búsquedas frecuentes.
5. Telemetría estructurada (OpenTelemetry + trazas de tool).
6. Versionado explícito de contrato MCP y compatibilidad hacia atrás.

---

## 16. Decisiones arquitectónicas clave

- Catálogo local como fuente de lectura de baja latencia para tools MCP.
- Integraciones ERP desacopladas mediante `IErpConnector`.
- Lógica de negocio centralizada en `CatalogSearchService`.
- Persistencia abstracta por `ICatalogRepository`.
- Extensibilidad por nuevos conectores sin modificar dominio.

---

## 17. Resumen ejecutivo

La solución está preparada para operar como servidor MCP de proveedor en .NET 8, con arquitectura limpia, múltiples fuentes de catálogo y despliegue contenedorizado.  
Para un despliegue productivo sólido, deben cerrarse los tres puntos del apartado de observaciones técnicas y aplicar hardening de seguridad/operación.
