# Spec FEAT-008: Reemplazar input file por NzFileUpload con loader y preview en el formulario de creación de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-008 |
| PRD | docs/daw/prd/prd-FEAT-008.md |
| Tier | FEATURE |
| Date | 2026-08-29 |
| Spec loops | 0 |

## Summary

Reemplaza el `<input type="file">` nativo de `create-mural-form.component` por `nz-upload`
(`NzUploadModule`, `nzListType="picture"`). El alta de archivo se maneja en `beforeUpload` (no en
`nzChange`): con `nzBeforeUpload` devolviendo `false` de forma síncrona, `nz-upload` nunca dispara
`onStart`/`nzChange` para el archivo nuevo (verificado contra el código fuente de
`ng-zorro-antd-upload.mjs`), así que el propio `beforeUpload` valida tipo/tamaño y arma manualmente
la entrada de `nzFileList` (con `thumbUrl` vía `URL.createObjectURL`, dando el preview inmediato). La
baja (quitar archivo) sí llega por `(nzChange)` con `type: 'removed'`, porque ese flujo del ícono de
eliminar es independiente del ciclo de subida. Esta asimetría es intencional y no un desvío del PRD
(hallazgo del arch-auditor en PLAN). `beforeUpload` y `onUploadChange` se implementan como class
fields de flecha, no como métodos, porque `nz-upload` los invoca con un `this` distinto al de la
instancia del componente.

Se divide en 2 bloques para que cada uno compile y sea verificable de forma independiente: Block 1
cubre alta + validación + preview inicial; Block 2 cubre reemplazo, eliminación y limpieza de
memoria (el binding `(nzChange)` y su handler viven enteros en Block 2, para no dejar a Block 1
referenciando un método que todavía no existe — hallazgo FAIL del arch-auditor en PLAN).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 |
| FR-02 | Block 1 |
| FR-03 | Block 1 |
| FR-04 | Block 1 |
| FR-05 | Block 2 |
| FR-06 | Block 2 |
| FR-07 | Block 1 (siempre retorna `false`; nunca se activa una subida real) |
| FR-08 | Block 2 (test de integración confirma que el comportamiento existente no cambió) |
| NFR-01 | Strategy: revocar `URL.revokeObjectURL` en reemplazo, en eliminación (`nzChange` 'removed') y en `ngOnDestroy` — implementado en Block 2 |
| AC-01 | Block 1 |
| AC-02 | Block 2 |
| AC-03 | Block 2 |
| AC-04 | Block 1 |
| AC-05 | Block 1 |
| AC-06 | Block 2 |
| AC-07 | Block 2 |
| AC-08 | Block 1 |

## Dependencies between blocks

Block 2 depende de Block 1 (extiende el mismo `beforeUpload` para revocar el `thumbUrl` anterior en
el reemplazo, y agrega el binding `(nzChange)` + su handler sobre el `<nz-upload>` que Block 1 ya
dejó en el template). Orden de ejecución: Block 1 → Block 2.

## Block 1 — Reemplazo del control, validación y preview inicial

**Files**
- `frontend/src/app/features/murals/ui/create-mural-form.component.ts` (modified) — agrega
  `NzUploadModule` a los `imports` standalone; agrega el signal `fileList =
  signal<NzUploadFile[]>([])`; agrega `readonly beforeUpload = (file: NzUploadFile): boolean =>
  {...}` (class field de flecha, NO método) que valida tipo/tamaño reutilizando las constantes
  existentes `ALLOWED_PHOTO_TYPES`/`MAX_PHOTO_SIZE_BYTES`, y si el archivo es válido arma
  manualmente una entrada `NzUploadFile` (`uid`, `name`, `status: 'done'`, `thumbUrl:
  URL.createObjectURL((file as any).originFileObj ?? file)`, `originFileObj`) reemplazando
  `fileList` completo (un solo elemento — el reemplazo de la URL anterior se hace en Block 2),
  sincroniza el signal existente `selectedFile`, limpia `fileError`; si es inválido, setea
  `fileError` y NO modifica `fileList`. SIEMPRE retorna `false` de forma síncrona (nunca deja pasar
  la subida real de `nz-upload`).
