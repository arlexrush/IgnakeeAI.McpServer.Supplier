# TOOL CONTRACT — IgnakeeAI MCP Supplier Server

## 1) Objetivo

Este documento define el contrato funcional y de compatibilidad de las herramientas MCP expuestas por el servidor de proveedor.

- Endpoint MCP: `/mcp`
- Runtime: `.NET 8`
- Formato de salida de tools: `string` con JSON serializado en `camelCase`

Este contrato es la referencia para:
- consumidores MCP,
- pruebas automatizadas,
- evolución compatible del servidor.

---

## 2) Versionado del contrato

- **ContractVersion**: `1.0.0`
- Cambios **breaking** requieren incremento de versión mayor.
- Cambios aditivos (nuevos campos opcionales, nuevas tools) incrementan menor/parche.
- No se deben renombrar ni eliminar parámetros/campos existentes dentro de la misma major.

---

## 3) Convenciones globales

- Moneda por defecto: `EUR`.
- Criterios válidos de sustitución:
  - `cheaper`
  - `better`
  - `onSale`
  - `optimalPack`
  - `any`
- Cuando no hay resultados, la respuesta debe incluir `found: false` (o equivalente) y no lanzar error funcional.
- Errores técnicos (infraestructura, DB, ERP, serialización) se reportan como error de ejecución MCP.

---

## 4) Tools expuestas

## 4.1 `getPrice`

Obtiene precio de un material/recurso por código exacto o descripción.

### Parámetros
- `itemDescription` (`string`, requerido)
- `itemCode` (`string?`, opcional, default `null`)
- `currency` (`string`, opcional, default `"EUR"`)

### Respuesta esperada (JSON serializado a string)
Campos esperados:
- `found` (`boolean`)
- `itemCode` (`string?`)
- `description` (`string?`)
- `unitPrice` (`number?`)
- `currency` (`string?`)
- `unit` (`string?`)
- `packSize` (`number?`)
- `packPrice` (`number?`)
- `validUntil` (`string?`, fecha ISO)
- `contactEmail` (`string?`)
- `contactPhone` (`string?`)
- `contactAddress` (`string?`)
- `vendorName` (`string?`)

---

## 4.2 `searchAlternatives`

Busca productos alternativos/sustitutos.

### Parámetros
- `itemDescription` (`string`, requerido)
- `category` (`string?`, opcional, default `null`)
- `criteria` (`string`, opcional, default `"any"`)
- `requiredQuantity` (`decimal?`, opcional, default `null`)
- `maxResults` (`int`, opcional, default `5`)
- `currency` (`string`, opcional, default `"EUR"`)

### Respuesta esperada (JSON serializado a string)

{ 
  "found": true, 
  "count": 2, 
  "alternatives": [ { "itemCode": "ABC-001", 
					  "description": "Cemento 25kg", 
					  "unitPrice": 5.5, 
					  "originalPrice": 6.0, 
					  "currency": "EUR", 
					  "unit": "saco", 
					  "packSize": 1, 
					  "packPrice": 5.5, 
					  "specification": "CEM II", 
					  "presentation": "25kg", 
					  "qualityRating": 4, 
					  "isOnSale": true, 
					  "availableStock": 120, 
					  "leadTimeDays": 1, 
					  "url": "https://...", 
					  "reason": "Más barato para mismo uso" } 
				   ] 
}

---

## 4.3 `checkAvailability`

Consulta stock y plazo estimado de entrega.

### Parámetros
- `itemCode` (`string`, requerido)

### Respuesta esperada (JSON serializado a string)
Campos esperados:
- `found` (`boolean`)
- `itemCode` (`string?`)
- `availableStock` (`number?`)
- `leadTimeDays` (`number?`)
- `message` (`string?`)

---

## 4.4 `getBusinessHours`

Devuelve datos de atención del proveedor.

### Parámetros
- Sin parámetros.

### Respuesta esperada (JSON serializado a string)

{ 
  "hours": "Lun-Vie 08:00-18:00", 
  "vendorName": "Proveedor IgnakeeAI", 
  "contactEmail": "ventas@proveedor.com", 
  "contactPhone": "+34 900 000 000", 
  "contactAddress": "Calle Ejemplo 123, Madrid" 
}


---

## 5) Errores y comportamiento

- **Errores funcionales** (ej. producto no encontrado): respuesta normal con `found: false`.
- **Errores técnicos**: error MCP con mensaje trazable en logs.
- No se deben exponer secretos (credenciales ERP, connection strings) en respuestas de tools.

---

## 6) Compatibilidad y pruebas

Cada cambio de contrato debe:
1. actualizar este documento,
2. actualizar tests de tools (`PricingToolsTests`, `AlternativeSearchTests`, etc.),
3. mantener compatibilidad hacia atrás dentro de la misma versión major.

---

## 7) Historial del contrato

- `1.0.0` — contrato inicial para:
  - `getPrice`
  - `searchAlternatives`
  - `checkAvailability`
  - `getBusinessHours`
 
