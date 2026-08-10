# ECOMMERCE_INTEGRATION_RUNBOOK

Guía operativa para configurar desde cero la integración entre `IgnakeeEcommerce-BackEnd` y `IgnakeeAI.McpServer.Supplier`, alineada con el contrato implementado actualmente en ambos repositorios.

> Este runbook toma la implementación como fuente de verdad. Mantiene sin cambios los nombres MCP (`GetPrice`, `SearchAlternatives`, `CheckAvailability`, `GetBusinessHours`) y `Mcp:ContractVersion = 1.0.0`.

## 1. Qué corrige este documento

Antes existían referencias conflictivas sobre:

- rol permitido en Ecommerce (`SUPPLIER_INTEGRATION` en docs antiguas vs `INVENTORY_READER` en el controlador real);
- rutas antiguas (`/api/v1/inventory/product/{productCode}` y `/api/v1/inventory/catalog`) vs rutas implementadas;
- uso de `X-Admin-Key` en ejemplos del Supplier vs header real `X-Api-Key`;
- `SyncPageSize=100` en ejemplos, aunque el Ecommerce limita `pageSize` a `50`.

Este runbook normaliza el flujo real:

- **Producto individual:** `GET /api/v1/inventory/{productCode}`
- **Catálogo paginado:** `GET /api/v1/inventory?pageIndex={n}&pageSize={n}`
- **Auth Ecommerce:** encabezado `Authorization` con el JWT del usuario técnico autenticado
- **Rol requerido:** `INVENTORY_READER` (o `ADMIN` como break-glass)
- **Sync Supplier:** `POST /admin/sync/ecommerce` con `X-Api-Key: <Admin:ApiKey>`

## 2. Prerrequisitos

### 2.1 Servicios y entornos

- Ecommerce desplegado y accesible por HTTPS.
- Supplier desplegado y accesible por HTTPS.
- Conectividad Supplier → Ecommerce.
- Un entorno donde puedas guardar secretos fuera del repositorio.

### 2.2 Secretos mínimos

#### En Ecommerce

- usuario técnico activo;
- contraseña del usuario técnico;
- JWT emitido por Ecommerce para ese usuario.

#### En Supplier

- `Admin__ApiKey` (o `ADMIN_API_KEY` si usas `docker-compose.production.yml`);
- `Mcp__Clients__0__ApiKey` / `MCP_API_KEY`;
- `EcommerceInventory__BearerToken`.

## 3. Contrato canónico: qué expone Ecommerce y qué consume Supplier

| Ecommerce expone | Supplier consume | Notas |
|---|---|---|
| `GET /api/v1/inventory/{productCode}` | lookup en tiempo real para `CheckAvailability` | `productCode` es el identificador externo canónico |
| `GET /api/v1/inventory?pageIndex={n}&pageSize={n}` | sincronización inicial y posteriores `/admin/sync/ecommerce` | usar `pageSize <= 50` |
| encabezado `Authorization` con el JWT del usuario técnico | `EcommerceInventory__BearerToken` | no usar API key para esta llamada |
| roles `ADMIN` o `INVENTORY_READER` en `InventoryController` | identidad técnica del Supplier | `SUPPLIER_INTEGRATION` por sí solo no sirve para este controlador |
| `PaginationVm<T>` con `data`, `count`, `pageIndex`, `pageSize`, `pageCount`, `resultByPage` | bucle de sincronización paginado del Supplier | cambiar estas claves rompe el import |

## 4. Acciones obligatorias del lado Ecommerce

### 4.1 Crear o elegir identidad técnica

Usa un usuario técnico dedicado para el Supplier. No reutilices cuentas humanas.

### 4.2 Asignar rol correcto

La autorización real del endpoint de inventario está en `InventoryController`:

- válido: `INVENTORY_READER`
- válido para contingencia: `ADMIN`
- **no suficiente:** `SUPPLIER_INTEGRATION` si no va acompañado de `INVENTORY_READER` o `ADMIN`

> El rol `INVENTORY_READER` está sembrado en `EcommerceDbContextData.cs`. El rol `SUPPLIER_INTEGRATION` existe en `Role.cs`, pero no es el que protege `InventoryController`.

### 4.3 Emitir JWT

Flujo implementado hoy:

1. autenticar contra `POST /api/v1/user/login`
2. enviar JSON con `email` y `password`
3. recibir `AuthResponse` con `token` y `roles`
4. configurar ese `token` como `EcommerceInventory__BearerToken` en el Supplier

Ejemplo:

```bash
curl -sS -X POST "$ECOMMERCE_BASE_URL/api/v1/user/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "supplier.integration@example.com",
    "password": "'"$ECOMMERCE_PASSWORD"'"
  }'
```

