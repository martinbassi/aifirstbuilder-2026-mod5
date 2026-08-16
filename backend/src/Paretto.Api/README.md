# Paretto.Api

## Regenerar el cliente HTTP de Angular (NSwag)

`frontend/src/app/core/api-client/api-client.generated.ts` se genera automáticamente a partir del
`swagger.json` que expone esta API, usando la configuración de `nswag.json` (en este mismo
directorio). **Nunca se edita a mano** — cualquier cambio de contrato del backend (nuevo endpoint,
DTO modificado, etc.) requiere regenerarlo con este comando.

### Prerequisito

Instalar NSwag CLI como herramienta global de .NET (una sola vez por máquina):

```bash
dotnet tool install -g NSwag.ConsoleCore
```

### Paso 1 — levantar la API localmente

Desde la raíz del repo:

```bash
dotnet run --project backend/src/Paretto.Api
```

Esto deja la API escuchando en `https://localhost:7126` (perfil `https` de
`Properties/launchSettings.json`), que es la URL que `nswag.json` usa para leer
`swagger/v1/swagger.json`.

### Paso 2 — regenerar el cliente

En **otra terminal**, con la API del paso 1 todavía corriendo, desde
`backend/src/Paretto.Api/`:

```bash
nswag run nswag.json
```

Esto sobrescribe `frontend/src/app/core/api-client/api-client.generated.ts` con el cliente
actualizado.

### Nota

El archivo de salida (`api-client.generated.ts`) es generado y nunca se edita a mano. Si el
contrato del backend cambia, se regenera repitiendo los dos pasos de arriba — no se parchea el
archivo generado directamente.
