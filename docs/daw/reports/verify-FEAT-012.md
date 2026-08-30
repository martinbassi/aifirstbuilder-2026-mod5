# Reporte de verificación — FEAT-012

Ticket: **FEAT-012** — Comando único para levantar frontend+backend visibles en la LAN
Rama: `feat/FEAT-012-lan-deploy` · Tier: FEATURE
PRD: `docs/daw/prd/prd-FEAT-012.md` · Spec: `docs/daw/specs/spec-FEAT-012.md`

## Ronda 1 — 2026-08-30

Ejecutado por el agente `daw-module-verifier` (cross-verificación independiente).

### Suite y calidad (confirmado independientemente)
- Backend: `dotnet test` → 149/149 PASSED. `dotnet build` → 0 warnings.
- Frontend: `ng test --watch=false` → 186/186 PASSED. `tsc --build --noEmit` limpio. `ng lint` limpio.
- Cobertura: Block 1 (`Program.cs:234-237`) 100% líneas/branches sobre el diff nuevo. Block 2
  (`app.config.ts`, factory nuevo) 100% líneas/branches/funciones. Block 3 (bash) N/A, sin
  cobertura de código gestionado.

### Trazabilidad PRD → Código → Tests (9 AC)

| AC | Resultado | Detalle |
|----|-----------|---------|
| AC-01 | ✅ PASS | Backend/script confirmados con arranque real en el commit de Block 3 |
| AC-02 | ❌ **FAIL** | Mitad backend sin test automatizado de CORS dinámico (patrón ya existe en `CorsTests.cs`); mitad E2E (abrir desde otro dispositivo) explícitamente pendiente según el commit de Block 3 |
| AC-03 | ✅ PASS | `LanModeTests.Without_LanMode_...` — regresión automatizada, verde |
| AC-04 | ⚠️ WARN | Sin confirmación explícita en el commit de que se ejecutó la verificación manual (comparte comando con AC-03, no depende de código nuevo) |
| AC-05 | ❌ **FAIL** | Sin test automatizado (aceptado por diseño) NI evidencia escrita de que la verificación manual (cambio de IP) se haya ejecutado — reproducible en la misma máquina, sin hardware externo |
| AC-06 | ✅ PASS | Ctrl+C limpio, confirmado en el commit de Block 3 |
| AC-07 | ❌ **FAIL** | Requiere un segundo dispositivo en otra red — explícitamente pendiente según el commit de Block 3 |
| AC-08 | ❌ **FAIL** | Código correcto (sad path real), pero sin evidencia escrita persistida de que la verificación manual se ejecutó y pasó — reproducible sin hardware externo |
| AC-09 | ✅ PASS | `app.config.spec.ts` — automatizado, verde, reforzado por evidencia de NSwag contra `localhost:7126` |

### Spec — bloques
- ✅ Block 1: 2/2 archivos, 2/2 tests requeridos, 100% cobertura del diff.
- ✅ Block 2: 3/3 archivos, 2/2 tests requeridos, 100% cobertura del diff.
- ❌ Block 3: de las 8 verificaciones manuales del spec, solo 3-4 confirmadas con evidencia escrita
  de ejecución y resultado (arranque/bindings, NSwag, Ctrl+C limpio); el resto sin rastro
  persistido de haberse corrido.

### TDD evidence
- ✅ Block 1/Block 2: evidencia TDD verificada independientemente durante CODE, tests existen y
  corren en verde.
- ⚠️ Block 3: sin TDD tradicional por diseño aprobado en PLAN (aceptable), pero la ejecución real
  de las verificaciones manuales comprometidas quedó incompleta — no es un problema de metodología,
  es que el trabajo de verificación no se terminó.

### Veredicto ronda 1

```
Total: 10 PASS, 4 FAIL, 3 WARN
Result: BLOCKED
```

**Razonamiento del verificador (aplicando F-VER-01 mecánicamente):** un test manual comprometido
pero no ejecutado es, a efectos de VERIFY, indistinguible de "no hay test" — el catálogo no exime
de esta regla a los criterios que requieren hardware real. La ausencia de evidencia de ejecución
para AC-05/AC-08 (ambos reproducibles en la misma máquina, sin dispositivo físico externo) es un
FAIL por trabajo incompleto, distinto del FAIL de AC-02/AC-07 (que sí requieren genuinamente un
segundo dispositivo físico en otra red).

**Acción:** corrective loop VERIFY → CODE.

## Corrective loop — CODE — 2026-08-30

Decisión del usuario: *"Arreglás lo tuyo ahora, yo hago las 2 verificaciones con el celular
después"* — AC-02 (abrir la URL desde otro dispositivo) y AC-07 (confirmar inalcanzable fuera de
la LAN) requieren genuinamente hardware físico y quedan pendientes del usuario. Los otros 2 FAILs
(AC-05, AC-08) y la mitad backend de AC-02 (CORS dinámico) son reproducibles en esta misma máquina
y se resolvieron en esta sesión.

### Intento descartado: test automatizado de CORS dinámico

