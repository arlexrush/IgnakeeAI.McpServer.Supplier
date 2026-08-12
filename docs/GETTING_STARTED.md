# Inicio para proveedores e integración con Legio

## 1. Objetivo

Esta guía está dirigida a un proveedor que quiere publicar su catálogo ante los agentes de **Legio**, el cliente MCP que consumirá este servidor.

El flujo recomendado es:

```text
ERP / CSV / Excel del proveedor
              ?
              ?
IgnakeeAI MCP Supplier Server
              ?  HTTPS + X-Api-Key
              ?
Agentes de Legio (cliente MCP)
```

Legio nunca necesita acceso directo a la base de datos ni a las credenciales del ERP. Solo consume el endpoint MCP autorizado.

## 2. Requisitos

- Docker Desktop o Docker Engine y Docker Compose 2.x.
- Acceso al repositorio privado y al paquete GHCR.
- Una URL HTTPS accesible por Legio en producción.
- Credenciales del ERP, si se sincroniza Odoo o SAP.
- Una API key MCP independiente para Legio.

## 3. Configuración local

```powershell
Copy-Item .env.example .env.develop
notepad .env.develop
docker compose --env-file .env.develop -f docker-compose.yml -f docker-compose.sqlite.yml up --build -d
```

La API local queda disponible en `http://localhost:5100`.

Los stacks de `staged` y `master` corresponden a servidores Hetzner distintos. Si uno ocupa el puerto local `5100`, configura `SUPPLIER_HOST_PORT=5101` en `.env.develop`, añade `--env-file .env.develop` al comando de Compose y usa `http://localhost:5101` para el stack local.

Para PostgreSQL local:

```powershell
docker compose --env-file .env.develop -f docker-compose.yml -f docker-compose.override.yml up --build -d
```

## 4. Variables de autenticación

| Variable | Uso |
|---|---|
| `ADMIN_API_KEY` | Clave privada del proveedor para `/admin/*`. No se entrega a Legio. |
| `MCP_CLIENT_ID` | Identificador de la integración Legio. |
| `MCP_API_KEY` | Clave que Legio enviará en la cabecera `X-Api-Key`. |
| `Mcp__ContractVersion` | Versión del contrato MCP. |
| `Mcp__ProtocolVersion` | Versión del protocolo MCP negociada. |

No uses la misma clave para administración y MCP. En producción genera claves aleatorias, guárdalas en un gestor de secretos y no las incluyas en Git.

## 5. Verificar la instalación

```powershell
Invoke-WebRequest http://localhost:5100/health
Invoke-WebRequest http://localhost:5100/
```

Las peticiones MCP requieren `X-Api-Key` con el valor real de `MCP_API_KEY`. No sustituyas la clave por un texto como `<MCP_API_KEY_REAL>`.

```powershell
$mcpApiKey = ((Get-Content .env) | Where-Object { $_ -match '^MCP_API_KEY=' }) -replace '^MCP_API_KEY=', ''
$headers = @{
  'X-Api-Key' = $mcpApiKey
  'Accept' = 'application/json, text/event-stream'
  'Content-Type' = 'application/json'
}

$body = @{
  jsonrpc = '2.0'
  id = 1
  method = 'initialize'
  params = @{
    protocolVersion = '2025-03-26'
    capabilities = @{}
    clientInfo = @{ name = 'Legio'; version = '1.0.0' }
  }
} | ConvertTo-Json -Depth 10

Invoke-WebRequest http://localhost:5100/mcp -Method Post -Headers $headers -Body $body
```

Después del `initialize`, Legio envía `notifications/initialized` y consulta `tools/list`. Las tools disponibles son:

- `GetPrice`
- `SearchAlternatives`
- `CheckAvailability`
- `GetBusinessHours`

## 6. Preparar la integración de Legio

El proveedor debe entregar al equipo de integración de Legio, por canal seguro:

- `MCP endpoint`: `https://dominio-del-proveedor.example/mcp`;
- `MCP client ID`: el valor de `MCP_CLIENT_ID`;
- `MCP API key`: el valor de `MCP_API_KEY`;
- versión de protocolo soportada: `2025-03-26`;
- versión de contrato: `1.0.0`;
- scopes: `catalog.read` y `availability.read`.

No entregue `ADMIN_API_KEY`, credenciales PostgreSQL ni credenciales de Odoo/SAP.

## 7. Datos del catálogo

Antes de conectar Legio, cargue datos mediante ERP, CSV o Excel y verifique:

```text
GET  /admin/catalog/stats
POST /admin/sync/erp
POST /admin/sync/csv
POST /admin/sync/excel
```

Estos endpoints requieren `ADMIN_API_KEY`. Consulte [`ERP_INTEGRATION.md`](ERP_INTEGRATION.md) para el formato y la sincronización.

## 8. Checklist de aceptación

- [ ] `/health` responde HTTP 200.
- [ ] La base de datos contiene productos activos.
- [ ] `tools/list` devuelve las cuatro tools.
- [ ] `GetPrice` devuelve precios y datos de contacto del proveedor.
- [ ] `SearchAlternatives` devuelve sustitutos relevantes.
- [ ] `CheckAvailability` devuelve stock y plazo.
- [ ] La API key MCP no permite acceder a `/admin/*`.
- [ ] `ADMIN_API_KEY` no se ha compartido con Legio.
- [ ] La URL de producción usa HTTPS y tiene control de acceso/red.

## 9. Pruebas

```powershell
dotnet test
```

Documentos relacionados:

- [`TOOL_CONTRACT.md`](TOOL_CONTRACT.md): contrato que Legio consume.
- [`DEPLOYMENT.md`](DEPLOYMENT.md): despliegue local, GHCR y producción.
- [`ERP_INTEGRATION.md`](ERP_INTEGRATION.md): carga y sincronización del catálogo.
- [`ARCHITECTURE.md`](ARCHITECTURE.md): componentes y límites de seguridad.
