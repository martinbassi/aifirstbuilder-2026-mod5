# Reporte de verificación — FIX-005

Ticket: **FIX-005** — Coordenadas 0,0 en sugerencias de calle+número del autocomplete de direcciones
Rama: `fix/FIX-005-address-portal-coordinates` · Tier: FIX
RCA: `docs/daw/specs/rca-FIX-005.md` · Fix-plan: `docs/daw/specs/fix-FIX-005.md`

Ejecutado por el agente `daw-module-verifier` (cross-verificación independiente, no escribió el código).

## Fix-plan steps (15/15)

Los 15 pasos del "Solution — steps" están implementados tal como se describieron, incluyendo las 2
correcciones del arch audit de PLAN: nombrado en inglés (`StreetId`/`Locality`) en los tipos propios
del proyecto (`AddressSuggestionDto`, `ResolveAddressQuery`, controller) — solo el wire type privado
`IdeGeocodeResultWire` conserva los nombres del proveedor (`IdCalle`/`Localidad`) para poder
deserializar; y la regla de negocio "0,0 significa que hay que resolver" movida a
`AddressService.resolveIfNeeded()`, no al componente.

## Regression test
✅ Confirmado rojo→verde de forma verificable en el historial: `ResolveAsync`/`resolveIfNeeded` no
existían en el commit padre — los tests nuevos referencian métodos inexistentes antes del fix (no
podían compilar/pasar). `dotnet test --filter "FullyQualifiedName~Resolve"` → 11/11 pasan.

⚠️ **WARN:** el test unitario de `ResolveAsync` no verifica explícitamente la URL exacta llamada
(`geocode/find` vs `geocode/candidates`) — mismo patrón preexistente en el archivo para
`SearchAsync`/`ReverseGeocodeAsync`, no una regresión de este fix. Mitigado por revisión manual del
código (`ResolveAsync` construye `api/v1/geocode/find?idcalle=...` correctamente).

## Cobertura de sad paths
- ✅ Sin resultado (`/find` vacío) → 200 con `suggestion: null`, no error.
- ✅ Proveedor caído (503).
- ✅ `streetId`/`portal` ≤ 0 → 422.
- ✅ `locality`/`type` vacíos → 400 (código distinto, documentado correctamente en el fix-plan y
  confirmado en el código real: validación automática de `[ApiController]`, no FluentValidation).

## Regresión
- ✅ Backend: `dotnet test` → 146/146 (número esperado exacto).
- ✅ Frontend: `npx ng test --watch=false` → 184/184 (número esperado exacto). Errores de consola
  (`IconNotFoundError`/`NG04002`) preexistentes, ya documentados en verificaciones anteriores.
- ✅ Lint/typecheck/build limpios en ambos lados.

## Coherencia con el RCA
✅ El fix ataca la causa raíz documentada (falta de segundo llamado a `/find`), no el síntoma.

## TDD evidence
✅ Sin reporte de implementador separado, pero la evidencia del historial de git reemplaza esa
función: los métodos nuevos no existían antes del fix, por lo que el rojo→verde es estructuralmente
verificable, no solo una afirmación del commit.

## Hallazgo de proceso
⚠️ **WARN:** el commit de CODE (`27fb7c6`) modifica `docs/daw/specs/fix-FIX-005.md` (corrige 422→400
para `locality`/`type` vacíos, según lo encontrado al implementar) — el orchestrator prohíbe
modificar el spec en fase CODE. El contenido corregido es preciso y coincide con el comportamiento
real; señalado para no repetirlo, no bloquea el fix.

## Veredicto

```
Total: 15 PASS (steps) + 6 PASS (verificaciones) + 2 WARN, 0 FAIL
Result: PASSED
```

**Conclusión: FIX-005 verificado. `gates.verify = true`. Listo para RELEASE.**