- `frontend/src/app/features/murals/ui/create-mural-form.component.html` (modified) — reemplaza
  `<input type="file" id="mural-photo" accept="image/jpeg,image/png,image/webp"
  (change)="onFileSelected($event)" data-testid="photo-input">` por:
  ```html
  <nz-upload
    nzListType="picture"
    [nzShowUploadList]="true"
    [nzBeforeUpload]="beforeUpload"
    [nzFileList]="fileList()"
    [nzMaxCount]="1"
    nzAccept="image/jpeg,image/png,image/webp"
    data-testid="photo-upload"
  >
    <button nz-button type="button">
      <span nz-icon nzType="cloud-upload"></span>
      Seleccionar imagen
    </button>
  </nz-upload>
  ```
  Sin binding `(nzChange)` todavía (se agrega recién en Block 2, junto con `onUploadChange`, para
  que este bloque compile solo).
- `frontend/src/app/app.config.ts` (modified) — agrega el import `DeleteOutline` desde
  `@ant-design/icons-angular/icons`, en orden alfabético entre `CompassOutline` y
  `EnvironmentOutline` (líneas 18-19 actuales), y lo suma al array de `provideNzIcons([...])` con un
  comentario indicando que lo requiere el ícono de eliminar de `nz-upload-list` (mismo patrón que el
  comentario ya existente para `CalendarOutline`).
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` (modified) — agrega
  `provideNzIcons([DeleteOutline])` (o `provideNzIconsTesting()`, siguiendo el patrón ya usado en
  `login-form.component.spec.ts`/`register-form.component.spec.ts`) al `TestBed`, porque
  `nz-upload-list` con `nzListType="picture"` renderiza el ícono de eliminar incondicionalmente
  aunque este bloque todavía no maneje el click.

**Logic**

`beforeUpload` es la única fuente de alta de archivo: valida, y si es válido arma el registro de
`nzFileList` y el `thumbUrl` con `URL.createObjectURL` en el mismo paso — esto es lo que le da a
`nz-upload` (con `nzListType="picture"`) todo lo que necesita para mostrar la miniatura, sin esperar
ningún evento de `nz-upload` que nunca va a llegar (`nzBeforeUpload` devolviendo `false` corta el
flujo de subida antes de `onStart`, que es el único punto que dispara `nzChange`).

**Input validation**

- Tipo: `ALLOWED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp']` contra `file.type` (ya
  existente, sin cambios en el criterio).
- Tamaño: `MAX_PHOTO_SIZE_BYTES = 10 * 1024 * 1024` contra `file.size` (ya existente, sin cambios en
  el criterio).
- Ambas validaciones son UX-only (documentado en el propio código y en el threat model,
  `docs/daw/security/threat-FEAT-008.md`, riesgo R1 — aceptado, backend es la autoridad real).

**Error handling**

- Archivo de tipo inválido → `fileError.set('El archivo debe ser una imagen JPEG, PNG o WebP.')`,
  `fileList` no se modifica, `beforeUpload` retorna `false`.
- Archivo de más de 10 MB → `fileError.set('El archivo no puede superar los 10 MB.')`, `fileList` no
  se modifica, `beforeUpload` retorna `false`.

**Required tests**

- [ ] Selecciona un archivo válido (JPEG ≤10MB) → `fileList()` tiene 1 entrada con `thumbUrl`
      definido, y el DOM muestra un `<img>` de preview — valida AC-01.
- [ ] Selecciona un archivo de más de 10MB → `fileError()` tiene el mensaje esperado, `fileList()`
      sigue vacío, no aparece ningún preview — valida AC-04.
- [ ] Selecciona un archivo de tipo `application/pdf` → `fileError()` tiene el mensaje esperado,
      `fileList()` sigue vacío — valida AC-05.
- [ ] Sin ningún archivo seleccionado, el botón "Guardar" (`data-testid="submit-button"`) tiene el
      atributo `disabled` — valida AC-08.

**Completion criterion**

Los 4 tests de este bloque pasan; `npx tsc --build --noEmit tsconfig.json` no reporta errores
nuevos; el componente compila y renderiza sin depender de ningún símbolo que Block 2 vaya a agregar.

## Block 2 — Reemplazo, eliminación y limpieza de memoria

