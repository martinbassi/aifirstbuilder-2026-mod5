# Threat Model FEAT-008: Reemplazar input file por NzFileUpload con loader y preview en el formulario de creación de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-008 |
| Date | 2026-08-29 |
| Scope | `create-mural-form.component.ts/.html` (frontend), `app.config.ts` (registro de ícono) |

## Contexto arquitectónico

Este cambio es puramente de capa de presentación en el frontend: reemplaza el control de selección
de archivo (`<input type="file">` nativo → `nz-upload` de ng-zorro), sin tocar el endpoint de
creación de mural, el pipeline de validación NSFW, ni el límite de tamaño del backend. El archivo
sigue viajando por el mismo flujo de envío existente (`mural.service.ts` → cliente NSwag →
`POST` multipart), fuera de alcance de este ticket.

**Trust boundary relevante:** navegador (no confiable) → API backend (confiable, autoridad real).
Esta frontera no se mueve con este cambio — las validaciones del lado del navegador (tipo MIME,
tamaño) son, como ya eran con el `<input>` nativo, **UX-only**; el backend sigue siendo quien valida
de verdad: `RequestFormLimits` (tamaño, fix de FEAT-001b), clasificación NSFW por firma de bytes
(RF-015, con el fix de WebP de FIX-004), y ocultamiento de murales `pending` en resultados públicos
(RF-013). Nada de esto cambia en FEAT-008.

## Componentes nuevos/modificados y su superficie

| Componente | Acepta input de usuario | Expone datos sensibles | Cruza un trust boundary |
|---|---|---|---|
| `beforeUpload()` / `fileList` signal | Sí (archivo elegido por el usuario) | No | No (todo client-side, el submit ya existente es el único cruce) |
| `URL.createObjectURL` (preview) | No (deriva del archivo ya aceptado) | No (blob: URL de mismo origen, no persiste tras `revokeObjectURL`) | No |
| `DeleteOutline` en `app.config.ts` | No | No | No |

## Análisis STRIDE

| Categoría | Aplica a este cambio | Evaluación |
|---|---|---|
| **Spoofing** | No | No se introduce identidad, sesión ni autenticación nueva. |
| **Tampering** | Parcial | El navegador puede reportar un `file.type`/`file.size` falseado (ya era así con el `<input>` nativo — no es una regresión). La única autoridad real sigue siendo el backend, sin cambios. |
| **Repudiation** | No | El submit sigue yendo por el mismo flujo/logging existente; no hay una acción nueva a repudiar. |
| **Information Disclosure** | Bajo | El `blob:` URL del preview es de mismo origen y vive solo en memoria del navegador del propio usuario; no se comparte con otros usuarios ni se persiste. |
| **Denial of Service** | Bajo (solo cliente) | Si el `blob:` URL no se revoca al reemplazar/quitar el archivo o destruir el componente, se acumula memoria en la pestaña del propio usuario (self-DoS, sin impacto en el servidor ni en otros usuarios). |
| **Elevation of Privilege** | No | Sin cambios de permisos/roles. |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Mitigación propuesta |
|---|---|---|---|---|
| R1: Validación de tipo/tamaño en `beforeUpload` es bypasseable (MIME/tamaño reportados por el navegador) | Tampering | Media | Bajo | Ya mitigado por diseño: es la misma limitación que tenía el `<input>` nativo (documentada como UX-only en el propio componente); el backend es la autoridad real (`RequestFormLimits` + NSFW por firma de bytes). No requiere cambio adicional — **riesgo aceptado**, no introducido por este ticket. |
| R2: Memory leak en el navegador por `blob:` URLs no revocadas | Denial of Service (cliente) | Baja | Bajo | Ya cubierto por el diseño del PRD (NFR-01/AC-07): revocar en reemplazo, en eliminación y en `ngOnDestroy`. El spec debe implementarlo explícitamente en Block 2 antes de darlo por cerrado. |
| R3: El botón de eliminar de `nz-upload-list` (`nzType="delete"`) queda sin ícono registrado y rompe visualmente en producción | Denial of Service (UX, no seguridad) | Media (precedente: FEAT-004/FEAT-006 tuvieron este mismo bug con otros íconos) | Bajo | Ya cubierto por el gap plegado en PLAN: registrar `DeleteOutline` en `provideNzIcons(...)` en `app.config.ts` (Block 1). |

No se identifican riesgos CRITICAL ni HIGH: el cambio no introduce un endpoint nuevo, no cruza un
trust boundary nuevo, y no maneja datos sensibles nuevos (PII, credenciales, datos financieros). La
foto del mural, una vez aprobada, es contenido público por diseño (fuera del alcance de este
ticket).

## Clasificación de datos sensibles (F-TM-05)

- **Archivo de imagen seleccionado:** dato "Pending" hasta moderación (RF-013 ya lo oculta de
  resultados públicos), luego "Public". Sin cambios de clasificación en este ticket.
- No hay PII, credenciales ni datos financieros involucrados en este cambio.

## Riesgos aceptados

Ninguno requiere aprobación formal del usuario: R1 es una limitación preexistente (no introducida
por este ticket, ya mitigada arquitectónicamente por el backend), y R2/R3 ya tienen mitigación
concreta plegada en el diseño del spec (no quedan como riesgo abierto).

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 3                            │
│  Trust boundaries declared: 1 (navegador → API backend,   │
│    sin cambios respecto al diseño existente)               │
│                                                            │
│  Risks:                                                    │
│    🟡 MEDIUM: —                                            │
│    🟢 LOW: R1 (validación cliente bypasseable, heredada,   │
│       backend es la autoridad real)                         │
│    🟢 LOW: R2 (memory leak de blob: URLs, mitigado por      │
│       diseño — NFR-01/AC-07)                                 │
│    🟢 LOW: R3 (ícono delete roto en producción, mitigado     │
│       por diseño — registro en app.config.ts)                 │
│                                                                │
│  Mitigations to fold into the spec:                             │
│    1. Revocar blob: URL en reemplazo/eliminación/ngOnDestroy      │
│       (Block 2).                                                    │
│    2. Registrar DeleteOutline en provideNzIcons (Block 1).            │
│                                                                          │
│  ─────────────────────────────────────────────────────                   │
│  Risks: C:0 H:0 M:0 L:3                                                    │
│  Report: docs/daw/security/threat-FEAT-008.md                                │
└─────────────────────────────────────────────────────────┘
```
