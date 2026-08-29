# PRD FEAT-008: Reemplazar input file por NzFileUpload con loader y preview en el formulario de creación de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-008 |
| Tracker | none |
| Date | 2026-08-29 |
| PRD loops | 0 |

## Context and Problem

El formulario de creación de mural (`create-mural-form.component.ts/.html`) usa hoy un
`<input type="file">` nativo, manejado imperativamente con un signal (`selectedFile`). Las
validaciones de tipo MIME (`image/jpeg`, `image/png`, `image/webp`) y de tamaño máximo (10 MB) ya
existen, pero son UX-only: se ejecutan en JS al recibir el `(change)` del input, sin ningún feedback
visual más allá de un `<nz-alert>` con el mensaje de error.

No hay preview de la imagen seleccionada: el usuario no ve qué foto eligió hasta después de guardar
el mural. El único indicador de carga existente (`nzLoading` en el botón "Guardar") aparece recién
al enviar el formulario completo (título + ubicación + foto), no durante la selección del archivo.

El proyecto ya tiene `ng-zorro-antd ^21.3.3` instalado, pero `NzUploadModule` no se usa en ningún
lugar todavía — sería la primera vez que se incorpora al proyecto.

## Goals

- Reemplazar el `<input type="file">` nativo por el componente `nz-upload` de ng-zorro
  (`NzUploadModule`), consistente con el resto del formulario (que ya usa `NzFormModule`,
  `NzInputModule`, `NzButtonModule`, `NzAlertModule`).
- Dar feedback visual inmediato de la imagen elegida (preview), sin esperar al submit.
- Conservar las validaciones de tipo y tamaño ya existentes, ahora integradas al flujo de
  `nz-upload` en vez de un handler manual de `(change)`.
- No alterar el comportamiento de envío del formulario: el archivo se sigue enviando junto con
  título y ubicación al hacer submit, no se auto-sube al seleccionarlo.

## Functional Requirements

- FR-01: El sistema debe reemplazar el `<input type="file">` nativo por el componente `nz-upload`
  (`NzUploadModule`) en el formulario de creación de mural.
- FR-02: El sistema debe restringir la selección de archivos en `nz-upload` a los tipos
  `image/jpeg`, `image/png` e `image/webp` (vía `nzAccept` y validación explícita del tipo real del
  archivo, no solo del filtro del selector del navegador).
- FR-03: El sistema debe rechazar archivos de más de 10 MB antes de agregarlos a la lista de
  `nz-upload`, sin llegar a mostrarlos como seleccionados.
- FR-04: El sistema debe mostrar un preview en miniatura de la imagen seleccionada de forma
  inmediata, sin esperar al envío del formulario.
- FR-05: El sistema debe permitir reemplazar el archivo seleccionado eligiendo uno nuevo, lo que
  descarta el preview y el archivo anteriores.
- FR-06: El sistema debe permitir quitar el archivo seleccionado antes de enviar el formulario (vía
  el ícono de eliminar propio de `nz-upload`), volviendo el control a su estado vacío.
- FR-07: El sistema debe mantener el `nz-upload` en modo de selección local (sin auto-subida al
  seleccionar): el archivo se conserva en el estado del componente y se envía recién al hacer submit
  del formulario completo, igual que hoy.
- FR-08: El sistema debe conservar el indicador de carga existente (`nzLoading`) en el botón
  "Guardar" mientras se procesa el submit del formulario.

## Non-Functional Requirements

- NFR-01: La generación del preview debe hacerse con `URL.createObjectURL` (u opción equivalente
  sin conversión a base64 completa), y la URL creada debe revocarse (`URL.revokeObjectURL`) al
  reemplazar el archivo, quitarlo, o destruir el componente, para no generar memory leaks.

## Acceptance Criteria

- AC-01: WHEN el usuario selecciona un archivo de imagen válido (JPEG, PNG o WebP, ≤10 MB), THE
  sistema SHALL mostrar de inmediato un preview en miniatura de esa imagen usando el componente
  `nz-upload` (valida FR-01, FR-04).
- AC-02: WHEN el usuario selecciona un archivo nuevo habiendo ya uno seleccionado, THE sistema SHALL
  reemplazar el preview y el archivo seleccionado por el nuevo (valida FR-05).
- AC-03: WHEN el usuario hace click en el ícono de eliminar del archivo seleccionado, THE sistema
  SHALL limpiar el preview y devolver el control de carga a su estado vacío (valida FR-06).
- AC-04: IF el usuario selecciona un archivo de más de 10 MB, THEN THE sistema SHALL rechazarlo,
  mostrar un mensaje de error explícito, y no agregarlo al preview ni a la lista de `nz-upload`
  (valida FR-03).
- AC-05: IF el usuario selecciona un archivo de un tipo distinto a JPEG, PNG o WebP, THEN THE
  sistema SHALL rechazarlo, mostrar un mensaje de error explícito, y no agregarlo al preview ni a la
  lista de `nz-upload` (valida FR-02).
- AC-06: WHEN se envía el formulario con una imagen válida seleccionada, THE sistema SHALL mostrar
  el indicador de carga del botón "Guardar" hasta que la petición termine, sin haber subido el
  archivo por su cuenta antes del submit (valida FR-07, FR-08).
- AC-07: WHEN el archivo seleccionado se reemplaza, se quita, o el componente se destruye, THE
  sistema SHALL revocar cualquier object URL creado para el preview anterior (valida NFR-01).
- AC-08: WHERE no hay ningún archivo seleccionado todavía, THE sistema SHALL mantener deshabilitado
  el botón "Guardar" (comportamiento actual de `canSubmit()` sin cambios, valida FR-01).

## Out of Scope

- Selección o envío de múltiples fotos por mural (sigue siendo una sola foto por mural).
- Zona de drag-and-drop: el alcance cubre el click sobre el control estándar de `nz-upload`, no un
  área de arrastrar-y-soltar dedicada.
- Cambios a la validación del backend, al pipeline de NSFW, o al límite de 10 MB en sí (RNF-003 de
  `AGENTS.md` no cambia — el backend sigue siendo la autoridad real).
- Recorte, rotación o edición de la imagen antes de subirla.

## Risks and Mitigations

- **Riesgo:** el comportamiento por defecto de `nz-upload` es auto-subir el archivo a una URL al
  seleccionarlo, lo cual rompería el flujo actual (el archivo se envía junto con título y ubicación
  recién al hacer submit).
  **Mitigación:** usar `[nzBeforeUpload]` devolviendo `false` (o `nzCustomRequest` sin efecto) para
  que `nz-upload` solo administre la selección local del archivo, sin disparar ninguna subida por su
  cuenta.
- **Riesgo:** perder el modelo reactivo actual basado en el signal `selectedFile` al migrar al
  modelo de lista de archivos propio de `nz-upload`.
  **Mitigación:** mantener `selectedFile` (o un signal equivalente) como fuente de verdad única,
  sincronizado desde el evento `(nzChange)` de `nz-upload`.
- **Riesgo:** memory leak por object URLs de preview no revocadas.
  **Mitigación:** revocar explícitamente en reemplazo, remoción y `ngOnDestroy` (cubierto por
  NFR-01 / AC-07).

## Dependencies

- `NzUploadModule` de `ng-zorro-antd` (ya instalado en `^21.3.3`, sin nueva dependencia a agregar).
- Estado actual del componente (`create-mural-form.component.ts`, basado en signals de Angular).