Respuesta esperada parcial:

```json
{
  "email": "supplier.integration@example.com",
  "roles": ["INVENTORY_READER"],
  "token": "<jwt>"
}
```

## 5. Endpoints exactos consumidos por Supplier

### 5.1 Producto individual

```bash
curl -sS "$ECOMMERCE_BASE_URL/api/v1/inventory/ECO-001" \
  -H "$ECOMMERCE_AUTH_HEADER"
```

Respuesta esperada:

```json
{
  "productCode": "ECO-001",
  "productId": 101,
  "productName": "Cable THHN 12 AWG",
  "description": "Cable THHN 12 AWG rojo",
  "category": "cables",
  "price": 12.5,
  "currency": "USD",
  "isAvailableForSale": true,
  "stock": 34,
  "unitToSell": "rollo",
  "purchaseLeadTime": 2,
  "purchaseLeadTimeUnit": "days",
  "status": "Active"
}
```

### 5.2 Catálogo paginado

```bash
curl -sS "$ECOMMERCE_BASE_URL/api/v1/inventory?pageIndex=1&pageSize=50" \
  -H "$ECOMMERCE_AUTH_HEADER"
```

Respuesta esperada:

```json
{
  "pageIndex": 1,
  "pageSize": 50,
  "count": 248,
  "pageCount": 5,
  "resultByPage": 50,
  "data": [
    {
      "productCode": "ECO-001",
      "productId": 101,
      "productName": "Cable THHN 12 AWG",
      "description": "Cable THHN 12 AWG rojo",
      "category": "cables",
      "price": 12.5,
      "currency": "USD",
      "isAvailableForSale": true,
      "stock": 34,
      "unitToSell": "rollo",
      "purchaseLeadTime": 2,
      "purchaseLeadTimeUnit": "days",
      "status": "Active"
    }
  ]
}
```

### 5.3 Headers y auth esperados

