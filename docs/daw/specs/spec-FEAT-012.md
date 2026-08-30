# Spec FEAT-012: Comando único para levantar frontend+backend visibles en la LAN

| Field | Value |
|-------|-------|
| Ticket | FEAT-012 |
| PRD | docs/daw/prd/prd-FEAT-012.md |
| Tier | FEATURE |
| Date | 2026-08-30 |
| Spec loops | 0 |

## Summary

Un script nuevo (`scripts/dev-lan.sh`) orquesta backend y frontend en modo LAN vía variables de
entorno — sin tocar el flujo por defecto de `dotnet run`/`ng serve`. El backend gana un pequeño
cambio condicional en `Program.cs` (saltear `UseHttpsRedirection()` cuando el script lo pide) y
preserva el binding HTTPS de `localhost` además de agregar uno HTTP en todas las interfaces —
hallazgo crítico del impact scan: un `ASPNETCORE_URLS` que reemplazara HTTPS por HTTP hubiera roto
tanto el acceso normal a `localhost:4200` (AC-09) como la auto-regeneración de NSwag de
QUICK-FIX-004 (que golpea `https://localhost:7126/swagger/v1/swagger.json` en cada arranque). El
frontend resuelve la URL del backend dinámicamente según el host desde el que se sirvió, y la CSP
de desarrollo se extiende para permitir esa conexión. `allowedHosts` de Vite NO necesita
configurarse — confirmado por el impact scan: Vite permite automáticamente cualquier Host header
que sea una IP literal.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 3 |
| FR-02 | Block 1 — Strategy: sin `LanMode`, `UseHttpsRedirection()` sigue corriendo igual que hoy |
| FR-03 | Block 3 — Strategy: `ng serve` sin flags del script no pasa `--host`, sin cambios |
| FR-04 | Block 1 |
| FR-05 | Block 2 |
| FR-06 | Block 1, Block 3 |
| FR-07 | Block 2 |
| FR-08 | Block 3 |
| NFR-01 | Strategy: el script solo agrega bindings a interfaces YA presentes en la máquina (`0.0.0.0` es "todas las que ya existen", no crea ninguna); sin port forwarding, sin túneles — nada de eso se introduce en ningún archivo |
| NFR-02 | Strategy: el script detecta la IP de LAN en tiempo de ejecución (`hostname -I`), nunca la escribe en un archivo versionado |

## Dependencies between blocks

Block 3 depende de Block 1 y Block 2 (el script asume el `LanMode`/`ASPNETCORE_URLS` que Block 1
espera, y un frontend que ya resuelve su base URL dinámicamente vía Block 2). Block 1 y Block 2 son
independientes entre sí.

## Block 1 — Backend: preservar HTTPS local + habilitar LAN + CORS dinámico

**Files**
- `backend/src/Paretto.Api/Program.cs` (modified) — condicionar `app.UseHttpsRedirection()`.
- `backend/tests/Paretto.Api.Tests/LanModeTests.cs` (new) — tests del comportamiento condicional.

**Logic**

Hoy `app.UseHttpsRedirection()` (línea ~230, fuera de cualquier gate de entorno) se ejecuta
siempre. Si el modo LAN reemplazara el binding HTTPS por uno HTTP wildcard, este middleware
redirigiría cualquier request HTTP entrante (incluidas las de la LAN) a
`https://{host-del-request}:7126` — un endpoint que Kestrel nunca expone fuera de loopback, y que
el celular no confiaría igual (por eso se eligió HTTP para LAN, PRD FR-04). La causa raíz no es el
middleware en sí — es perderse el binding HTTPS. Este bloque corrige ambas cosas:

1. El script (Block 3) exporta `ASPNETCORE_URLS="https://localhost:7126;http://0.0.0.0:5267"` —
   preserva el binding HTTPS de siempre en `localhost` (mismo puerto que hoy, así
   `nswag run nswag.json`/QUICK-FIX-004 y el acceso normal a `localhost:4200` con HTTPS siguen
   funcionando sin cambios, AC-09) y agrega HTTP en todas las interfaces (AC-01, FR-04).