**Files**
- `frontend/src/app/features/murals/ui/create-mural-form.component.ts` (modified) — extiende
  `beforeUpload` para revocar `URL.revokeObjectURL` del `thumbUrl` anterior (si `fileList()` ya
  tenía una entrada) antes de asignar la nueva; agrega `readonly onUploadChange = (event:
  NzUploadChangeParam): void => {...}` (class field de flecha) que, cuando `event.type === 'removed'`,
  revoca el `thumbUrl` de la entrada removida, y limpia `fileList`, `selectedFile` y `fileError` a su
  estado vacío; implementa `ngOnDestroy(): void` revocando el `thumbUrl` de la entrada actual de
  `fileList()` si existe (evita el leak si el usuario navega fuera del formulario con un archivo
  todavía seleccionado).
- `frontend/src/app/features/murals/ui/create-mural-form.component.html` (modified) — agrega
  `(nzChange)="onUploadChange($event)"` al `<nz-upload>` que Block 1 ya dejó en el template.
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` (modified) — agrega los
  tests de este bloque (abajo).

**Logic**

El reemplazo (elegir un archivo nuevo habiendo uno ya seleccionado) se resuelve dentro del mismo
`beforeUpload` de Block 1: al ser síncrono y reemplazar `fileList` completo, el paso adicional de
Block 2 es simplemente revocar la URL vieja antes de pisarla. La eliminación (click en el ícono de
`nz-upload-list`) es el único camino que sí pasa por `nzChange`, porque ese botón no depende del
ciclo de subida — dispara el flujo interno de remoción de `nz-upload` independientemente de que
`nzBeforeUpload` haya devuelto `false`.

**Error handling**

- No hay nuevos casos de error de usuario en este bloque (la validación ya vive en Block 1); el único
  caso a cubrir es la ausencia de una entrada previa al llamar a `ngOnDestroy` o al manejar
  `'removed'` sin archivo activo — en ambos casos el código debe ser un no-op seguro (verificar
  `fileList().length > 0` antes de revocar).

**Required tests**

- [ ] Selecciona un archivo válido, luego selecciona un segundo archivo válido distinto → `fileList()`
      tiene 1 sola entrada (la nueva), y `URL.revokeObjectURL` fue llamado con la URL del primer
      archivo (spy) — valida AC-02.
- [ ] Selecciona un archivo válido, hace click en el ícono de eliminar del `nz-upload-list` →
      `fileList()` vuelve a estar vacío, `selectedFile()` es `null`, el botón "Guardar" vuelve a
      quedar `disabled` — valida AC-03.
- [ ] Con un archivo válido seleccionado, se envía el formulario → el botón "Guardar" muestra
      `nzLoading` hasta que la promesa/observable del submit resuelve, y no se dispara ninguna
      petición HTTP de subida antes del submit explícito (se verifica que `nzCustomRequest`/`nzAction`
      nunca se invocan) — valida AC-06.
- [ ] Reemplaza el archivo y destruye el componente (`fixture.destroy()`) → `URL.revokeObjectURL` fue
      llamado tanto en el reemplazo como en el destroy, con las URLs correspondientes (spy) — valida
      AC-07.
- [ ] Destruye el componente (`fixture.destroy()`) sin haber seleccionado nunca un archivo → no lanza
      ninguna excepción y `URL.revokeObjectURL` NO es invocado — valida el manejo seguro sin entrada
      previa documentado en "Error handling" de este bloque.

**Completion criterion**

Los 4 tests de este bloque pasan además de los 4 de Block 1 (8/8 tests del componente en verde);
`npx tsc --build --noEmit tsconfig.json` limpio; ningún `URL.createObjectURL` queda sin su
`URL.revokeObjectURL` correspondiente en los escenarios de reemplazo/eliminación/destroy cubiertos
por los tests.

## Final verification

- Los 8 tests nuevos/actualizados de `create-mural-form.component.spec.ts` pasan (AC-01 a AC-08
  cubiertos 1:1).
- `npx tsc --build --noEmit tsconfig.json` sin errores.
- Lint (ESLint) sin nuevos warnings/errores en los 4 archivos tocados.
- Verificación manual: seleccionar una imagen JPEG/PNG/WebP válida muestra el preview de inmediato;
  seleccionar una imagen de más de 10MB o de un tipo no soportado muestra el mensaje de error sin
  preview; el botón de eliminar limpia el estado; el submit sigue funcionando exactamente igual que
  antes del cambio (mismo endpoint, mismo payload, mismo `nzLoading`).
