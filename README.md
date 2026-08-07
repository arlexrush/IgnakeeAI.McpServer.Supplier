# IgnakeeAI MCP Supplier Server

> Servidor MCP (Model Context Protocol) para catálogo de proveedor, construido con .NET 8.  
> Expone herramientas de consulta de precio, disponibilidad, alternativas y atención a través del protocolo MCP sobre HTTP.

[![CI - Build & Test](https://github.com/IgnakeeProjects/mcp-supplier-server/actions/workflows/ci.yml/badge.svg)](github/workflows/ci.yml)
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

`IgnakeeAI MCP Supplier Server` es un servidor MCP orientado al sector de construcción y distribución.  
Permite que agentes LLM o clientes MCP consulten en tiempo real:

- precios de materiales,
- disponibilidad de stock,
- alternativas y sustitutos,
- horarios y datos de contacto del proveedor.

El catálogo se alimenta desde múltiples fuentes: base de datos local (EF Core), archivos CSV/Excel y sincronización con ERP (Odoo o SAP).

---

## Herramientas MCP expuestas

| Tool                   | Descripción                                                                                    |
|------------------------|------------------------------------------------------------------------------------------------|
| `getPrice`             | Precio por código de artículo o descripción libre                                              |
| `searchAlternatives`   | Búsqueda de sustitutos por criterio (`cheaper`, `better`, `onSale`, `optimalPack`, `any`)      |
| `checkAvailability`    | Stock disponible y plazo de entrega estimado                                                   |
| `getBusinessHours`     | Datos de contacto y horario de atención del proveedor                                          |

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

git clone <repo-url> 
cd <repo-folder> 
cp .env.example .env 
docker compose -f docker-compose.yml -f 
docker-compose.sqlite.yml up --build -d


### Opción B — .NET CLI

git clone <repo-url> 
cd <repo-folder> 
cp .env.example .env 
dotnet restore 
dotnet build -c Release 
dotnet run --project src/IgnakeeAI.McpServer.Supplier.Api


### Verificar funcionamiento

curl http://localhost:5100/health curl http://localhost:5100/

Guía completa: [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md)

---

## Configuración

Copia `.env.example` a `.env` y ajusta los valores necesarios.

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

Las migraciones de base de datos se aplican automáticamente al arrancar (`ApplyMigrationsOnStartup: true`).

---

## Integración de catálogo

### ERP — Odoo o SAP

Configurar `Erp__Provider` en `appsettings.json` o como variable de entorno y ejecutar:

curl -X POST http://localhost:5100/admin/sync/erp


### CSV
curl -X POST http://localhost:5100/admin/sync/csv -F "file=@catalogo.csv"


Separador `;` con cabeceras: `ItemCode;Description;Category;Keywords;Unit;UnitPrice;...`

### Excel

curl -X POST http://localhost:5100/admin/sync/excel -F "file=@catalogo.xlsx"


Hoja `Catalogo` (o primera hoja), columnas A–Q.

### Ecommerce — IgnakeeEcommerce-BackEnd

El conector de inventario ecommerce conecta con la API REST de inventario de `IgnakeeEcommerce-BackEnd`.

**Habilitar la integración** (`appsettings.json` o variables de entorno):

```json
"EcommerceInventory": {
  "Enabled": true,
  "BaseUrl": "https://ecommerce.example.com",
  "ApiKeyHeaderName": "X-Api-Key",
  "ApiKeyValue": "",
  "TimeoutSeconds": 10,
  "ProductLookupPath": "/api/inventory/products/{productCode}",
  "CatalogSyncPath": "/api/inventory/products",
  "SyncPageSize": 100
}
```

> **Seguridad:** nunca confirmes `ApiKeyValue` en el repositorio. Usa la variable de entorno `EcommerceInventory__ApiKeyValue` o un secret manager.

**Sincronización manual del catálogo:**

```bash
curl -X POST http://localhost:5100/admin/sync/ecommerce \
  -H "X-Admin-Key: <admin-key>"
```

Pagina por el catálogo activo del ecommerce y realiza upsert por `ItemCode`/`ProductCode`.

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

Manual completo: [`docs/ERP_INTEGRATION.md`](docs/ERP_INTEGRATION.md)

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
- `EcommerceInventoryConnectorTests` — success, pagination, not-found, auth error, malformed JSON, lead-time normalization, hybrid availability fallback

---

## Despliegue y CI/CD

### Pipelines de GitHub Actions

| Pipeline        | Activación                            | Acción                                 |
|-----------------|---------------------------------------|----------------------------------------|
| `ci.yml`        | push a `main`/`develop` o PR a `main` | restore → build → test                 |
| `release.yml`   | push de tag `v*` (ej. `v1.0.0`)       | build imagen multi-arch → push a GHCR  |

### Publicar una nueva versión
git tag v1.0.0 git push origin v1.0.0


La imagen queda disponible en:
ghcr.io/ignakeeai/mcp-supplier-server:1.0.0 ghcr.io/ignakeeai/mcp-supplier-server:latest


Guía completa: [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)

---

## Documentación adicional

| Documento                                             | Contenido                                                                        |
|-------------------------------------------------------|----------------------------------------------------------------------------------|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)        | Arquitectura, flujos, modelo de dominio, decisiones técnicas                     |
| [`docs/TOOL_CONTRACT.md`](docs/TOOL_CONTRACT.md)      | Contrato MCP: parámetros y esquemas de respuesta de cada tool                    |
| [`docs/ERP_INTEGRATION.md`](docs/ERP_INTEGRATION.md)  | Manual de integración con Odoo, SAP, CSV y Excel                                 |
| [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)            | Despliegue local, en contenedor y CI/CD                                          |
| [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md)  | Guía de inicio rápido                                                            |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)                  | Cómo contribuir al proyecto y CLA                                                |

---

## Licencia

Este proyecto está licenciado bajo la [Apache License 2.0](LICENSE.txt).  
Copyright 2026 IgnakeeAI.