2. `Program.cs` gana un nuevo flag de configuración `LanMode` (PascalCase — arch audit: toda
   clave de configuración propia del proyecto ya sigue esa convención, `Cors`/`AddressProvider`;
   `SCREAMING_SNAKE_CASE` queda reservado a variables del framework como `ASPNETCORE_URLS`).
   Leído del env var `LanMode` que el script exporta en `true` (el binding de configuración de
   .NET no distingue mayúsculas). Cuando está activo, se saltea `app.UseHttpsRedirection()` — sin
   este salto, una request LAN por HTTP sería redirigida al HTTPS inalcanzable de arriba. Sin
   `LanMode` (el caso por defecto, `dotnet run` sin el script) el middleware corre exactamente
   igual que hoy (FR-02) — no hace falta un gate de entorno explícito además: `GetValue<bool>`
   sin la clave presente devuelve `false` por defecto, mismo efecto práctico.
3. CORS: **sin cambios de código**. `Program.cs:191` ya lee `Cors:AllowedOrigins` vía
   `IConfiguration`, que respeta la convención `__` de ASP.NET Core para env vars — el script
   (Block 3) exporta `Cors__AllowedOrigins__1=http://<ip-lan>:4200`, que se agrega al array sin
   pisar el índice 0 existente (`http://localhost:4200` de `appsettings.Development.json`, ya
   verificado en el impact scan). Confirma FR-06.

```csharp
// FEAT-012: en modo LAN (variable de entorno LanMode, seteada por scripts/dev-lan.sh) el
// dispositivo llega por HTTP a la IP de LAN — redirigirlo a HTTPS lo mandaría a un puerto que
// Kestrel solo expone en localhost (ver docs/daw/security/threat-FEAT-012.md, R1: riesgo HTTP
// plano dentro de la LAN, aceptado explícitamente para este modo opt-in).
if (!app.Configuration.GetValue<bool>("LanMode"))
{
    app.UseHttpsRedirection();
}
```

**Error handling**
- Ninguno nuevo — es una decisión de configuración en el arranque, no un camino de error en
  runtime.

**Required tests**
- [ ] `LanModeTests`: con `LanMode=true` en la configuración de un `WebApplicationFactory`, una
  request HTTP simple contra `/api/discovery/nearby-murals?lat=-34.6037&lng=-58.3816&radiusKm=5`
  (mismo endpoint sonda que `CorsTests.cs:26`, con el mismo swap de `AppDbContext`→InMemory /
  `IBlobStorageService`→fake que `CorsTests.cs:31-53` ya resuelve para este tipo de test) no
  recibe redirect (307/301) — valida FR-04/AC-01.
- [ ] `LanModeTests`: sin `LanMode` (default, como hoy), la misma request sigue siendo redirigida
  a HTTPS — regresión explícita de FR-02/AC-03 (que el comportamiento por defecto no cambió).

**Completion criterion**
`dotnet build` sin errores; los 2 tests nuevos pasan; la suite completa del backend sigue en verde
(147/147 antes de este bloque).

## Block 2 — Frontend: base URL dinámica + CSP de LAN

**Files**
- `frontend/src/app/app.config.ts` (modified) — `API_BASE_URL` pasa de `useValue` a `useFactory`.
- `frontend/src/app/app.config.spec.ts` (modified — el archivo ya existe, hoy corre
  `TestBed.configureTestingModule({ providers: appConfig.providers })` sobre la config real sin
  mockear `window.location`) — agregar los 2 tests nuevos.
- `frontend/src/index.development.html` (modified) — CSP `connect-src` + corregir el comentario
  desactualizado sobre `fileReplacements` (el mecanismo real es
  `architect.build.configurations.development.index` en `angular.json:82`, no
  `fileReplacements`, hallazgo del impact scan).

