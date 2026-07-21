# DEPLOYMENT — Guía de despliegue y CI/CD

## 1. Objetivo

Este documento describe cómo construir, publicar y desplegar `IgnakeeAI MCP Supplier Server`
en los entornos **Development** y **Production**, incluyendo el flujo completo de CI/CD
automatizado con GitHub Actions y la publicación de imágenes Docker en GHCR.

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
- Variables sensibles gestionadas como **Secrets** de GitHub o variables del sistema.
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
no productivos coordinados para el cliente `legio-development`. Estos valores solo sirven
para pruebas locales y deben sustituirse por secretos externos en producción.

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
| `Admin__ApiKey`              | ambos             | API key administrativa; usar secreto externo en producción |
| `Mcp__ContractVersion`       | ambos             | Versión contractual, por defecto `1.0.0`    |
| `Mcp__ProtocolVersion`       | opcional          | Versión MCP negociada/configurada            |
| `Mcp__Clients__0__ClientId`  | producción       | Identificador del cliente MCP                |
| `Mcp__Clients__0__ApiKey`    | producción       | API key del cliente MCP; secreto externo     |
| `Mcp__Clients__0__Scopes__0` | producción       | Por ejemplo `catalog.read`                  |
| `Mcp__Clients__0__Scopes__1` | producción       | Por ejemplo `availability.read`             |
| `Supplier__Location__Latitude` | ambos           | Latitud operativa del proveedor              |
| `Supplier__Location__Longitude`| ambos           | Longitud operativa del proveedor             |

- **Regla de precedencia en .NET:**
- Variables de entorno > `appsettings.{Environment}.json` > `appsettings.json`

---

## 5. Despliegue en Development (local)

### 5.1 Opción A: .NET CLI (sin Docker)

#### 1. Clonar el repositorio y preparar configuración
git clone <repo-url> 
cd <repo-folder>

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

Las migraciones EF Core se aplican al arrancar la API mediante
`Database.MigrateAsync()`. Para despliegues con migraciones gestionadas fuera de
la aplicación, establecer `Database__ApplyMigrationsOnStartup=false` y ejecutar
la actualización contra PostgreSQL antes de levantar la API.

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

El proyecto tiene dos pipelines definidos en `github/workflows/`:

github/ 
└── workflows/ 
	      ├── ci.yml        ← pipeline de integración continua (CI)    
	      └── release.yml   ← pipeline de publicación de imagen (CD)
	

### 7.1 Pipeline CI — `ci.yml`

**Archivo:** `github/workflows/ci.yml`

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

**Archivo:** `github/workflows/release.yml`

**Cuándo se ejecuta:**
- En cada `push` de un **tag** con prefijo `v` (ejemplos: `v1.0.0`, `v2.3.1`).

**Pasos del pipeline:**

checkout → setup Docker Buildx → login GHCR → extraer versión del tag → build & push imagen multi-arquitectura (amd64 + arm64)

**Imágenes publicadas en GHCR:**
ghcr.io/ignakeeai/mcp-supplier-server:1.0.0   ← versión exacta ghcr.io/ignakeeai/mcp-supplier-server:latest  ← última versión


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
Repositorio → Packages → ghcr.io/ignakeeai/mcp-supplier-server


O desde terminal:
docker pull ghcr.io/ignakeeai/mcp-supplier-server:1.0.0 docker pull ghcr.io/ignakeeai/mcp-supplier-server:latest


---

## 9. Despliegue en Production

### 9.1 Preparar variables de producción

En el servidor de producción, crear un archivo `.env.production` (nunca en repositorio):
ASPNETCORE_ENVIRONMENT=Production DatabaseProvider=postgresql ConnectionStrings__Catalog=Host=<host>;Port=5432;Database=supplier_catalog;Username=supplier_user;Password=<password_segura> POSTGRES_DB=supplier_catalog POSTGRES_USER=supplier_user POSTGRES_PASSWORD=<password_segura>

SUPPLIER_VENDOR_NAME=Mi Empresa S.L. SUPPLIER_CONTACT_EMAIL=ventas@miempresa.com SUPPLIER_CONTACT_PHONE=+34 900 000 000 SUPPLIER_CONTACT_ADDRESS=Calle Real 1, Madrid SUPPLIER_BUSINESS_HOURS=Lun-Vie 08:00-18:00

Solo si se usa ERP:
Erp__Provider=odoo Erp__Odoo__BaseUrl=https://mi-odoo.com Erp__Odoo__Database=mi_empresa Erp__Odoo__Username=api_user Erp__Odoo__Password=<password_erp>


### 9.2 Desplegar con Docker Compose en producción

