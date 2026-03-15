# Getting Started

## 1. Objetivo
Guía rápida para ejecutar `IgnakeeAI MCP Supplier Server` en local.

## 2. Prerrequisitos
- .NET SDK 8
- Docker y Docker Compose (opcional, recomendado)
- Git
- (Opcional) SQLite / SQL Server según `DataSourceSettings`

## 3. Clonar y preparar
git clone <repo-url> 
cd <repo-folder>

## 4. Configuración
- Copiar `.env.example` a `.env`
- Revisar:
  - `src/IgnakeeAI.McpServer.Supplier.Api/appsettings.json`
  - `src/IgnakeeAI.McpServer.Supplier.Api/appsettings.Development.json`
- Ajustar proveedor de datos (CSV / Excel / EF / ERP)

## 5. Inicializar datos
- Opción SQL seed: `seed/seed-catalog.sql`
- Opción conectores: CSV/Excel/Odoo/SAP según configuración

## 6. Ejecutar la aplicación
### Opción A: .NET local
- dotnet restore 
- dotnet build 
- dotnet run --project src/IgnakeeAI.McpServer.Supplier.Api


### Opción B: Docker
- docker compose up --build
(usar `docker-compose.sqlite.yml` si aplica)
## SQLite:
-	docker compose -f docker-compose.yml -f docker-compose.sqlite.yml up -d
## PostgreSQL:
-	docker compose -f docker-compose.yml -f docker-compose.override.yml up -d

## 7. Verificar funcionamiento
- Endpoint MCP: `/mcp`
- Probar tools:
  - `getPrice`
  - `searchAlternatives`
  - `checkAvailability`
  - `getBusinessHours`
- Contrato: `docs/TOOL_CONTRACT.md`

## 8. Ejecutar tests
- dotnet test

Tests clave:
- `PricingToolsTests`
- `AlternativeSearchTests`
- `OdooConnectorTests`

## 9. Solución de problemas
- Revisar configuración de conexión (ERP/DB)
- Confirmar variables de entorno
- Validar formato de salida JSON `camelCase`
- Revisar logs de ejecución

## 10. Siguientes pasos
- Arquitectura: `docs/ARCHITECTURE.md`
- Contrato de tools: `docs/TOOL_CONTRACT.md`