**Logic**

```ts
{
  provide: API_BASE_URL,
  useFactory: () =>
    window.location.hostname === 'localhost'
      ? 'https://localhost:7126'
      : `http://${window.location.hostname}:5267`,
}
```

Accedido desde `localhost:4200` (flujo normal, con o sin el script de LAN corriendo) resuelve
exactamente al mismo `https://localhost:7126` de siempre (FR-02, AC-09). Accedido desde
`http://<ip-lan>:4200` (un dispositivo de la LAN) resuelve a `http://<esa-misma-ip>:5267` — mismo
host que sirvió la página, puerto HTTP del backend (FR-05, AC-02). `auth.interceptor.ts`
(`isSameOrigin`, ya parsea `API_BASE_URL` dinámicamente vía `new URL(...).origin`) funciona sin
cambios con cualquiera de los dos valores — confirmado en el impact scan.

CSP: extender `connect-src` de `'self' https://localhost:7126` a
`'self' https://localhost:7126 http://*:5267` — `*` como host completo es sintaxis CSP válida
("cualquier host"), acotado al puerto HTTP del backend y solo en `index.development.html` (nunca
en el `index.html` de producción, mismo patrón ya establecido en FIX-002). El comentario que
acompaña la CSP en ese archivo (líneas 9-22) ya cita `threat-FEAT-001a.md`/`threat-FIX-002.md` por
cada entrada — la línea nueva de `connect-src` debe agregar la cita a
`docs/daw/security/threat-FEAT-012.md` (R2: wildcard de host acotado a este archivo y a un puerto
específico), siguiendo el mismo patrón.

**Error handling**
- Ninguno nuevo — `useFactory` es una función pura sin caminos de error (siempre devuelve un
  string); no hay entrada de usuario que validar.

**Required tests**
- [ ] `app.config.spec.ts`: `API_BASE_URL` resuelve a `https://localhost:7126` cuando
  `window.location.hostname === 'localhost'` — valida FR-02/AC-09.
- [ ] `app.config.spec.ts`: `API_BASE_URL` resuelve a `http://<hostname>:5267` cuando el hostname
  no es `localhost` — valida FR-05/AC-02. Mecanismo: `window.location` no es reasignable
  directamente en jsdom sin `configurable: true`; usar
  `Object.defineProperty(window, 'location', { value: { hostname: '192.168.1.50' }, configurable: true })`
  antes de crear el `TestBed`, y restaurar el `location` original en un `afterEach` (mismo cuidado
  de limpieza que ya usa `create-mural-form.component.spec.ts` con `vi.restoreAllMocks()` para
  spies globales).

**Completion criterion**
`npx tsc --build --noEmit tsconfig.json` limpio; los 2 tests nuevos pasan; la suite completa del
frontend sigue en verde (184/184 antes de este bloque); `npx ng lint` limpio.

## Block 3 — Script orquestador (`scripts/dev-lan.sh`)

**Files**
- `scripts/dev-lan.sh` (new, ejecutable) — no existe carpeta `scripts/` a nivel de repo hoy
  (confirmado en el impact scan), ni convención de orquestación multi-proceso; sí existe una
  convención de estilo de shell script en `.claude/hooks/*.sh` (shebang `#!/usr/bin/env bash`,
  `set -euo pipefail`) — el script nuevo la adopta, particularmente relevante acá porque maneja
  PIDs y un `trap` de limpieza, donde un fallo silencioso a mitad de camino dejaría procesos
  huérfanos (justo lo que AC-05/AC-06 quieren evitar).

**Logic**

1. Detecta la IP de LAN: `hostname -I | awk '{print $1}'` (Linux — documentado como limitación,
   sin alternativa cross-platform simple disponible en este repo). Si el resultado es vacío
   (sin interfaz de LAN activa, solo loopback) → imprime un error claro y termina con código
   distinto de cero, sin arrancar nada (AC-08).