Se agregó un test a `LanModeTests.cs` que simulaba `Cors__AllowedOrigins__1` vía
`WithWebHostBuilder().ConfigureAppConfiguration(...)`. Falló de forma reproducible. Diagnóstico:
`factory.Services.GetRequiredService<IConfiguration>()` (resuelto DESPUÉS de que el host termina
de construirse) sí ve ambos orígenes correctamente — la fuente de configuración del test se
mezcla bien. Pero `Program.cs:191` lee `builder.Configuration.GetSection("Cors:AllowedOrigins")`
**antes** de `builder.Build()` (línea 209), para capturar el array que `AddCors`/`WithOrigins`
usa. Con hosting mínimo (`WebApplication.CreateBuilder`), `WebApplicationFactory` inyecta el
`ConfigureAppConfiguration` del test recién en el momento en que se llama a `Build()` — cualquier
lectura de `builder.Configuration` hecha antes de esa llamada, aunque sea una línea antes, no lo
ve todavía. Es una particularidad del arnés de test (`WebApplicationFactory` + hosting mínimo), no
un bug de `Program.cs`: en producción real, los env vars ya están cargados por
`WebApplication.CreateBuilder(args)` desde el arranque del proceso, antes de que corra cualquier
línea de `Program.cs` — por eso el mecanismo real (`scripts/dev-lan.sh` exportando
`Cors__AllowedOrigins__1` como env var real) nunca pisa este problema. Se revirtió el test
(`git checkout -- backend/tests/Paretto.Api.Tests/LanModeTests.cs`) y se lo reemplazó por
verificación real end-to-end (abajo), que ejercita el código de producción tal cual corre en
`dev-lan.sh`, sin el artefacto del arnés de test.

### Evidencia real — CORS dinámico (backend de AC-02, FR-06)

Con `scripts/dev-lan.sh` corriendo de verdad (LAN IP real detectada: `192.168.1.7`), contra
`http://localhost:5267` (el mismo puerto que expone en la LAN):

```
$ curl -D - -H "Origin: http://localhost:4200" .../nearby-murals?...
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://localhost:4200

$ curl -D - -H "Origin: http://192.168.1.7:4200" .../nearby-murals?...
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://192.168.1.7:4200

$ curl -D - -H "Origin: http://evil.example.com" .../nearby-murals?...
HTTP/1.1 200 OK
(sin header Access-Control-Allow-Origin)
```

El índice 0 (`localhost:4200`) y el índice 1 agregado dinámicamente (`192.168.1.7:4200`) quedan
ambos permitidos sin pisarse; un origen no declarado no recibe el header. Mecanismo real
confirmado de punta a punta.

### Evidencia real — AC-05 (cambio de IP sin reconfiguración manual)

Se interceptó `hostname -I` con un stub en `PATH` (sin tocar ningún archivo del repo) que
devuelve `10.20.30.40` en vez de la IP real, simulando reconexión a otra red:

```
$ PATH="<stub>:$PATH" timeout 6s bash scripts/dev-lan.sh
Iniciando backend (dotnet run)...
Iniciando frontend (ng serve --host 0.0.0.0)...

Abrí esta URL desde otro dispositivo de la misma red local:
  http://10.20.30.40:4200
...
Deteniendo procesos...   ← limpieza correcta tras el timeout (SIGTERM), sin duplicar el mensaje
```

`pgrep -af "dotnet run|ng serve"` tras la corrida: sin procesos remanentes. La detección de IP es
dinámica en cada corrida, sin caché ni archivo a editar — AC-05/NFR-02 confirmado.

### Evidencia real — AC-08 (sin interfaz de LAN)

Mismo mecanismo, stub de `hostname -I` devolviendo vacío:

```
$ PATH="<stub>:$PATH" bash scripts/dev-lan.sh
Error: no se detectó ninguna interfaz de LAN activa (hostname -I no devolvió ninguna IP).
Conectate a una red (WiFi/Ethernet) y volvé a intentar.
$ echo $?
1
```

Termina antes de arrancar `dotnet`/`ng` (confirmado por `pgrep` sin diferencia antes/después) —
AC-08 confirmado.

### Evidencia real — puerto ocupado (checklist ítem 8, no ligado a un AC específico)

Se ocupó el puerto 5267 con `nc -l 5267` antes de correr el script: la excepción real de Kestrel
(`AddressInUseException`) llega íntegra a la terminal de inmediato, el frontend sigue built y
corriendo, y el `trap` limpia todo sin huérfanos al cortar. Sin cuelgue.

### Suite y calidad — re-confirmado

- Backend: `dotnet test` → 149/149 PASSED (sin cambios de código de producción en este loop; el
  único archivo tocado, `LanModeTests.cs`, quedó revertido a su estado ya committeado).
- `dotnet build` → 0 warnings.
- No hubo cambios en frontend en este loop.

### Estado de los 4 FAILs de ronda 1

| AC | Ronda 1 | Ronda 2 |
|----|---------|---------|
| AC-02 | ❌ FAIL | ⚠️ Backend (CORS dinámico) verificado con evidencia real arriba. Mitad E2E (abrir desde otro dispositivo) **pendiente del usuario, con celular** — excepción de proceso explícitamente acordada |
| AC-05 | ❌ FAIL | ✅ Verificado con evidencia real arriba |
| AC-07 | ❌ FAIL | **Pendiente del usuario, con celular** — requiere genuinamente un segundo dispositivo fuera de la LAN, excepción de proceso explícitamente acordada |
| AC-08 | ❌ FAIL | ✅ Verificado con evidencia real arriba |

**Excepción de proceso:** AC-02 (mitad E2E) y AC-07 no pueden verificarse desde esta máquina —
requieren un dispositivo físico en la LAN y otro fuera de ella. El usuario decidió explícitamente
avanzar con el resto del cierre y ejecutar estas 2 verificaciones él mismo después
("Arreglás lo tuyo ahora, yo hago las 2 verificaciones con el celular después"). Documentado acá
como excepción de proceso aceptada, no como gap silencioso.
