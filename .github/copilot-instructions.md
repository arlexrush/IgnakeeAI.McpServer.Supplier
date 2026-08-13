# Copilot Instructions

## General Guidelines
- El usuario prefiere que las correcciones propuestas expliquen con precisión la ubicación de cada cambio y describan su efecto.
- El usuario exige revisar completamente el código y la documentación aplicable antes de proponer flujos operativos, para no dar comandos que fallen por requisitos de configuración omitidos.

## Project-Specific Rules
- El repositorio usa tres ramas y entornos asociados: `develop` para desarrollo, `staged` para preproducción y `master` para producción; el flujo de CI/CD debe respetar esta promoción.
- Los entornos `staged` y `master`/producción se desplegarán en servidores de Hetzner; `develop` se expone localmente mediante ngrok.
- La descripción del commit/workflow «feature/Prepare for CI CD» corresponde a una pull request desde `develop` hacia `staged`, y no debe interpretarse como una rama feature.