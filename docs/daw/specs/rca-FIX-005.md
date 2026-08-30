# RCA FIX-005: Coordenadas 0,0 en sugerencias de calle+número del autocomplete de direcciones

## Síntoma reportado por el usuario

Al buscar una dirección por calle y número en el formulario de carga de mural, solo la primera
sugerencia devolvía coordenadas correctas al seleccionarla; el resto quedaba con latitud/longitud
en `0`.

## Investigación

`IdeUruguayAddressProviderClient.SearchAsync` (backend, Block 1 de FEAT-011) llama únicamente a
`GET /api/v1/geocode/candidates?q=...` del proveedor externo `direcciones.ide.uy` y confía
directamente en los campos `lat`/`lng` de cada resultado — sin ningún segundo llamado.

Probando el endpoint real en vivo:

```
GET /api/v1/geocode/candidates?q=Bulevar+Artigas+1234
```

```
0  ARTIGAS, ARTIGAS                                    lat=-30.4076  lng=-56.4721  type=LOCALIDAD
1  ARTIGAS RURAL                                        lat=-30.5884  lng=-57.0699  type=LOCALIDAD
2  BARROS BLANCOS, CANELONES                            lat=-34.7523  lng=-55.9930  type=LOCALIDAD
3  BULEVAR ARTIGAS 1234, ANSINA, TACUAREMBO             lat=0.0       lng=0.0       type=CALLEyPORTAL
4  BULEVAR GENERAL ARTIGAS 1234, MONTEVIDEO, MONTEVIDEO lat=0.0       lng=0.0       type=CALLEyPORTAL
5  BULEVAR ARTIGAS 1234, JOAQUIN SUAREZ, CANELONES      lat=0.0       lng=0.0       type=CALLEyPORTAL
...(el resto de los CALLEyPORTAL, todos en 0.0/0.0)
```

**Causa raíz confirmada:** `/api/v1/geocode/candidates` del proveedor externo **nunca** resuelve
coordenadas para resultados de tipo `CALLEyPORTAL` (calle + número exacto) — siempre devuelve
`lat: 0.0, lng: 0.0` para ese tipo, incluso para direcciones reales de Montevideo. Solo los
resultados de tipo `LOCALIDAD`/`POI` (centroides ya almacenados a nivel ciudad/punto de interés)
traen coordenadas reales. Esto explica el síntoma reportado: en una búsqueda típica, los primeros
resultados suelen ser coincidencias de tipo `LOCALIDAD` (con coordenadas), y los de tipo
`CALLEyPORTAL` (la dirección exacta que el usuario realmente busca) vienen después, todos en 0,0.

El proveedor sí expone una forma de resolver coordenadas reales para un resultado `CALLEyPORTAL`
específico: `GET /api/v1/geocode/find`, pasándole los datos que ya vienen en el candidato
(`idcalle`, `portal`, `localidad`, `type`):

```
GET /api/v1/geocode/find?idcalle=8143&portal=1234&localidad=MONTEVIDEO&type=CALLEyPORTAL
→ lat=-34.9059, lng=-56.1639, stateMsg="Aproximado"
```

Confirmado contra varias direcciones reales de Montevideo (probado en vivo, no documentado
explícitamente en el swagger del proveedor más allá del listado de parámetros).

**Cadena de eventos:**
1. FEAT-011 (spec Block 1) diseñó `IdeUruguayAddressProviderClient` para mapear directamente los
   campos del wire format de `/candidates` (`ToSuggestion`), sin contemplar que ese endpoint no
   resuelve coordenadas para `CALLEyPORTAL`.
2. Los tests del bloque (`AddressesControllerTests`, `IdeUruguayAddressProviderClientTests`) usan
   fixtures de mano (`FakeAddressProviderClient`, wire JSON armado a mano) que siempre incluyen
   lat/lng plausibles — nunca reprodujeron el caso real del proveedor donde `CALLEyPORTAL` viene en
   0,0, porque nadie probó contra el proveedor real con una dirección de calle+número.
3. El bug llegó a producción sin que ningún gate lo detectara (ni CODE ni VERIFY de FEAT-011
   ejercitaron el proveedor real).

## Análisis

```
┌─────────────────────────────────────────────────────────┐
│  DEFINE — Root Cause Analysis                            │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Ticket: FIX-005 — Coordenadas 0,0 en sugerencias de     │
│    calle+número (candidates de direcciones.ide.uy no     │
│    resuelve CALLEyPORTAL)                                │
│                                                          │
│  Root cause: IdeUruguayAddressProviderClient.SearchAsync │
│    confía en lat/lng de /candidates, que el proveedor    │
│    siempre deja en 0,0 para resultados CALLEyPORTAL       │
│    (calle+número exacto) — solo LOCALIDAD/POI traen       │
│    coordenadas reales en ese endpoint. El proveedor       │
│    expone /find (idcalle+portal+localidad+type) para      │
│    resolver coordenadas reales de un CALLEyPORTAL         │
│    específico, nunca invocado.                             │
│  Affected component: Paretto.Infrastructure/Geocoding/     │
│    IdeUruguayAddressProviderClient.cs                       │
│  Related PRD: prd-FEAT-001b.md — AC-21 (WHEN se resuelve     │
│    una ubicación por GPS o por selección de una sugerencia,   │
│    THE sistema SHALL mostrar la ubicación resuelta antes de     │
│    guardar) ya exige una ubicación real; sin gap de PRD, es      │
│    un defecto de implementación.                                   │
│  Gap in the PRD: no                                                  │
│                                                                        │
└─────────────────────────────────────────────────────────┘
```

## Alcance del fix (a definir en PLAN)

Cuando `SearchAsync` recibe un candidato de tipo `CALLEyPORTAL` con `lat==0 && lng==0`, resolverlo
con un segundo llamado a `/find` antes de devolverlo al frontend (o, alternativamente, resolverlo
recién al seleccionar la sugerencia — decisión de diseño para PLAN, con su propio trade-off de
latencia/cantidad de llamadas). Fuera de alcance de este RCA: PLAN decide el punto exacto de
resolución y el fallback si `/find` tampoco devuelve coordenadas válidas.

## Rollback plan

Revertir el commit del fix. `IdeUruguayAddressProviderClient` vuelve a su comportamiento actual
(coordenadas 0,0 en `CALLEyPORTAL`) — regresión conocida y ya presente en producción, sin riesgo
adicional al revertir.
