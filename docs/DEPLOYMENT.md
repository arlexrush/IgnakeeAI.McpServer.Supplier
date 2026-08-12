# DEPLOYMENT — Guía de despliegue y CI/CD

## 1. Objetivo

Este documento guía a un proveedor desde la instalación hasta la integración productiva
con los agentes de **Legio**, el cliente MCP. Incluye el flujo de CI/CD y la publicación
de la imagen Docker en GHCR.

---

## 2. Herramientas necesarias

| Herramienta                                         | Versión mínima         | Propósito                            |
|-----------------------------------------------------|------------------------|--------------------------------------|
| [.NET SDK](https://dotnet.microsoft.com/download)   | 8.0.x                  | Compilar, testear y publicar         |
| [Docker](https://docs.docker.com/get-docker/)       | 24+                    | Construir y ejecutar la imagen       |
| [Docker Compose](https://docs.docker.com/compose/)  | 2.x                    | Orquestar servicios locales          |
| [Git](https://git-scm.com/)                         | cualquier              | Control de versiones y trigger CI/CD |
| GitHub Actions                                      | —                      | Pipeline de CI/CD automatizado       |
| GHCR (GitHub Container Registry)                    | —                      | Registro de imágenes Docker          |

---

## 3. Entornos

### 3.1 Development

- Uso de SQLite como base de datos (sin servidor externo).
- Variables de entorno cargadas desde `.env` o `appsettings.Development.json`.
- Puerto de escucha: `5100`.
- Base de datos: `catalog.dev.db` (local, en disco).
- Ideal para desarrollo y validación local antes de promover a producción.

### 3.2 Production

- Base de datos recomendada: PostgreSQL (o SQL Server según infraestructura).
- Variables sensibles gestionadas como secretos externos o variables del sistema.
- Imagen publicada en GHCR y desplegada como contenedor.
- Puerto de escucha: `5100` (mapeado desde host según entorno).
- TLS/HTTPS gestionado por Ingress o Reverse Proxy externo (Nginx, Traefik, etc.).

---

## 4. Estructura de configuración por entorno

### 4.1 Ficheros de configuración
- src/IgnakeeAI.McpServer.Supplier.Api/     ├── appsettings.json                  
- ← configuración base (SQLite por defecto) ├── appsettings.Development.json      
- ← sobreescribe en desarrollo local        └── appsettings.Production.json       
- ← (crear) sobreescribe en producción


- En contenedor, la configuración se inyecta principalmente mediante **variables de entorno**,
  que tienen mayor prioridad que los ficheros `appsettings.json`.

En desarrollo local, `appsettings.Development.json` y `.env.example` contienen valores
de prueba para una integración Legio. Deben sustituirse por secretos externos en producción.

### 4.2 Variables de entorno clave

Copiar `.env.example` como `.env` y ajustar los valores:

cp .env.example .env


| Variable                     | Entorno          |Descripción                                   |
|------------------------------|------------------|----------------------------------------------|
| `ASPNETCORE_ENVIRONMENT`     | ambos            | `Development` o `Production`                 |
| `DatabaseProvider`           | ambos            | `sqlite`, `postgresql`, `sqlserver`, `mysql` |
| `ConnectionStrings__Catalog` | ambos            | Cadena de conexión a la BD                   |
| `POSTGRES_DB`                | producción       | Nombre de la base de datos PostgreSQL        |
| `POSTGRES_USER`              | producción       | Usuario PostgreSQL                           |
| `POSTGRES_PASSWORD`          | producción       | Contraseña PostgreSQL (**secreto**)          |
| `Erp__Provider`              | opcional         | `odoo`, `sap` o vacío                        |
| `Erp__Odoo__BaseUrl`         | si Odoo          | URL base de Odoo                             |
| `Erp__Odoo__Database`        | si Odoo          | Base de datos Odoo                           |
| `Erp__Odoo__Username`        | si Odoo          | Usuario Odoo                                 |
| `Erp__Odoo__Password`        | si Odoo          | Contraseña Odoo (**secreto**)                |
| `Erp__Sap__BaseUrl`          | si SAP           | URL base del Service Layer SAP               |
| `Erp__Sap__Database`         | si SAP           | Base de datos SAP                            |
| `Erp__Sap__Username`         | si SAP           | Usuario SAP                                  |
| `Erp__Sap__Password`         | si SAP           | Contraseña SAP (**secreto**)                 |
| `SUPPLIER_VENDOR_NAME`       | ambos            | Nombre del proveedor                         |
| `SUPPLIER_CONTACT_EMAIL`     | ambos            | Email de contacto                            |
| `SUPPLIER_CONTACT_PHONE`     | ambos            | Teléfono de contacto                         |
| `SUPPLIER_CONTACT_ADDRESS`   | ambos            | Dirección del proveedor                      |
| `SUPPLIER_BUSINESS_HOURS`    | ambos            | Horario de atención                          |
| `ADMIN_API_KEY`              | ambos             | API key del proveedor para `/admin/*`; secreto externo en producción |
| `MCP_CLIENT_ID`              | ambos             | Identificador del cliente MCP Legio |
| `MCP_API_KEY`                | ambos             | API key que Legio enviará en `X-Api-Key` |
| `Mcp__ContractVersion`       | ambos             | Versión contractual, por defecto `1.0.0`    |
| `Mcp__ProtocolVersion`       | opcional          | Versión MCP negociada/configurada            |
| `Mcp__Clients__0__Scopes__0` | producción       | Scope MCP configurado (`catalog.read`)      |
| `Mcp__Clients__0__Scopes__1` | producción       | Scope MCP configurado (`availability.read`) |
| `Supplier__Location__Latitude` | ambos           | Latitud operativa del proveedor              |
| `Supplier__Location__Longitude`| ambos           | Longitud operativa del proveedor             |

- **Regla de precedencia en .NET:**
- Variables de entorno > `appsettings.{Environment}.json` > `appsettings.json`

---

## 5. Despliegue en Development (local)

### 5.1 Opción A: .NET CLI (sin Docker)

#### 1. Clonar el repositorio y preparar configuración
git clone https://github.com/arlexrush/IgnakeeAI.McpServer.Supplier.git
cd IgnakeeAI.McpServer.Supplier

#### 2. Copiar configuración de ejemplo
cp .env.example .env

#### 3. Restaurar dependencias
dotnet restore IgnakeeAI.McpServer.Supplier.sln

#### 4. Construir la solución
dotnet build IgnakeeAI.McpServer.Supplier.sln -c Release

#### 5. Ejecutar pruebas
dotnet test IgnakeeAI.McpServer.Supplier.sln -c Release --no-build --verbosity normal

#### 6. Ejecutar la aplicación
dotnet run --project src/IgnakeeAI.McpServer.Supplier.Api --launch-profile Development

La API queda disponible en: `http://localhost:5100`

Verificar:

curl http://localhost:5100/health curl http://localhost:5100/


### 5.2 Opción B: Docker Compose con SQLite (recomendado para desarrollo)
docker compose -f docker-compose.yml -f docker-compose.sqlite.yml up --build -d

#### Verificar que el contenedor está corriendo
docker compose ps

#### Verificar logs en tiempo real
docker compose logs -f ignakeeai.mcpserver.supplier.api

#### Activar el perfil sqlite-ui
Acceder a la interfaz visual de SQLite (si el profile está activado):
docker compose -f docker-compose.yml -f docker-compose.sqlite.yml --profile sqlite-ui up -d

Abrir en navegador: http://localhost:8081


### 5.3 Opción C: Docker Compose con PostgreSQL

En producción, la API exige `DatabaseProvider=postgresql` (o `postgres`) y no
permite iniciar accidentalmente con SQLite. El compose de PostgreSQL establece
también `ASPNETCORE_ENVIRONMENT=Production`; la cadena
`ConnectionStrings__Catalog` se construye a partir de las variables del fichero
`.env`.

#### Configurar credenciales en .env
Configurar credenciales en .env:
- POSTGRES_DB=supplier_catalog
- POSTGRES_USER=supplier_user
- POSTGRES_PASSWORD=change_me

#### Construir y levantar servicios
- docker compose -f docker-compose.yml -f docker-compose.override.yml up --build -d

Aplica las migraciones de forma controlada antes de levantar o escalar la API:

```powershell
$env:ConnectionStrings__Catalog = 'Host=postgres;Port=5432;Database=...;Username=...;Password=...'
./scripts/migrate-production.ps1
```

La API no ejecuta migraciones al arrancar.

El `healthcheck` de PostgreSQL usa los mismos valores por defecto que el servicio
(`supplier_catalog` y `supplier_user`) y acepta los valores personalizados de
`.env`; así `depends_on` no libera la API hasta que PostgreSQL esté listo.

### 5.4 Producción con el compose endurecido

El fichero `docker-compose.production.yml` no contiene secretos y exige que se
definan antes de arrancar:

```powershell
Copy-Item .env.example .env.production
notepad .env.production
docker compose --env-file .env.production -f docker-compose.production.yml config
docker compose --env-file .env.production -f docker-compose.production.yml up -d
```

En `.env.production` se deben sustituir, como mínimo, `POSTGRES_PASSWORD`,
`ADMIN_API_KEY`, `MCP_CLIENT_ID`, `MCP_API_KEY` y los datos reales del proveedor.
El fichero debe permanecer fuera de Git. El comando `config` valida la
interpolación sin iniciar contenedores.

#### Verificar salud del servicio
- docker compose ps
- curl http://localhost:5100/health


---

## 6. Construir la imagen Docker manualmente

### Construir imagen etiquetada como versión local
docker build 
-f src/IgnakeeAI.McpServer.Supplier.Api/Dockerfile 
-t ignakeeai/mcp-supplier-server:local 

### Ejecutar el contenedor directamente
docker run -d 
--name mcp-supplier-local 
-p 5100:5100 
-e ASPNETCORE_ENVIRONMENT=Development 
-e DatabaseProvider=sqlite 
-e "ConnectionStrings__Catalog=Data Source=/app/data/catalog.db" 
-v mcp_data:/app/data 
ignakeeai/mcp-supplier-server:local


---

## 7. Flujo de CI/CD con GitHub Actions

El proyecto tiene dos pipelines definidos en `.github/workflows/`:

.github/ 
└── workflows/ 
	      ├── ci.yml        ← pipeline de integración continua (CI)    
	      └── release.yml   ← pipeline de publicación de imagen (CD)
	

### 7.1 Pipeline CI — `ci.yml`

**Archivo:** `.github/workflows/ci.yml`

**Cuándo se ejecuta:**
- En cada `push` a las ramas `main` o `develop`.
- En cada `pull_request` hacia `main`.

**Pasos del pipeline:**

checkout → setup .NET 8 → dotnet restore → dotnet build -c Release → dotnet test


**¿Qué valida?**
1. Que el código compila sin errores en modo `Release`.
2. Que todos los tests pasan (`PricingToolsTests`, `AlternativeSearchTests`, `OdooConnectorTests`).

**Ejemplo de ejecución local equivalente:**

- dotnet restore IgnakeeAI.McpServer.Supplier.sln dotnet build IgnakeeAI.McpServer.Supplier.sln -c Release --no-restore dotnet test IgnakeeAI.McpServer.Supplier.sln -c Release --no-build --verbosity normal


> El CI actúa como **puerta de calidad**: si falla, el merge o release queda bloqueado.

### 7.2 Pipeline CD — `release.yml`

**Archivo:** `.github/workflows/release.yml`

**Cuándo se ejecuta:**
- En cada `push` de un **tag** con prefijo `v` (ejemplos: `v1.0.0`, `v2.3.1`).

**Pasos del pipeline:**

checkout → setup Docker Buildx → login GHCR → extraer versión del tag → build & push imagen multi-arquitectura (amd64 + arm64)

**Imágenes publicadas en GHCR:**
ghcr.io/arlexrush/mcp-supplier-server:1.0.0   ← versión exacta
ghcr.io/arlexrush/mcp-supplier-server:latest  ← última versión


**Autenticación con GHCR:**
- Se usa `GITHUB_TOKEN` (secreto automático de GitHub Actions, sin configuración manual).

---

## 8. Crear y publicar un release (paso a paso)

### Paso 1 — Asegurarse de que CI está en verde

Verificar estado en GitHub: Actions → CI - Build & Test → último run = success


### Paso 2 — Crear un tag de versión

Desde la rama main (asegurarse de estar actualizado)
git checkout main git pull origin main

Crear tag de versión semántica
git tag v1.0.0

Publicar el tag (esto dispara el pipeline release.yml)
git push origin v1.0.0


### Paso 3 — Verificar el pipeline de release

En GitHub:
Repositorio → Actions → Release - Docker Image → verificar que completa sin errores


### Paso 4 — Verificar la imagen publicada
Repositorio → Packages → `mcp-supplier-server`


O desde terminal:
docker pull ghcr.io/arlexrush/mcp-supplier-server:1.0.0
docker pull ghcr.io/arlexrush/mcp-supplier-server:latest


---

## 9. Despliegue en producción para un proveedor

### 9.1 Preparar el servidor

El servidor de producción necesita Docker Engine/Desktop, Docker Compose 2.x y acceso de lectura al paquete privado de GHCR. Autentica el registro con una cuenta o token que tenga `read:packages`:

```powershell
docker login ghcr.io -u arlexrush
```

No guardes el token en `.env.production` ni lo compartas con Legio.

### 9.2 Preparar `.env.production`

Crea el fichero fuera del repositorio:

```powershell
Copy-Item .env.example .env.production
notepad .env.production
```

Configura como mínimo:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
DatabaseProvider=postgresql
POSTGRES_DB=supplier_catalog
POSTGRES_USER=supplier_user
POSTGRES_PASSWORD=<secreto-postgres>

SUPPLIER_VENDOR_NAME=Mi Empresa S.L.
SUPPLIER_CONTACT_EMAIL=ventas@miempresa.com
SUPPLIER_CONTACT_PHONE=+34 900 000 000
SUPPLIER_CONTACT_ADDRESS=Calle Real 1, Madrid
SUPPLIER_BUSINESS_HOURS=Lun-Vie 08:00-18:00

ADMIN_API_KEY=<clave-solo-para-el-proveedor>
MCP_CLIENT_ID=legio-production
MCP_API_KEY=<clave-que-se-entregara-a-legio>
Mcp__ContractVersion=1.0.0
Mcp__ProtocolVersion=2025-03-26
```

Si se utiliza un ERP, añade `Erp__Provider` y las credenciales correspondientes. El fichero debe permanecer fuera de Git.

### 9.3 Descargar y levantar la imagen

La imagen publicada por GitHub Actions es:

```text
ghcr.io/arlexrush/mcp-supplier-server:<version>
ghcr.io/arlexrush/mcp-supplier-server:latest
```

Valida primero la interpolación y después inicia los servicios:

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml config
docker compose --env-file .env.production -f docker-compose.production.yml pull
docker compose --env-file .env.production -f docker-compose.production.yml up -d
```

El Compose de producción utiliza PostgreSQL y espera a que su healthcheck esté listo antes de iniciar la API. La API escucha en el puerto `5100`; en una instalación pública debe quedar detrás de HTTPS mediante un reverse proxy.

### 9.4 Verificación y conexión de Legio

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml ps
Invoke-WebRequest http://localhost:5100/health
Invoke-WebRequest http://localhost:5100/
```

El proveedor entrega a Legio únicamente:

- `https://dominio-del-proveedor.example/mcp`;
- `MCP_CLIENT_ID`;
- `MCP_API_KEY`;
- protocolo `2025-03-26` y contrato `1.0.0`;
- scopes `catalog.read` y `availability.read`.

No entregue a Legio `ADMIN_API_KEY`, contraseñas PostgreSQL ni credenciales ERP.

### 9.5 Actualizar una versión

El pipeline publica una imagen versionada y `latest` cuando se envía un tag `v*`. Para actualizar de forma controlada, use la etiqueta versionada:

```powershell
docker pull ghcr.io/arlexrush/mcp-supplier-server:1.0.1
# Actualizar image en docker-compose.production.yml a :1.0.1
docker compose --env-file .env.production -f docker-compose.production.yml up -d --no-deps api
```

Comprueba `/health` y las tools MCP antes de comunicar la actualización a Legio.


---

## 10. Migraciones de base de datos

Las migraciones se ejecutan explícitamente antes del despliegue, con una sola
instancia y usando la misma cadena de conexión que producción:

```powershell
$env:ConnectionStrings__Catalog = 'Host=postgres;Port=5432;Database=...;Username=...;Password=...'
./scripts/migrate-production.ps1
```

No inicies ni escales la API hasta que este paso termine correctamente. La
aplicación no aplica migraciones al arrancar.

---

## 11. Verificación post-despliegue (checklist)

Ejecutar tras cualquier despliegue en ambos entornos:

1. Salud general
curl http://localhost:5100/health
Esperado: HTTP 200, "Healthy"

2. Metadata del servidor
curl http://localhost:5100/
Esperado: JSON con server, version, contractVersion, mcpEndpoint, healthEndpoint y tools

3. Estadísticas del catálogo
curl http://localhost:5100/admin/catalog/stats
Esperado: JSON con conteo de productos

4. Sincronización ERP (si aplica)
curl -X POST http://localhost:5100/admin/sync/erp
Esperado: { "erp": "Odoo"|"SAP", "productsSynced": N, "syncedAt": "..." }

5. Importación CSV de prueba
curl -X POST http://localhost:5100/admin/sync/csv 
-F "file=@seed/seed-catalog.csv"

6. Verificar tool MCP con la clave de `MCP_API_KEY`
curl -X POST http://localhost:5100/mcp 
-H "X-Api-Key: <MCP_API_KEY>" 
-H "Accept: application/json, text/event-stream" 
-H "Content-Type: application/json" 
-d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"GetPrice","arguments":{"itemDescription":"cemento","currency":"EUR"}}}'

7. Logs y estado de contenedores
docker compose --env-file .env.production -f docker-compose.production.yml ps
docker compose --env-file .env.production -f docker-compose.production.yml logs --tail=100 api postgres


---

## 12. Secrets de GitHub Actions (configuración requerida)

Para que el pipeline `release.yml` funcione, no se requiere configuración adicional:
`GITHUB_TOKEN` es un secreto automático provisto por GitHub Actions.

Para pipelines más avanzados (ej. despliegue automático al servidor de producción),
se pueden añadir secrets adicionales en:

Repositorio GitHub → Settings → Secrets and variables → Actions → New repository secret


Ejemplos de secrets adicionales:
| Secret                   | Descripción                                            |
|--------------------------|--------------------------------------------------------|
| `PROD_SSH_KEY`           | Clave SSH para acceso al servidor de producción        |
| `PROD_HOST`              | IP o hostname del servidor de producción               |
| `PROD_POSTGRES_PASSWORD` | Contraseña de PostgreSQL en producción                 |
| `ERP_ODOO_PASSWORD`      | Contraseña del usuario ERP Odoo                        |

---

## 13. Resumen del flujo CI/CD completo

Developer │ 
          ├─► push a develop / PR a main │       
		  └─► CI: restore → build → test │                  

