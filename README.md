# IgnakeeAI MCP Supplier Server

> Servidor MCP (Model Context Protocol) para catálogo de proveedor, construido con .NET 8.  
> Expone herramientas de consulta de precio, disponibilidad, alternativas y atención a través del protocolo MCP sobre HTTP.

[![CI - Build & Test](https://github.com/arlexrush/IgnakeeAI.McpServer.Supplier/actions/workflows/ci.yml/badge.svg)](.github/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

---

## 📋 Tabla de contenidos

- [¿Qué es?](#qué-es)
- [Herramientas MCP expuestas](#herramientas-mcp-expuestas)
- [Arquitectura](#arquitectura)
- [Requisitos previos](#requisitos-previos)
- [Inicio rápido](#inicio-rápido)
- [Configuración](#configuración)
- [Integración de catálogo](#integración-de-catálogo)
- [Endpoints disponibles](#endpoints-disponibles)
- [Pruebas](#pruebas)
- [Despliegue y CI/CD](#despliegue-y-cicd)
- [Documentación adicional](#documentación-adicional)
- [Licencia](#licencia)

---

## ¿Qué es?

`IgnakeeAI MCP Supplier Server` es la pieza que instala un proveedor para publicar su catálogo y sus condiciones comerciales ante los agentes de Legio, que actúan como clientes MCP.  
El proveedor mantiene sus datos y el servidor expone, de forma controlada, consultas en tiempo real sobre:

- precios de materiales,
- disponibilidad de stock,
- alternativas y sustitutos,
- horarios y datos de contacto del proveedor.

El catálogo se alimenta desde múltiples fuentes: base de datos local (EF Core), archivos CSV/Excel y sincronización con ERP (Odoo o SAP).

### Cómo encaja Legio

El proveedor despliega este servidor en su infraestructura y entrega a Legio únicamente:

1. la URL HTTPS pública del endpoint `/mcp`;
2. un `MCP_CLIENT_ID` para identificar la integración;
3. un `MCP_API_KEY` con los scopes `catalog.read` y `availability.read`.

La clave administrativa `ADMIN_API_KEY` es exclusivamente del equipo del proveedor y nunca debe entregarse a Legio. Legio descubre las tools mediante MCP y las utiliza desde sus agentes para responder a consultas de productos, precios, alternativas y disponibilidad.

---

## Herramientas MCP expuestas

| Tool                   | Descripción                                                                                    |
|------------------------|------------------------------------------------------------------------------------------------|
| `GetPrice`             | Precio por código de artículo o descripción libre                                              |
| `SearchAlternatives`   | Búsqueda de sustitutos por criterio (`cheaper`, `better`, `onSale`, `optimalPack`, `any`)      |
| `CheckAvailability`    | Stock disponible y plazo de entrega estimado                                                   |
| `GetBusinessHours`     | Datos de contacto y horario de atención del proveedor                                          |

**Endpoint MCP:** `POST /mcp`  
**Contrato completo:** [`docs/TOOL_CONTRACT.md`](docs/TOOL_CONTRACT.md)

---

## Arquitectura
graph 
TD C["Cliente MCP (agente/LLM)"] 
--> A["API ASP.NET Core (.NET 8)"] A 
--> M["MCP Endpoint /mcp"] A 
--> H["Health Endpoint /health"] M 
--> T["MCP Tools"] T 
--> S["CatalogSearchService"] S 
--> R["ICatalogRepository"] R 
--> E["EfCatalogRepository"] E 
--> DB["SQLite / PostgreSQL / SQL Server / MySQL"] A 
--> AD["Admin Endpoints /admin/*"] AD 
--> X["Conectores: ERP / CSV / Excel"] X 
--> DB


Arquitectura en capas con enfoque hexagonal ligero:

- **Domain** — entidades y reglas de negocio (`CatalogProduct`, `EffectivePrice`)
- **Application** — casos de uso y contratos de puertos (`CatalogSearchService`, `ICatalogRepository`)
- **Infrastructure** — EF Core, conectores ERP/CSV/Excel, configuración
- **McpTools** — registro y serialización de herramientas MCP
- **Api** — bootstrap, health checks, endpoints HTTP

Más detalle en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## Requisitos previos

| Herramienta                                        | Versión mínima  |
|----------------------------------------------------|-----------------|
| [.NET SDK](https://dotnet.microsoft.com/download)  | 8.0.x           |
| [Docker](https://docs.docker.com/get-docker/)      | 24+             |
| [Docker Compose](https://docs.docker.com/compose/) | 2.x             |
| Git | cualquier |

---

## Inicio rápido

### Opción A — Docker Compose con SQLite (recomendado para desarrollo)

git clone https://github.com/arlexrush/IgnakeeAI.McpServer.Supplier.git
cd IgnakeeAI.McpServer.Supplier
Copy-Item .env.example .env.develop
docker compose --env-file .env.develop -f docker-compose.yml -f docker-compose.sqlite.yml up --build -d


### Opción B — .NET CLI

git clone https://github.com/arlexrush/IgnakeeAI.McpServer.Supplier.git
cd IgnakeeAI.McpServer.Supplier
Copy-Item .env.example .env
dotnet restore
dotnet build -c Release
dotnet run --project src/IgnakeeAI.McpServer.Supplier.Api


### Verificar funcionamiento

Invoke-WebRequest http://localhost:5100/health
Invoke-WebRequest http://localhost:5100/

Guía completa: [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md)

---

## Configuración

Copia `.env.example` a `.env.develop` y ajusta los valores necesarios para desarrollo. El Compose local carga ese fichero dentro del contenedor.

Para una integración con Legio, configura como mínimo `MCP_CLIENT_ID`, `MCP_API_KEY`, `ADMIN_API_KEY` y los datos de contacto del proveedor. En producción usa secretos externos y no subas `.env.production` al repositorio.

El stack local y el de despliegue publican la API en el puerto `5100` de forma predeterminada. `develop` se ejecuta localmente; `staged` y `master` se ejecutan en sus servidores Hetzner. Si necesitas ejecutar ambos stacks en el mismo equipo, asigna `SUPPLIER_HOST_PORT=5101` en `.env.develop` y usa `http://localhost:5101` (o `ngrok http 5101`) para desarrollo.

### Variables de entorno clave

| Variable                     | Descripción                                                  | Valor por defecto                                |
|------------------------------|--------------------------------------------------------------|--------------------------------------------------|
| `DatabaseProvider`           | Motor de BD: `sqlite`, `postgresql`, `sqlserver`, `mysql`    | `sqlite`                                         |
| `ConnectionStrings__Catalog` | Cadena de conexión a la BD                                   | `Data Source=/app/data/catalog.db`               |
| `SUPPLIER_VENDOR_NAME`       | Nombre del proveedor                                         | —                                                |
| `SUPPLIER_CONTACT_EMAIL`     | Email de contacto                                            | —                                                |
| `SUPPLIER_CONTACT_PHONE`     | Teléfono de contacto                                         | —                                                |
| `SUPPLIER_CONTACT_ADDRESS`   | Dirección                                                    | —                                                |
| `SUPPLIER_BUSINESS_HOURS`    | Horario de atención                                          | —                                                |
| `Erp__Provider`              | ERP activo: `odoo`, `sap` o vacío                            | vacío                                            |
| `ADMIN_API_KEY`              | Autenticación del proveedor para `/admin/*`                  | —                                                |
| `MCP_CLIENT_ID`              | Identidad del cliente MCP (Legio)                            | —                                                |
| `MCP_API_KEY`                | Clave que Legio enviará en `X-Api-Key`                       | —                                                |
| `EcommerceInventory__Enabled` | Habilita el conector con Ecommerce                          | `false`                                          |
| `EcommerceInventory__BaseUrl` | URL base del inventario de Ecommerce                        | —                                                |
| `EcommerceInventory__BearerToken` | JWT técnico para el header `Authorization`            | —                                                |
| `EcommerceInventory__ProductLookupPath` | Ruta del producto individual                   | `/api/v1/inventory/{productCode}`                |
| `EcommerceInventory__CatalogSyncPath` | Ruta del catálogo paginado                       | `/api/v1/inventory`                              |
| `EcommerceInventory__SyncPageSize` | Tamaño de página seguro para sync                    | `50`                                             |

Las migraciones de base de datos se aplican automáticamente al arrancar (`ApplyMigrationsOnStartup: true`).

> Si ejecutas el proceso fuera de Docker Compose, usa las claves runtime `Admin__ApiKey`, `Mcp__Clients__0__ClientId` y `Mcp__Clients__0__ApiKey`. Los nombres `ADMIN_API_KEY`, `MCP_CLIENT_ID` y `MCP_API_KEY` del `.env.example` son alias prácticos para `docker-compose.production.yml`.

---

## Integración de catálogo

### ERP — Odoo o SAP

Configurar `Erp__Provider` en `appsettings.json` o como variable de entorno y ejecutar el endpoint administrativo con `X-Api-Key: ADMIN_API_KEY`:

curl -X POST http://localhost:5100/admin/sync/erp -H "X-Api-Key: <ADMIN_API_KEY>"


### CSV
curl -X POST http://localhost:5100/admin/sync/csv -H "X-Api-Key: <ADMIN_API_KEY>" -F "file=@catalogo.csv"


Separador `;` con cabeceras: `ItemCode;Description;Category;Keywords;Unit;UnitPrice;...`

### Excel

curl -X POST http://localhost:5100/admin/sync/excel -H "X-Api-Key: <ADMIN_API_KEY>" -F "file=@catalogo.xlsx"


Hoja `Catalogo` (o primera hoja), columnas A–Q.

### Ecommerce — IgnakeeEcommerce-BackEnd

El conector de inventario ecommerce conecta con la API REST de inventario de `IgnakeeEcommerce-BackEnd`.

**Contrato real consumido por el Supplier:**

| Operación | Método | Ruta |
|-----------|--------|------|
| Producto individual | `GET` | `/api/v1/inventory/{productCode}` |
| Catálogo paginado (PaginationVm) | `GET` | `/api/v1/inventory?pageIndex={n}&pageSize={n}` |

Autenticación: header `Authorization` con el JWT del usuario técnico. La identidad técnica debe tener el rol `INVENTORY_READER` (o `ADMIN` para break-glass). `SUPPLIER_INTEGRATION` por sí solo no autoriza este controlador.

La respuesta del catálogo usa la semántica `PaginationVm<T>`: `data[]`, `count`, `pageIndex`, `pageSize`, `pageCount`, `resultByPage`.

> **Fuente del contrato:** `src/Api/Ecommerce.Api/Controllers/InventoryController.cs` y
> `src/Core/Ecommerce.Application/…/Features/Shared/Queries/PaginationBaseQuery.cs`
> en `arlexrush/IgnakeeEcommerce-BackEnd` (rama por defecto).

> **Límite del servidor:** el ecommerce aplica `MaxPagesSize = 50` en `PaginationBaseQuery`.
> `SyncPageSize` debe ser ≤ 50; valores superiores son recortados silenciosamente y
> provocan que el bucle de sincronización termine en la primera página, perdiendo el resto del catálogo.

**Configuración mínima en Supplier:** `EcommerceInventory__Enabled=true`, `EcommerceInventory__BaseUrl`, `EcommerceInventory__BearerToken` y `EcommerceInventory__SyncPageSize=50`.

> **Seguridad:** nunca confirmes `BearerToken` en el repositorio. Inyecta el token mediante `EcommerceInventory__BearerToken` o un secret manager.

**Sincronización manual del catálogo:**

```bash
curl -X POST http://localhost:5100/admin/sync/ecommerce \
  -H "X-Api-Key: <ADMIN_API_KEY>"
```

Pagina por el catálogo activo del ecommerce (usando `pageIndex` y `pageSize`) y realiza upsert por `ItemCode`/`ProductCode`.

**Disponibilidad en tiempo real (`CheckAvailability`):**
Cuando `Enabled = true`, `checkAvailability` consulta el ecommerce en tiempo real antes de caer al catálogo local. Si el ecommerce falla o no responde, la operación continúa con el catálogo local sin romper el contrato MCP.

**Mapping de campos:**

| Campo ecommerce        | Campo `CatalogProduct`  | Notas                                    |
|------------------------|-------------------------|------------------------------------------|
| `productCode`          | `ItemCode`              | Identificador canónico externo           |
| `productName`/`description` | `Description`     | description tiene prioridad              |
| `category`             | `Category`              |                                          |
| `price`                | `UnitPrice`             |                                          |
| `currency`             | `Currency`              | Default: EUR                             |
| `stock`                | `AvailableStock`        |                                          |
| `unitToSell`           | `Unit`                  | Default: ud                              |
| `purchaseLeadTime`     | `LeadTimeDays`          | Normalizado: hours÷24, weeks×7           |
| `status = "active"`    | `IsActive = true`       | Cualquier otro valor → false             |

Runbook completo: [`docs/ECOMMERCE_INTEGRATION_RUNBOOK.md`](docs/ECOMMERCE_INTEGRATION_RUNBOOK.md)  
Otras fuentes de carga: [`docs/ERP_INTEGRATION.md`](docs/ERP_INTEGRATION.md)
---

## Endpoints disponibles

| Método | Ruta                  | Descripción                                   |
|--------|-----------------------|-----------------------------------------------|
| `GET`  | `/health`             | Estado del servicio                           |
| `GET`  | `/`                   | Metadata del servidor y tools declaradas      |
| `POST` | `/mcp`                | Endpoint MCP (HTTP transport)                 |
| `POST` | `/admin/sync/erp`     | Sincronizar catálogo desde ERP                |
| `POST` | `/admin/sync/csv`     | Importar catálogo desde CSV                   |
| `POST` | `/admin/sync/excel`   | Importar catálogo desde Excel                 |
| `POST` | `/admin/sync/ecommerce`| Sincronizar catálogo desde ecommerce         |
| `GET`  | `/admin/catalog/stats`| Estadísticas del catálogo                     |

---

## Pruebas

Suites incluidas:

- `PricingToolsTests` — precio por código, descripción, oferta, no encontrado
- `AlternativeSearchTests` — criterios de sustitución
- `OdooConnectorTests` — happy path, errores de autenticación, upsert, nulables, JSON-RPC
- `EcommerceInventoryConnectorTests` — bearer auth header, nullable price, `data` envelope, `pageIndex` URL param, not-found, 401/403 auth errors, malformed JSON, lead-time normalization, isAvailableForSale mapping, cancellation propagation, hybrid availability fallback

---

## Despliegue y CI/CD

### Pipelines de GitHub Actions

| Pipeline        | Activación                            | Acción                                 |
|-----------------|---------------------------------------|----------------------------------------|
| `ci.yml`        | push o PR a `develop`, `staged` o `master` | restore → build → test             |
| `release.yml`   | push a `develop`, `staged` o `master` | build multi-arch → push a GHCR; despliega en Hetzner sólo desde `staged` y `master` |

### Promoción de cambios

`feature/*` y `fix/*` se integran en `develop`, que se ejecuta localmente y se expone temporalmente mediante ngrok. Tras validarlo, se promociona mediante Pull Request a `staged`, desplegado en Hetzner como preproducción, y finalmente a `master`, desplegado en Hetzner como producción.

Cada ejecución publica una imagen inmutable con etiqueta `sha-<commit>`. Las etiquetas de rama (`develop`, `staged`, `master`) son de conveniencia y `latest` sólo se actualiza desde `master`.


Guía completa: [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)

---

## Documentación adicional

| Documento                                             | Contenido                                                                        |
|-------------------------------------------------------|----------------------------------------------------------------------------------|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)        | Arquitectura, flujos, modelo de dominio, decisiones técnicas                     |
| [`docs/ECOMMERCE_INTEGRATION_RUNBOOK.md`](docs/ECOMMERCE_INTEGRATION_RUNBOOK.md) | Runbook canónico de integración Ecommerce ↔ Supplier                 |
| [`docs/TOOL_CONTRACT.md`](docs/TOOL_CONTRACT.md)      | Contrato MCP: parámetros y esquemas de respuesta de cada tool                    |
| [`docs/ERP_INTEGRATION.md`](docs/ERP_INTEGRATION.md)  | Manual de integración con Odoo, SAP, CSV y Excel                                 |
| [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)            | Despliegue local, en contenedor y CI/CD                                          |
| [`docs/POSTGRESQL_RESET.md`](docs/POSTGRESQL_RESET.md) | Cambio de credenciales y reinicialización de PostgreSQL por entorno              |
| [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md)  | Guía de inicio rápido                                                            |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)                  | Cómo contribuir al proyecto y CLA                                                |

---

## Licencia

Este proyecto está licenciado bajo la [Apache License 2.0](LICENSE.txt).  
Copyright 2026 IgnakeeAI.