2. Exporta: `ASPNETCORE_URLS="https://localhost:7126;http://0.0.0.0:5267"`,
   `ASPNETCORE_ENVIRONMENT=Development`, `LanMode=true`,
   `Cors__AllowedOrigins__1="http://${LAN_IP}:4200"`.
3. Arranca el backend (`dotnet run` desde `backend/src/Paretto.Api`) en background, guarda su PID.
4. Arranca el frontend (`npx ng serve --host 0.0.0.0` desde `frontend/`) en background, guarda su
   PID.
5. `trap` sobre `INT`/`TERM`/`EXIT` que mata ambos PIDs y espera a que terminen — ningún proceso
   queda huérfano al cortar con Ctrl+C (AC-05).
6. Imprime la URL a abrir desde el otro dispositivo (`http://${LAN_IP}:4200`) antes de esperar a
   los procesos.

No hace falta tocar `angular.json` para `allowedHosts` — confirmado por el impact scan: Vite
permite automáticamente cualquier Host header que sea una dirección IP literal
(`isHostAllowedInternal`, `node_modules/vite/dist/node/chunks/config.js`), y el flujo de esta
feature es siempre acceder por IP de LAN, nunca por hostname. Documentado acá para que nadie lo
"corrija" innecesariamente más adelante.

**Error handling**
- Sin interfaz de LAN detectada → error explícito, script termina antes de arrancar nada (AC-08).
- Si `dotnet run` o `ng serve` fallan al arrancar → sus propios mensajes de error llegan a la
  terminal (ambos corren con su stdout/stderr heredado, sin redirección que los oculte); el `trap`
  sigue limpiando lo que sí llegó a arrancar.

**Required tests**
- [ ] Manual: correr `./scripts/dev-lan.sh`, confirmar que backend y frontend arrancan y que la
  URL impresa es correcta; abrir esa URL desde otro dispositivo de la misma LAN y confirmar que
  `/login` y `/discover` funcionan de punta a punta (valida AC-01/AC-02).
- [ ] Manual: con el script corriendo, abrir `http://localhost:4200` en la misma máquina y
  confirmar que sigue funcionando contra `https://localhost:7126` (valida AC-09).
- [ ] Manual: sin el script (comandos `dotnet run`/`ng serve` normales), confirmar que ambos
  siguen exactamente igual que antes de este ticket — HTTPS en `localhost:7126`, `ng serve` solo
  en `localhost:4200` (valida AC-03/AC-04).
- [ ] Manual: presionar Ctrl+C y confirmar (`ps`/`jobs`) que ningún proceso de `dotnet`/`node`
  queda huérfano (valida AC-06).
- [ ] Manual: cambiar la IP de LAN de la máquina (reconectar a otra red, o forzar otra IP) y
  volver a correr el script sin editar ningún archivo — confirmar que detecta la IP nueva
  automáticamente y todo sigue funcionando (valida AC-05/NFR-02).
- [ ] Manual: desde un dispositivo fuera de la LAN (ej. datos móviles del celular, no WiFi),
  confirmar que la URL de LAN impresa por el script NO es alcanzable (valida AC-07/NFR-01).
- [ ] Manual: simular ausencia de interfaz LAN (ej. desconectar la red) y confirmar el mensaje de
  error claro, sin que el script quede colgado (valida AC-08).
- [ ] Manual: forzar que uno de los dos procesos falle al arrancar (ej. ocupar el puerto 5267 con
  otro proceso antes de correr el script) y confirmar que el mensaje de error de ese proceso llega
  a la terminal, sin que el script quede colgado esperando indefinidamente.

**Completion criterion**
Las 8 verificaciones manuales pasan.

## Final verification

- Suite completa (backend + frontend) en verde, sin regresiones sobre el estado previo a FEAT-012.
- `dotnet build`, `npx tsc --build --noEmit tsconfig.json`, `npx ng lint` limpios.
- Las 8 verificaciones manuales del Block 3 completas.
