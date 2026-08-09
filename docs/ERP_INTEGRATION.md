# ERP_INTEGRATION — Manual de integración de catálogo

## 1. Objetivo

Este documento está dirigido al equipo técnico del proveedor. Describe cómo alimentar el
catálogo que consultarán los agentes de Legio desde:

- ERP: `Odoo` o `SAP`
- Ficheros: `CSV` o `Excel`

La sincronización alimenta el catálogo local usado por las tools MCP (`GetPrice`, `SearchAlternatives`, `CheckAvailability`).

---

## 2. Alcance y comportamiento

- La carga se realiza por **upsert por `ItemCode`**:
  - si el producto existe: se actualiza
  - si no existe: se crea
- La fuente de verdad para consultas MCP es la base de datos local.
- Endpoints administrativos:
  - `POST /admin/sync/erp`
  - `POST /admin/sync/csv`
  - `POST /admin/sync/excel`
  - `GET /admin/catalog/stats`

---

## 3. Prerrequisitos

1. API en ejecución.
2. Base de datos configurada (`sqlite`, `postgresql`, `sqlserver`, `mysql`).
3. `appsettings.json` o `appsettings.Development.json` con bloque `Erp`.
4. Conectividad de red hacia ERP (si aplica).

---

## 4. Configuración base
{ 
	"Erp": { 
				"Provider": "", 
				"Odoo": { 
							"BaseUrl": "", 
							"Database": "", 
							"Username": "", 
							"Password": "" 
						}, 
							"Sap": { 
										"BaseUrl": "", 
										"Database": "", 
										"Username": "", 
										"Password": "" 
									} 
						} 
			}
}


Regla clave:
- `Erp:Provider = "odoo"` activa `OdooConnector`
- `Erp:Provider = "sap"` activa `SapConnector`
- vacío: no hay conector ERP registrado

---

## 5. Integración 1: Odoo (JSON-RPC)

## 5.1 Cuándo usarla
Cuando el catálogo maestro está en Odoo (`product.product`).

## 5.2 Configuración
{ 
	"Erp": { 
				"Provider": "odoo", 
				"Odoo": { 
							"BaseUrl": "https://mi-odoo.com", 
							"Database": "mi_empresa", 
							"Username": "api_user", 
							"Password": "api_password" 
						} 
			} 
}


## 5.3 Flujo técnico
1. Autenticación en `/jsonrpc` (`common.authenticate`)
2. Lectura `product.product/search_read`
3. Mapeo a `CatalogProduct`
4. Upsert local + `SaveChanges`

## 5.4 Campos relevantes mapeados
- `default_code` → `ItemCode`
- `name` → `Description`
- `categ_id` → `Category`
- `list_price` → `UnitPrice`
- `uom_id` → `Unit`
- `qty_available` → `AvailableStock`
- `description_sale` → `Keywords`

## 5.5 Ejecutar sincronización
- curl -X POST http://localhost:5100/admin/sync/erp


Respuesta esperada (ejemplo):

{ 
	"erp": "Odoo", 
	"productsSynced": 2480, 
	"syncedAt": "2026-03-04T10:35:12.000Z" 
}


## 5.6 Validación
1. `GET /admin/catalog/stats`
2. Consultar tool `GetPrice` con un `itemCode` conocido de Odoo

---

## 6. Integración 2: SAP (Service Layer / OData)

## 6.1 Cuándo usarla
Cuando el maestro de artículos reside en SAP Business One o S/4 expuesto por Service Layer/OData.

## 6.2 Configuración

{ 
	"Erp": { 
				"Provider": "sap", 
				"Sap": { 
							"BaseUrl": "https://mi-sap:50000/b1s/v1", 
							"Database": "MI_EMPRESA", 
							"Username": "manager", 
							"Password": "password" 
						} 
			} 
}


## 6.3 Flujo técnico
1. `POST /Login`
2. Lectura paginada de `Items` con filtro `SalesItem eq 'tYES'`
3. Mapeo a `CatalogProduct`
4. Upsert local
5. `POST /Logout`

## 6.4 Campos relevantes mapeados
- `ItemCode` → `ItemCode`
- `ItemName` → `Description`
- `ItemsGroupCode` → `Category` (`sap-group-{id}`)
- `AvgStdPrice` → `UnitPrice`
- `SalesUnit` → `Unit`
- `QuantityOnStock` → `AvailableStock`