En el servidor de producción:
1. Descargar la imagen publicada (versión específica recomendada)
docker pull ghcr.io/ignakeeai/mcp-supplier-server:1.0.0

2. Crear docker-compose.production.yml
docker-compose.production.yml services: api: image: ghcr.io/ignakeeai/mcp-supplier-server:1.0.0 restart: always ports: - "5100:5100" env_file: - .env.production depends_on: postgres: condition: service_healthy
postgres: image: postgres:16 restart: always env_file: - .env.production volumes: - supplier_postgres_data:/var/lib/postgresql/data healthcheck: test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"] interval: 10s timeout: 5s retries: 5
volumes: supplier_postgres_data:

3. Iniciar en producción
docker compose -f docker-compose.production.yml up -d

4. Verificar salud
curl http://localhost:5100/health curl http://localhost:5100/


### 9.3 Actualizar a una nueva versión en producción
1. Descargar nueva imagen
docker pull ghcr.io/ignakeeai/mcp-supplier-server:1.1.0

2. Actualizar la imagen en docker-compose.production.yml
image: ghcr.io/ignakeeai/mcp-supplier-server:1.1.0

3. Recrear el contenedor (sin downtime en bases de datos)
docker compose -f docker-compose.production.yml up -d --no-deps api

4. Verificar
curl http://localhost:5100/health


---

## 10. Migraciones de base de datos

Las migraciones se aplican **automáticamente** al iniciar la API cuando la clave de
configuración `Database:ApplyMigrationsOnStartup` está en `true` (valor por defecto).

{ "Database": { "ApplyMigrationsOnStartup": true } }


**Comportamiento:**
- Al arrancar el contenedor, `Program.cs` llama a `db.Database.MigrateAsync()`.
- Si la base de datos no existe (primera vez), se crea y se aplican todas las migraciones.
- Si ya existe, solo se aplican las migraciones pendientes.

**Para desactivar migraciones automáticas** (útil si se gestionan manualmente):

{ "Database": { "ApplyMigrationsOnStartup": false } }


Ejecutar migraciones manualmente:
dotnet ef database update 
--project src/IgnakeeAI.McpServer.Supplier.Infrastructure 
--startup-project src/IgnakeeAI.McpServer.Supplier.Api


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

6. Verificar tool MCP
curl -X POST http://localhost:5100/mcp 
-H "X-Api-Key: <mcp-secret>" 
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
		  └─► ✅ verde = puede mergear   │ 
		  ├─► merge a main │       
		  └─► CI: restore → build → test (confirmación final) │ 
		  └─► git tag v1.x.x + git push origin v1.x.x 
		  └─► CD: buildx → build imagen multi-arch 
		  └─► push a GHCR 
		  ├─► ghcr.io/.../mcp-supplier-server:1.x.x 
		  └─► ghcr.io/.../mcp-supplier-server:latest 
		  └─► despliegue manual en servidor de producción 
		  └─► docker compose pull + up -d


---

## 14. Seguridad en despliegue

- **Nunca** incluir credenciales reales en `appsettings.json` ni en el repositorio.
- Usar `.env.production` fuera del repositorio o un gestor de secretos (Vault, Azure Key Vault).
- Proteger los endpoints `/admin/*` con autenticación antes de exponer a internet.
- Restringir CORS en producción mediante `Cors:AllowedOrigins`.
- Activar TLS mediante Nginx/Traefik como reverse proxy delante del puerto `5100`.
- Aplicar rate limiting sobre `/mcp` y `/admin/*` en producción.

---

## 15. Troubleshooting de despliegue

| Síntoma                              | Causa probable                                               | Solución                                              |
|--------------------------------------|--------------------------------------------------------------|-------------------------------------------------------|
| `connection refused` en puerto 5100  | Contenedor no iniciado o puerto no mapeado                   | `docker compose ps` + revisar `ports`                 |
| `GET /health` devuelve error         | Fallo de migración al arrancar                               | Revisar logs: `docker compose logs api`               |
| `productsSynced = 0` tras sync ERP   | Credenciales ERP incorrectas o proveedor vacío               | Revisar `Erp__Provider` y credenciales                |
| Error en CI `dotnet build`           | Código no compila                                            | Revisar errores en GitHub Actions → CI                |
| Imagen no aparece en GHCR            | Tag no tiene prefijo `v`                                     | Usar `git tag v1.0.0` (con `v` minúscula)             |
| BD no se crea en PostgreSQL          | PostgreSQL no healthy al arrancar                            | Verificar `healthcheck` y `depends_on`                |
| Variables de entorno no aplicadas    | Fichero `.env` no cargado                                    | Pasar `--env-file .env` o usar `env_file:` en compose |

