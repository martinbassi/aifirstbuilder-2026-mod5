# Verify FEAT-008: Reemplazar input file por NzFileUpload con loader y preview en el formulario de creación de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-008 |
| PRD | docs/daw/prd/prd-FEAT-008.md |
| Spec | docs/daw/specs/spec-FEAT-008.md |
| Date | 2026-08-29 |
| Rondas | 1 |

## Trazabilidad PRD → Código → Tests (F-VER-01)

| AC | Implementado en | Test | Resultado |
|---|---|---|---|
| AC-01 | `beforeUpload()` (create-mural-form.component.ts:110) | "selecciona un archivo válido y muestra el preview inmediatamente" | ✅ PASA |
| AC-02 | `beforeUpload()` rama de reemplazo + `revokeCurrentThumbUrl()` (líneas 126-141) | "revoca el thumbUrl anterior al reemplazar…" | ✅ PASA |
| AC-03 | `onUploadChange()` (líneas 154-164) | "limpia el estado cuando se elimina el archivo…" | ⚠️ PASA — sin spy explícito de `URL.revokeObjectURL` en esta ruta (ver Warnings) |
| AC-04 | `beforeUpload()` rama tamaño | "rechaza un archivo oversized…" | ✅ PASA |
| AC-05 | `beforeUpload()` rama tipo | "rechaza un archivo de tipo inválido…" | ✅ PASA |
| AC-06 | `submit()` / `nzLoading` (sin cambios de Block 1) | "mantiene nzLoading durante el submit sin disparar ninguna subida…" | ✅ PASA |
| AC-07 | `revokeCurrentThumbUrl()` / `ngOnDestroy()` | "revoca el thumbUrl en el reemplazo y en el destroy…" | ⚠️ PASA — cubre 2 de las 3 rutas que el AC enumera (reemplazo, destroy); la ruta de eliminación revoca correctamente pero sin spy dedicado (mismo hallazgo que AC-03) |
| AC-08 | `canSubmit()` computed | "deshabilita Guardar cuando no hay ningún archivo seleccionado" | ✅ PASA |

## Tareas del spec (F-VER-02)

- Block 1: 5/5 completadas (NzUploadModule, signal `fileList`, `beforeUpload` como class field de flecha, template `<nz-upload>`, `DeleteOutline` en `app.config.ts`).
- Block 2: 4/4 completadas (revocación en reemplazo, `onUploadChange` como class field de flecha, `ngOnDestroy`, binding `(nzChange)`).
- `onFileSelected()` eliminado (confirmado, cero referencias).

## Reglas FAIL (sección 5 del catálogo)

| ID | Resultado |
|---|---|
| F-VER-01 | ✅ Los 8 AC tienen al menos un test que pasa |
| F-VER-02 | ✅ 9/9 tareas de los 2 bloques implementadas en el código real |
| F-VER-03 | ✅ Coverage sobre `create-mural-form.component.ts`: Statements 96% (72/75) · Branches 90.38% (47/52) · Functions 100% (18/18) · Lines 95.77% (68/71) — las 3 métricas ≥80% |
| F-VER-04 | ✅ Sad-path tests presentes (tipo inválido, tamaño excedido), no solo happy path |
| F-VER-05 | ✅ `tsc --build --noEmit` y `ng lint` sin errores |
| F-VER-06 | ⚠️ Los 9 tests requeridos por el spec (4 Block 1 + 5 Block 2) existen y pasan, mapeados 1:1 por propósito — pero el propio texto de "Final verification" del spec dice "8 tests… AC-01 a AC-08 cubiertos 1:1" cuando la suma real de sus bloques es 9 (el 9° cubre un edge case de manejo de errores, no un AC del PRD). Inconsistencia de conteo en el documento del spec, no de la implementación. |

## Reglas WARN (sección 5 del catálogo)

| ID | Resultado |
|---|---|
| W-VER-01 | ✅ Sin código muerto ni imports sin usar |
| W-VER-02 | ✅ Cobertura de lógica de negocio 90-96%, por encima del piso recomendado |
| W-VER-03 | ✅ Sin tests frágiles (`vi.restoreAllMocks()` en `afterEach`, sin estado compartido ni orden implícito) |

## Hallazgo a registrar (no bloqueante)

El test de eliminación ("limpia el estado cuando se elimina el archivo desde nz-upload-list") no incluye un `vi.spyOn(URL, 'revokeObjectURL')` para confirmar explícitamente que la ruta `onUploadChange` con `type: 'removed'` también revoca el `blob:` URL — a diferencia de los tests de reemplazo y destroy, que sí lo espían. El código usa la misma función privada `revokeCurrentThumbUrl()` en las tres rutas, así que no hay un bug funcional, pero AC-07 (que enumera explícitamente "se reemplaza, se quita, o el componente se destruye") no tiene sus 3 ramas confirmadas con la misma evidencia de test.

**Decisión del usuario:** aceptar como WARN documentado y avanzar a RELEASE, sin loop correctivo a CODE (2026-08-29).

## Suite completa del componente

14/14 tests en verde (`npx ng test --include "**/create-mural-form.component.spec.ts" --watch false`).

## Veredicto

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module FEAT-008 — PASSED                     │
├─────────────────────────────────────────────────────────┤
│  FAILs: 0 | WARNs: 3 | PASSes: 15                          │
│  Result: PASSED                                            │
└─────────────────────────────────────────────────────────┘
```