## 6.5 Ejecutar sincronización
- curl -X POST http://localhost:5100/admin/sync/erp


Ejemplo de respuesta:

{ 
	"erp": "SAP", 
	"productsSynced": 1790, 
	"syncedAt": "2026-03-04T10:41:05.000Z" 
}


---

## 7. Integración 3: CSV (carga manual/automatizada)

## 7.1 Formato esperado
Separador `;` y cabeceras:
`ItemCode;Description;Category;Keywords;Unit;UnitPrice;Currency;PackSize;PackPrice;Specification;Presentation;AvailableStock;LeadTimeDays;ProductUrl;IsOnSale;SalePrice;QualityRating`

## 7.2 Ejemplo mínimo CSV

ItemCode;
Description;
Category;
Keywords;
Unit;
UnitPrice;
Currency;
PackSize;
PackPrice;
Specification;
Presentation;
AvailableStock;
LeadTimeDays;
ProductUrl;
IsOnSale;
SalePrice;
QualityRating CEM-001;
Cemento gris 25kg;
cemento;
cemento obra;
cubo;
6.25;
EUR;
1;
6.25;
CEM II;
Saco 25kg;
120;
1;
https://proveedor.local/cem-001;
true;
5.90;
4;


## 7.3 Ejecutar importación
- curl -X POST http://localhost:5100/admin/sync/csv -F "file=@catalogo.csv"


---

## 8. Integración 4: Excel (.xlsx)

## 8.1 Formato esperado
Hoja `Catalogo` (o primera hoja), columnas:

A `ItemCode`, B `Description`, C `Category`, D `Keywords`, E `Unit`, F `UnitPrice`, G `Currency`, H `PackSize`, I `PackPrice`, J `Specification`, K `Presentation`, L `AvailableStock`, M `LeadTimeDays`, N `ProductUrl`, O `IsOnSale`, P `SalePrice`, Q `QualityRating`.

## 8.2 Ejecutar importación

- curl -X POST http://localhost:5100/admin/sync/excel -F "file=@catalogo.xlsx"


---

## 9. Verificación post-integración (checklist)

1. `GET /health` devuelve estado saludable.
2. `GET /admin/catalog/stats` muestra productos > 0.
3. Tool `GetPrice` encuentra códigos recién sincronizados.
4. Tool `SearchAlternatives` devuelve resultados por categoría/coste.
5. Logs sin errores de autenticación ni mapeo masivo.

---

## 10. Troubleshooting rápido

## 10.1 `No hay conector ERP configurado`
- Revisar `Erp:Provider` (`odoo` o `sap`).

## 10.2 Error de autenticación Odoo/SAP
- Verificar URL, base de datos, usuario, contraseña.
- Probar credenciales fuera de la API.

## 10.3 `productsSynced = 0`
- Confirmar filtros de origen (productos activos/vendibles).
- Verificar que `ItemCode` no llegue vacío.

## 10.4 Error al subir CSV/Excel
- Confirmar `form-data` con key `file`.
- Revisar formato de columnas y tipos.

---

## 11. Seguridad y operación

- No guardar credenciales reales en repositorio.
- Usar secretos/variables de entorno en despliegues.
- Separar siempre la credencial administrativa de las credenciales MCP de Legio:
  - `ADMIN_API_KEY` para `/admin/*`.
  - `MCP_API_KEY` para el cliente MCP.
  - `MCP_CLIENT_ID` identifica la integración de Legio.
  - `Mcp__Clients__0__Scopes__0=catalog.read`.
  - `Mcp__Clients__0__Scopes__1=availability.read`.
- No configurar la misma API key en `ADMIN_API_KEY` y `MCP_API_KEY`.
- Tras una sincronización, validar el catálogo con `GetPrice`, `SearchAlternatives` y
  `CheckAvailability` desde Legio o con una petición MCP de prueba.
- Proteger endpoints `/admin/*` antes de producción.
- Limitar CORS y habilitar TLS en entorno productivo.

---

## 12. Recomendación de uso

- ERP (Odoo/SAP): sincronización programada diaria o por ventana de negocio.
- CSV/Excel: cargas puntuales para catálogos pequeños o contingencia.
- Tras cada sync, ejecutar validación funcional con tools MCP.