- **Ecommerce:** encabezado `Authorization` con el JWT válido del usuario técnico
- **Supplier /admin/*:** `X-Api-Key: <Admin:ApiKey>`
- **Supplier /mcp:** `X-Api-Key: <Mcp client api key>`

## 6. Supplier configuration

### 6.1 Variables del conector Ecommerce

| appsettings | env var | Requerido | Valor recomendado |
|---|---|---|---|
| `EcommerceInventory:Enabled` | `EcommerceInventory__Enabled` | sí | `true` |
| `EcommerceInventory:BaseUrl` | `EcommerceInventory__BaseUrl` | sí | URL base del Ecommerce, sin slash final |
| `EcommerceInventory:BearerToken` | `EcommerceInventory__BearerToken` | sí | JWT del usuario técnico |
| `EcommerceInventory:TimeoutSeconds` | `EcommerceInventory__TimeoutSeconds` | no | `10` |
| `EcommerceInventory:ProductLookupPath` | `EcommerceInventory__ProductLookupPath` | no | `/api/v1/inventory/{productCode}` |
| `EcommerceInventory:CatalogSyncPath` | `EcommerceInventory__CatalogSyncPath` | no | `/api/v1/inventory` |
| `EcommerceInventory:SyncPageSize` | `EcommerceInventory__SyncPageSize` | no | `50` |

> No confirmes `EcommerceInventory__BearerToken` en el repositorio ni lo escribas en logs.

### 6.2 Variables administrativas y MCP

Si ejecutas el proceso directamente, usa las claves reales que consume ASP.NET Core:

- `Admin__ApiKey`
- `Mcp__Clients__0__ClientId`
- `Mcp__Clients__0__ApiKey`
- `Mcp__Clients__0__Scopes__0=catalog.read`
- `Mcp__Clients__0__Scopes__1=availability.read`

Si usas `docker-compose.production.yml`, puedes mantener los alias del `.env`:

- `ADMIN_API_KEY`
- `MCP_CLIENT_ID`
- `MCP_API_KEY`

porque el compose los mapea a `Admin__ApiKey` y `Mcp__Clients__0__*`.

## 7. Procedimiento de sincronización inicial

1. valida el token contra el endpoint de producto;
2. valida una página del catálogo con `pageSize=50`;
3. configura el Supplier con `EcommerceInventory__Enabled=true`;
4. reinicia el Supplier;
5. ejecuta la sincronización inicial:

```bash
curl -sS -X POST "$SUPPLIER_BASE_URL/admin/sync/ecommerce" \
  -H "X-Api-Key: $SUPPLIER_ADMIN_API_KEY"
```

Respuesta esperada:

```json
{
  "source": "ecommerce",
  "productsImported": 248,
  "productsUpserted": 248,
  "productsSkipped": 0,
  "syncedAt": "2026-08-10T00:00:00Z"
}
```

### Importante sobre paginación

El Ecommerce recorta `pageSize` a un máximo de `50`. El Supplier termina el bucle cuando la página devuelta trae menos elementos que el `pageSize` solicitado. Por eso:

- **usa `EcommerceInventory__SyncPageSize=50`**
- no subas ese valor a `100`

Si lo subes, el Ecommerce devolverá 50 elementos y el Supplier interpretará erróneamente que ya llegó a la última página.

## 8. Comportamiento en tiempo real y fallback

`CheckAvailability` funciona así:

1. si `EcommerceInventory` está habilitado y `BaseUrl` existe, intenta `GET /api/v1/inventory/{productCode}`;
2. si Ecommerce responde con producto válido, devuelve stock y lead time en tiempo real;
3. si Ecommerce falla, responde 404, devuelve null o hay timeout/error de red, el Supplier **cae al catálogo local**;
4. si no existe ni en Ecommerce ni en catálogo local, responde `found: false`.

Implicaciones:

- `CheckAvailability` puede seguir funcionando aunque Ecommerce esté caído, si el producto ya fue sincronizado;
- `GetPrice` y `SearchAlternatives` siguen usando el catálogo local.

## 9. Day 1 validation script

Exporta variables. `ECOMMERCE_AUTH_HEADER` debe contener el header completo de autorización listo para pasar a `curl -H ...`, construido a partir del JWT técnico obtenido en `POST /api/v1/user/login`.

```bash
export ECOMMERCE_BASE_URL="https://ecommerce.example.com"
export ECOMMERCE_JWT="<jwt>"
export ECOMMERCE_AUTH_HEADER="Authorization: Bearer ${ECOMMERCE_JWT}"
export SUPPLIER_BASE_URL="https://supplier.example.com"
export SUPPLIER_ADMIN_API_KEY="<admin-api-key>"
export SUPPLIER_MCP_API_KEY="<mcp-api-key>"
export PRODUCT_CODE="ECO-001"
```

### 9.1 Validar producto individual en Ecommerce

```bash
curl -i -sS "$ECOMMERCE_BASE_URL/api/v1/inventory/$PRODUCT_CODE" \
  -H "$ECOMMERCE_AUTH_HEADER"
```

**Éxito:** `200 OK` y body con `productCode`, `isAvailableForSale`, `status`.  
**Fallos típicos:** `401` token inválido/expirado, `403` falta `INVENTORY_READER`, `404` código o ruta incorrecta.

### 9.2 Validar catálogo paginado en Ecommerce

```bash
curl -i -sS "$ECOMMERCE_BASE_URL/api/v1/inventory?pageIndex=1&pageSize=50" \
  -H "$ECOMMERCE_AUTH_HEADER"
```

**Éxito:** `200 OK` con `data[]`, `count`, `pageIndex`, `pageSize`, `pageCount`, `resultByPage`.  
**Fallos típicos:** `400` por `pageIndex/pageSize` inválidos, `401/403` por auth, body sin `data` indica drift de contrato.

### 9.3 Lanzar sincronización del Supplier

```bash
curl -i -sS -X POST "$SUPPLIER_BASE_URL/admin/sync/ecommerce" \
  -H "X-Api-Key: $SUPPLIER_ADMIN_API_KEY"
```

**Éxito:** `200 OK` con `source=ecommerce` y `productsImported > 0`.  
**Fallos típicos:** `401` key ausente/incorrecta, `403` key MCP usada en `/admin/*`, `400` integración deshabilitada, `502` problema de auth/mapeo/comunicación con Ecommerce.

### 9.4 Probar MCP `CheckAvailability`

```bash
curl -i -sS -X POST "$SUPPLIER_BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $SUPPLIER_MCP_API_KEY" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "CheckAvailability",
      "arguments": {
        "itemCode": "'"$PRODUCT_CODE"'"
      }
    }
  }'
```

**Éxito:** `200 OK` y respuesta MCP con contenido serializado que incluya `found`, `itemCode`, `availableStock`, `leadTimeDays`, `message`.  
**Fallos típicos:** `401` sin `X-Api-Key`, `403` key administrativa o sin scopes MCP, `found:false` si no existe ni en ecommerce ni en catálogo local.

## 10. Checklist de verificación

- [ ] el usuario técnico del Ecommerce existe y está activo
- [ ] el JWT devuelve `roles` que incluyen `INVENTORY_READER` o `ADMIN`
- [ ] `GET /api/v1/inventory/{productCode}` responde `200`
- [ ] `GET /api/v1/inventory?pageIndex=1&pageSize=50` responde `200`
- [ ] el Supplier tiene `EcommerceInventory__Enabled=true`
- [ ] el Supplier tiene `EcommerceInventory__SyncPageSize=50`
- [ ] `POST /admin/sync/ecommerce` importa productos
- [ ] `CheckAvailability` encuentra un SKU sincronizado
- [ ] no se registran tokens JWT ni API keys en logs

## 11. Troubleshooting

### 11.1 401 Unauthorized desde Ecommerce

Síntoma:

- el Supplier devuelve `502` con detalle de autenticación rechazada;
- el curl directo al Ecommerce devuelve `401`.

Revisar:

- token expirado;
- token mal copiado en `EcommerceInventory__BearerToken`;
- prefijo `Bearer` ausente si haces la prueba manual.

### 11.2 403 Forbidden desde Ecommerce

Síntoma:

- login funciona, pero inventario devuelve `403`.

Revisar:

- el usuario técnico tiene `INVENTORY_READER` o `ADMIN`;
- no uses solo `SUPPLIER_INTEGRATION`.

### 11.3 404 Not Found

Revisar:

- ruta correcta: `/api/v1/inventory/{productCode}` o `/api/v1/inventory`;
- no usar rutas antiguas `/api/v1/inventory/product/{productCode}` ni `/api/v1/inventory/catalog`;
- `EcommerceInventory__ProductLookupPath` y `EcommerceInventory__CatalogSyncPath` no fueron personalizados incorrectamente.

### 11.4 Import vacío o parcial

Revisar:

- `productsImported = 0` indica catálogo vacío o contrato roto;
- `SyncPageSize` debe ser `50`, no `100`;
- el body del catálogo debe traer `data[]`, no `items[]` ni `results[]`.

### 11.5 Mismatch de paginación

Revisar primero:

- `PaginationBaseQuery.PageSize` en Ecommerce;
- claves `pageIndex`, `pageSize`, `pageCount`, `resultByPage`, `count`, `data`;
- si alguien cambió el envelope `PaginationVm<T>`.

## 12. Seguridad operativa

- usa una identidad técnica dedicada y de mínimo privilegio;
- prefiere `INVENTORY_READER` sobre `ADMIN`;
- rota periódicamente `EcommerceInventory__BearerToken`, `Admin__ApiKey` y `MCP` API keys;
- no pegues tokens en tickets, PRs ni logs;
- guarda secretos en el gestor de secretos del entorno;
- revisa que la key administrativa del Supplier no coincida con la key MCP.

## 13. Rollback / disable

Para desactivar rápidamente la dependencia en tiempo real hacia Ecommerce:

1. cambia `EcommerceInventory__Enabled=false`;
2. reinicia el Supplier;
3. conserva el catálogo local ya sincronizado para `GetPrice`, `SearchAlternatives` y fallback de disponibilidad;
4. opcionalmente elimina `EcommerceInventory__BearerToken` del runtime.

Efecto esperado:

- `POST /admin/sync/ecommerce` pasará a devolver `400`;
- `CheckAvailability` dejará de consultar Ecommerce y usará solo catálogo local.

## 14. Contract source (cross-repo)

Archivos a revisar primero en `arlexrush/IgnakeeEcommerce-BackEnd` cuando haya dudas o drift:

- `src/Api/Ecommerce.Api/Controllers/InventoryController.cs`
- `src/Api/Ecommerce.Api/Controllers/UserController.cs`
- `src/Api/Ecommerce.Api/Program.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Models/Authorization/Role.cs`
- `src/Infrastructure/Ecommerce.Infrastructure/Persistence/EcommerceDbContextData.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Features/Auths/Users/Commands/LoginUser/LoginUserCommand.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Features/Auths/Users/Vms/AuthResponse.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Features/Inventory/Queries/Vms/InventoryProductVm.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Features/Inventory/Queries/PaginationInventoryProducts/PaginationInventoryProductsQuery.cs`
- `src/Core/Ecommerce.Application/Ecommerce.Application/Features/Shared/Queries/PaginationBaseQuery.cs`

## 15. Drift guard

Cuando Ecommerce cambie, valida en este orden:

1. `InventoryController` para rutas y `[Authorize]`
2. `InventoryProductVm` para nombres/tipos de campos
3. `PaginationBaseQuery` para límites y nombres de paginación
4. `UserController` + `LoginUserCommand`/`AuthResponse` para emisión del JWT
5. `Role.cs` y seed de roles para confirmar que `INVENTORY_READER` sigue existiendo

Si cualquiera de esos archivos cambia, vuelve a probar el bloque **Day 1 validation script** antes de desplegar.

## 16. Referencias relacionadas en este repo

- [README.md](../README.md)
- [docs/ERP_INTEGRATION.md](./ERP_INTEGRATION.md)
- [docs/TOOL_CONTRACT.md](./TOOL_CONTRACT.md)
