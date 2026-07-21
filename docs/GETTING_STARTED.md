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
- Comprobar salud y metadata:
  - `curl http://localhost:5100/health`
  - `curl http://localhost:5100/`
- Las peticiones MCP requieren `X-Api-Key` de un cliente configurado.

### 7.1 `initialize`

```powershell
curl http://localhost:5100/mcp -Method Post -ContentType 'application/json' -Headers @{ 'X-Api-Key' = '<mcp-secret>' } -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"Legio","version":"1.0.0"}}}'
```

Después de `initialize`, enviar la notificación `notifications/initialized`:

```json
{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}
```

### 7.2 `tools/list`

```powershell
curl http://localhost:5100/mcp -Method Post -ContentType 'application/json' -Headers @{ 'X-Api-Key' = '<mcp-secret>' } -Body '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
```

### 7.3 `tools/call`

```powershell
# GetPrice
curl http://localhost:5100/mcp -Method Post -ContentType 'application/json' -Headers @{ 'X-Api-Key' = '<mcp-secret>' } -Body '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"GetPrice","arguments":{"itemDescription":"cemento portland 25kg","itemCode":"CEM-001","currency":"EUR"}}}'

# SearchAlternatives
curl http://localhost:5100/mcp -Method Post -ContentType 'application/json' -Headers @{ 'X-Api-Key' = '<mcp-secret>' } -Body '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"SearchAlternatives","arguments":{"itemDescription":"cemento","criteria":"cheaper","maxResults":5,"currency":"EUR"}}}'

# CheckAvailability
curl http://localhost:5100/mcp -Method Post -ContentType 'application/json' -Headers @{ 'X-Api-Key' = '<mcp-secret>' } -Body '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"CheckAvailability","arguments":{"itemCode":"CEM-001"}}}'
```

- `GetBusinessHours` no requiere argumentos y se invoca con el mismo formato `tools/call`.
- Los nombres oficiales son PascalCase y las respuestas contienen JSON `camelCase`.
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
