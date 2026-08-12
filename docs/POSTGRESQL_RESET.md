# PostgreSQL — cambiar credenciales o reinicializar la base de datos

Esta guía explica cómo cambiar `POSTGRES_USER`, `POSTGRES_PASSWORD` y `POSTGRES_DB` cuando PostgreSQL ya se ha iniciado una vez.

## Antes de empezar

La imagen oficial de PostgreSQL usa las variables `POSTGRES_*` **solamente durante la primera inicialización** del volumen `supplier_postgres_data`.

Por eso, editar el fichero `.env` y reiniciar los contenedores **no cambia** el usuario, la contraseña ni la base de datos que ya existen dentro de PostgreSQL.

Para principiantes, el método recomendado es recrear el volumen de PostgreSQL. Este método permite cambiar las tres variables a la vez, pero borra todos los datos del entorno seleccionado.

Nunca ejecutes comandos de Staging contra Production. Staging y Production usan servidores Hetzner diferentes.

## Qué método elegir

| Situación                                           | Método recomendado                                                                                    |
|-----------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| Local y no necesitas conservar datos                | Reinicialización local                                                                                |
| Staging y no necesitas conservar datos              | Reinicialización de Staging                                                                           |
| Production y no necesitas conservar datos           | Reinicialización de Production, durante una ventana de mantenimiento                                  |
| Debes conservar datos existentes                    | Haz una copia de seguridad y solicita o sigue un procedimiento de restauración antes de reinicializar |
| Sólo quieres cambiar la contraseña sin borrar datos | Rotación de contraseña sin reinicialización                                                           |

## Valores que debes preparar

Decide valores diferentes para cada entorno. Ejemplo:

| Variable            | Local                    | Staging                   | Production                    |
|---------------------|--------------------------|---------------------------|-------------------------------|
| `POSTGRES_DB`       | `supplier_catalog_local` | `supplier_catalog_staged` | `supplier_catalog_production` |
| `POSTGRES_USER`     | `supplier_local`         | `supplier_staged`         | `supplier_production`         |
| `POSTGRES_PASSWORD` | Contraseña única         | Contraseña única          | Contraseña única              |

Usa un gestor de contraseñas para generar `POSTGRES_PASSWORD`. No reutilices contraseñas entre entornos ni guardes los ficheros privados `.env`, `.env.staged` o `.env.production` en Git.

## A. Reinicialización local

Usa esta sección sólo si ejecutas PostgreSQL localmente mediante `docker-compose.yml` y `docker-compose.override.yml`. Si usas SQLite mediante `docker-compose.sqlite.yml`, no existe PostgreSQL y esta sección no aplica.

### Paso 1 — Detener el entorno local

Abre PowerShell en la raíz del repositorio y ejecuta:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml down
```

### Paso 2 — Cambiar las variables locales

Abre el fichero `.env` local. Si todavía no existe, créalo desde la plantilla:

```powershell
Copy-Item .env.example .env
notepad .env
```

Cambia las tres líneas. Usa tus propios valores:

```dotenv
POSTGRES_DB=supplier_catalog_local
POSTGRES_USER=supplier_local
POSTGRES_PASSWORD=<contraseña-local-nueva>
```

### Paso 3 — Borrar el volumen anterior

Este paso elimina permanentemente la base de datos local anterior:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml down -v
```

Antes de ejecutar el comando, comprueba que PowerShell está situado en la raíz del repositorio local correcto.

### Paso 4 — Crear la base de datos nueva

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up --build -d
```

PostgreSQL crea el usuario y la base de datos con los valores nuevos. La API aplica automáticamente las migraciones pendientes durante su inicio porque `Database:ApplyMigrationsOnStartup` está habilitado por defecto.

### Paso 5 — Comprobar el resultado

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml ps
docker compose -f docker-compose.yml -f docker-compose.override.yml logs --tail=100 ignakeeai.mcpserver.supplier.api
Invoke-WebRequest http://localhost:5100/health
```

El estado de `postgres` debe ser `healthy` y `/health` debe responder HTTP 200.

## B. Reinicialización de Staging en Hetzner

Haz este procedimiento en el servidor Hetzner de Staging, no en tu ordenador local.

### Paso 1 — Conectar y entrar en el directorio de despliegue

Desde PowerShell local, con los valores reales de Staging:

```powershell
ssh <DEPLOY_USER>@<DEPLOY_HOST>
cd <DEPLOY_PATH>
```

### Paso 2 — Editar el fichero privado de Staging

En el servidor Hetzner:

```bash
nano .env.staged
```

Actualiza los valores:

```dotenv
POSTGRES_DB=supplier_catalog_staged
POSTGRES_USER=supplier_staged
POSTGRES_PASSWORD=<contraseña-staging-nueva>
```

Guarda el fichero y cierra el editor.

### Paso 3 — Elegir la imagen de API

Busca en GitHub Actions el run correcto de la rama `staged`. Copia su etiqueta inmutable, con formato `sha-<commit>`. En el servidor, reemplaza el marcador:

```bash
export IMAGE_TAG='sha-<commit-del-run-de-staged>'
export ASPNETCORE_ENVIRONMENT='Staging'
```

Estas variables son obligatorias porque Compose necesita evaluar el servicio `api`, incluso cuando se opera únicamente sobre PostgreSQL.

### Paso 4 — Eliminar la base de datos anterior

**Este paso borra permanentemente todos los datos de Staging.**

```bash
docker compose --env-file .env.staged -f docker-compose.production.yml down -v
```

### Paso 5 — Crear de nuevo PostgreSQL y la API

```bash
docker compose --env-file .env.staged -f docker-compose.production.yml pull
docker compose --env-file .env.staged -f docker-compose.production.yml up -d
```

El volumen nuevo hace que PostgreSQL cree el usuario y la base de datos con los nuevos valores. La API aplica las migraciones al iniciar.

### Paso 6 — Verificar Staging

```bash
docker compose --env-file .env.staged -f docker-compose.production.yml ps
docker compose --env-file .env.staged -f docker-compose.production.yml logs --tail=100 api postgres
curl --fail http://localhost:5100/health
```

No continúes con Production hasta que Staging esté sano y las pruebas MCP hayan finalizado correctamente.

## C. Reinicialización de Production en Hetzner

El proceso es igual que Staging, pero se realiza en el servidor Hetzner de Production y elimina datos reales. Programa una ventana de mantenimiento y confirma que tienes una copia de seguridad antes de empezar.

### Paso 1 — Conectar y entrar en Production

```powershell
ssh <DEPLOY_USER>@<DEPLOY_HOST>
cd <DEPLOY_PATH>
```

### Paso 2 — Actualizar las variables de Production

```bash
nano .env.production
```

Ejemplo:

```dotenv
POSTGRES_DB=supplier_catalog_production
POSTGRES_USER=supplier_production
POSTGRES_PASSWORD=<contraseña-production-nueva>
```

### Paso 3 — Elegir la imagen aprobada

Usa la etiqueta `sha-<commit>` del run de GitHub Actions que ya ha sido validado en Staging:

```bash
export IMAGE_TAG='sha-<commit-aprobado>'
export ASPNETCORE_ENVIRONMENT='Production'
```

### Paso 4 — Eliminar el volumen anterior

**Este paso borra permanentemente todos los datos de Production.**

```bash
docker compose --env-file .env.production -f docker-compose.production.yml down -v
```

### Paso 5 — Crear de nuevo el entorno

```bash
docker compose --env-file .env.production -f docker-compose.production.yml pull
docker compose --env-file .env.production -f docker-compose.production.yml up -d
```

### Paso 6 — Verificar Production

```bash
docker compose --env-file .env.production -f docker-compose.production.yml ps
docker compose --env-file .env.production -f docker-compose.production.yml logs --tail=100 api postgres
curl --fail http://localhost:5100/health
```

Verifica además la URL pública HTTPS y las herramientas MCP antes de comunicar el cambio a Legio.

## D. Rotar sólo `POSTGRES_PASSWORD` sin borrar datos

Usa esta opción únicamente si quieres conservar la base de datos y cambiar sólo la contraseña. No cambia `POSTGRES_USER` ni `POSTGRES_DB`.

1. Accede al entorno correcto y define su contexto de Compose. Para Staging:

   ```bash
   export IMAGE_TAG='maintenance'
   export ASPNETCORE_ENVIRONMENT='Staging'
   ```

   Para Production, usa `ASPNETCORE_ENVIRONMENT='Production'` y `.env.production` en los comandos siguientes.

2. Abre PostgreSQL. Este comando toma el usuario real desde el contenedor:

   ```bash
   docker compose --env-file .env.staged -f docker-compose.production.yml exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d postgres'
   ```

3. En la consola `psql`, escribe el comando siguiente y pulsa Entrar:

   ```sql
   \password
   ```

   Introduce dos veces la nueva contraseña. PostgreSQL la cambia sin mostrarla.

4. Sal de `psql`:

   ```sql
   \q
   ```

5. Edita `.env.staged` y asigna la misma contraseña nueva a `POSTGRES_PASSWORD`.

6. Reinicia la API para que use la conexión nueva. No recrees el volumen:

   ```bash
   docker compose --env-file .env.staged -f docker-compose.production.yml up -d --no-deps api
   ```

7. Comprueba `/health` y los logs de `api`.

Para Local, sustituye los comandos de Compose de Hetzner por:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d postgres'
```

Después actualiza `POSTGRES_PASSWORD` en `.env` y reinicia sólo la API:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --no-deps ignakeeai.mcpserver.supplier.api
```

## E. Cambiar usuario o nombre de base sin borrar datos

No cambies únicamente `POSTGRES_USER` o `POSTGRES_DB` en un volumen existente. PostgreSQL no renombra ni transfiere permisos automáticamente y la API dejará de conectar.

Si necesitas conservar los datos, realiza una copia de seguridad, crea la nueva base y restaura los datos siguiendo un procedimiento supervisado. Para un entorno de aprendizaje o cuando los datos no importan, usa las secciones A, B o C: son el método más sencillo y fiable para cambiar los tres valores.

## Si algo falla

- `required variable IMAGE_TAG is missing`: define `IMAGE_TAG` antes de ejecutar Compose en Staging o Production.
- `required variable ASPNETCORE_ENVIRONMENT is missing`: define `ASPNETCORE_ENVIRONMENT` como `Staging` o `Production`.
- `password authentication failed`: confirma que la contraseña introducida con `\password` coincide exactamente con `POSTGRES_PASSWORD` del fichero de entorno y reinicia `api`.
- `/health` no responde 200 después de una reinicialización: consulta los logs de `api` y `postgres`; la API aplica las migraciones al inicio y debe finalizar sin errores.
- El contenedor `postgres` no inicia: revisa que `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` no estén vacíos y que no exista un volumen anterior si has elegido la reinicialización.
