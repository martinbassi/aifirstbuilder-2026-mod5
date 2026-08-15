# PRD-001: Paretto — PWA para documentar y descubrir murales urbanos geolocalizados

## Contexto y Problema

Las frases, grafitis artísticos y murales urbanos suelen ser efímeros. Muchas personas encuentran obras interesantes en la calle, pero no existe una forma simple de documentarlas y descubrir otras cercanas creadas o registradas por la comunidad.

### 1. Solución

Una aplicación web (PWA) donde los usuarios pueden:

* Subir una fotografía de un mural o frase urbana.
* Registrar la ubicación donde fue encontrada.
* Explorar murales cercanos cargados por otros usuarios.
* Visualizar los murales en un mapa.

### 2. Usuarios

* Usuario Explorador: Persona interesada en arte urbano, fotografía o turismo local.
* Usuario Colaborador: Persona que desea registrar y compartir murales encontrados en la ciudad.
* Usuario Administrador: Persona responsable de moderar murales pendientes y reportados.

### 3. Alcance del MVP

* Registro de murales.
* Foto.
* Geolocalización (GPS o coordenadas manuales).
* Mapa de murales cercanos.
* Lista de murales cercanos.
* Visualización de detalle.
* Registro y autenticación de usuarios (usuario/contraseña y Google, con vinculación automática por email), y reseteo de contraseña.
* Cierre de sesión.
* Gestión de murales propios: listado con estado y eliminación.
* Manejo de casos límite: permisos de ubicación denegados, imágenes inválidas, fallos de guardado y ausencia de resultados cercanos.
* Pantalla de entrada de la app (ruta raíz) definida explícitamente según el estado de sesión del usuario (login / exploración de murales / moderación).
* **Moderación mínima de contenido:**
  * Estado "pendiente" para murales recién subidos, no visibles públicamente hasta ser aprobados o validados.
  * Estado "rechazado" para murales bloqueados automáticamente (NSFW) o rechazados/despublicados por un administrador.
  * Reporte de murales por parte de los usuarios.
  * Validación automática básica para bloquear contenido NSFW antes de su publicación.
  * Listado de murales pendientes y reportados, y las acciones de aprobar/rechazar/despublicar, disponibles para el administrador mediante una interfaz simple de moderación (sin analíticas, gestión de usuarios/roles ni auditoría — ver "Fuera de Alcance").

---

## Objetivos

Permitir que una persona capture un mural y que otra persona pueda descubrirlo cerca de su ubicación.

---

## Requerimientos Funcionales

### RF-001 Crear mural

El sistema debe permitir subir una fotografía de un mural.

### RF-002 Registrar ubicación automática

El sistema debe permitir asociar la ubicación GPS actual al mural.

### RF-003 Registrar ubicación manual

El sistema debe permitir ingresar manualmente las coordenadas de ubicación (latitud/longitud).

### RF-004 Guardar mural

El sistema debe almacenar fotografía, ubicación, fecha de creación y estado inicial "pendiente".

### RF-005 Buscar murales cercanos

El sistema debe mostrar murales ubicados dentro de un radio configurable alrededor del usuario.

### RF-006 Mostrar mapa

El sistema debe mostrar marcadores de murales sobre un mapa.

### RF-007 Ver detalle de mural

El sistema debe mostrar la fotografía, fecha y ubicación de un mural seleccionado.

### RF-008 Ordenar por cercanía

El sistema debe ordenar los resultados por distancia al usuario.

### RF-009 Detectar denegación de permisos de ubicación

El sistema debe detectar cuándo el usuario no otorga permisos de geolocalización.

### RF-010 Rechazar imagen inválida

El sistema debe rechazar imágenes que superen los 10 MB o no estén en un formato soportado (JPEG, PNG o WebP).

### RF-011 Detectar fallo en el guardado

El sistema debe detectar errores ocurridos durante el proceso de guardado del mural (fotografía, ubicación o metadatos).

### RF-012 Informar ausencia de resultados cercanos

El sistema debe informar al usuario cuando no existan murales dentro del radio de búsqueda configurado.

### RF-013 Estado "pendiente" de publicación

El sistema debe marcar todo mural recién creado como "pendiente" y ocultarlo de las búsquedas y el mapa públicos hasta que sea validado por un usuario administrador.

### RF-014 Reportar mural

El sistema debe permitir a cualquier usuario reportar un mural publicado por contenido inapropiado, duplicado o ubicación incorrecta.

### RF-015 Validación automática básica de contenido NSFW

El sistema debe ejecutar una validación automática sobre cada imagen subida y cambiar el mural al estado "rechazado" si se detecta contenido NSFW, sin publicarlo.

### RF-016 Ofrecer ingreso manual de ubicación al denegar permisos

El sistema debe ofrecer la opción de ingreso manual de ubicación cuando el usuario deniega los permisos de geolocalización, sin interrumpir el flujo de registro.

### RF-017 Notificar motivo de rechazo de imagen

El sistema debe mostrar al usuario un mensaje indicando el motivo por el cual se rechazó la imagen (tamaño superior a 10 MB o formato no soportado).

### RF-018 Bloquear continuación con imagen inválida

El sistema debe impedir que el usuario continúe el registro del mural mientras la imagen adjunta no sea válida.

### RF-019 Notificar error de guardado

El sistema debe notificar al usuario cuando ocurre un error durante el proceso de guardado del mural.

### RF-020 Preservar datos al fallar el guardado

El sistema debe preservar la fotografía y ubicación ya ingresadas cuando ocurre un error en el guardado.

### RF-021 Sugerir ampliar radio de búsqueda

El sistema debe sugerir al usuario ampliar el radio de búsqueda cuando no existan murales en el radio actual.

### RF-022 Autenticación con usuario y contraseña

El sistema debe permitir al usuario autenticarse con usuario y contraseña.

### RF-023 Autenticación con Google

El sistema debe permitir al usuario autenticarse mediante su cuenta de Google.

### RF-024 Habilitar reintento de guardado

El sistema debe permitir al usuario reintentar el guardado del mural utilizando la fotografía y la ubicación ya ingresadas, sin necesidad de volver a cargarlas.

### RF-025 Aprobar mural pendiente

El sistema debe permitir a un usuario administrador cambiar el estado de un mural de "pendiente" a "publicado".

### RF-026 Requerir autenticación para crear mural

El sistema debe requerir que el usuario esté autenticado para acceder a la funcionalidad de creación de murales.

### RF-027 Rechazar mural pendiente

El sistema debe permitir a un usuario administrador cambiar el estado de un mural de "pendiente" a "rechazado".

### RF-028 Despublicar mural reportado

El sistema debe permitir a un usuario administrador cambiar el estado de un mural de "publicado" a "rechazado".

### RF-029 Listar murales pendientes

El sistema debe permitir a un usuario administrador obtener el listado de murales en estado "pendiente".

### RF-030 Listar murales reportados

El sistema debe permitir a un usuario administrador obtener el listado de murales publicados con al menos un reporte activo.

### RF-031 Ver murales propios

El sistema debe permitir a un usuario colaborador ver el listado de los murales que subió, incluyendo su estado actual (pendiente, publicado o rechazado).

### RF-032 Eliminar mural propio

El sistema debe permitir a un usuario colaborador eliminar un mural que él mismo subió.

### RF-033 Cerrar sesión

El sistema debe permitir al usuario cerrar su sesión activa.

### RF-034 Vincular cuentas por email compartido

El sistema debe asociar automáticamente un inicio de sesión con Google a una cuenta existente de usuario/contraseña cuando ambos compartan la misma dirección de email.

### RF-035 Detectar idioma del navegador

El sistema debe determinar el idioma de la interfaz a partir del idioma configurado en el navegador del usuario, utilizando español como idioma por defecto cuando el navegador esté configurado en un idioma no soportado.

### RF-036 Solicitar reseteo de contraseña

El sistema debe permitir a un usuario solicitar el reseteo de su contraseña ingresando el email asociado a su cuenta, y enviar a ese email un enlace de reseteo de un solo uso.

### RF-037 Establecer nueva contraseña

El sistema debe permitir al usuario establecer una nueva contraseña utilizando un enlace de reseteo válido.

### RF-038 Expirar enlace de reseteo por tiempo

El sistema debe invalidar el enlace de reseteo de contraseña transcurrida 1 hora desde su generación.

### RF-039 Invalidar enlace de reseteo tras su uso

El sistema debe invalidar el enlace de reseteo de contraseña inmediatamente después de haber sido utilizado.

### RF-040 Registrar cuenta con usuario y contraseña

El sistema debe permitir crear una cuenta nueva ingresando un nombre de usuario, contraseña y email.

### RF-050 Definir la pantalla de entrada de la aplicación

El sistema debe definir explícitamente qué pantalla se muestra al abrir la aplicación (ruta raíz), según el estado de sesión del usuario: la pantalla de inicio de sesión si no hay sesión activa, la pantalla de exploración de murales (mapa/lista, RF-005 a RF-008) si el usuario tiene sesión con rol Colaborador/Explorador, y la pantalla de moderación si tiene sesión con rol Administrador. Esto no restringe el acceso directo a la pantalla de exploración de murales para un visitante sin sesión (no requiere autenticación, ver "Alcance del MVP"); solo define cuál es la pantalla *por defecto* al entrar.

### RF-051 Conflicto de concurrencia en moderación

El sistema debe rechazar con un error explícito de conflicto cualquier acción de moderación (aprobar, rechazar o despublicar) sobre un mural cuyo estado ya cambió respecto al esperado por la solicitud, en vez de aplicarla silenciosamente.

### RF-052 Bloqueo temporal de login tras intentos fallidos

El sistema debe bloquear temporalmente los intentos de inicio de sesión con usuario/contraseña sobre una cuenta tras varios intentos fallidos consecutivos, para mitigar ataques de fuerza bruta.

### RF-053 Mural pendiente ante fallo de validación NSFW

El sistema debe mantener un mural en estado "pendiente" (sin publicarlo automáticamente ni bloquear el flujo de creación) cuando la validación automática de contenido NSFW falla o no responde, quedando disponible para revisión manual de un administrador.

### RF-054 Límite máximo de longitud de contraseña

El sistema debe aceptar contraseñas de hasta 128 caracteres, rechazando en el registro y en el reseteo de contraseña cualquier valor que supere ese máximo.

### RF-055 Invalidar sesiones activas tras reseteo de contraseña

El sistema debe invalidar todas las sesiones activas de una cuenta al completarse el reseteo de su contraseña, exigiendo un nuevo inicio de sesión en cualquier otro dispositivo.

### RF-056 Reintento de envío del email de reseteo

El sistema debe reintentar automáticamente el envío del email de reseteo de contraseña ante un fallo transitorio del proveedor de envío, mostrando un mensaje de error visible al usuario solo si el envío no puede completarse tras los reintentos.

### RF-057 Límite de solicitudes de reseteo de contraseña

El sistema debe limitar la cantidad de solicitudes de reseteo de contraseña que puede generar un mismo email en un período de tiempo, para mitigar el envío masivo de emails como vector de abuso.

---

## Requerimientos No Funcionales

### RNF-001 Tiempo de carga

La lista de murales cercanos debe mostrarse en menos de 3 segundos para el 95% de las consultas.

### RNF-002 Disponibilidad

La aplicación debe mantener una disponibilidad mensual mínima del 99%.

### RNF-003 Tamaño máximo de imagen

La aplicación debe aceptar imágenes de hasta 10 MB en formato JPEG, PNG o WebP.

### RNF-004 Precisión geográfica

La ubicación registrada debe tener una precisión igual o mejor a 50 metros cuando se utilice GPS.

### RNF-005 Internacionalización

La aplicación debe soportar español, inglés y portugués, con el 100% de los textos de la interfaz traducidos en los tres idiomas. El idioma se selecciona automáticamente según el navegador del usuario (RF-035), usando español como idioma por defecto.

### RNF-006 Seguridad de autenticación

El sistema debe almacenar las contraseñas de los usuarios mediante un algoritmo de hashing (nunca en texto plano), servir la aplicación exclusivamente sobre HTTPS, expirar la sesión del usuario a los 7 días de haber iniciado sesión, y exigir que las contraseñas tengan al menos 8 caracteres incluyendo letras y números.

### RNF-007 Instalabilidad y comportamiento offline

La aplicación debe ser instalable como PWA y debe mostrar la interfaz base (shell) en menos de 2 segundos cuando el usuario no tiene conexión a internet, usando el cache del service worker, sin requerir acceso a datos de murales.

### RNF-008 Accesibilidad

La aplicación debe cumplir con el nivel AA de las pautas WCAG 2.1.

### RNF-009 Almacenamiento privado de fotografías

El almacenamiento de imágenes (Azure Storage) debe configurarse como privado, sin acceso público anónimo por URL directa, independientemente del estado del mural. El sistema debe servir cada fotografía mediante una URL de acceso temporal firmada (SAS o equivalente), generada en el momento de la respuesta y de corta duración, respetando la misma visibilidad que ya aplica al mural (RF-013: un mural "pendiente" o "rechazado" solo debe ser visible por su dueño o un Administrador — eso incluye su fotografía, no solo su aparición en búsquedas y mapa).

---

## Criterios de Aceptación

### AC-01 (RF-001): Subida exitosa

**Dado** que el usuario se encuentra en la pantalla de creación de mural / **Cuando** selecciona una fotografía válida / **Entonces** el sistema debe permitir continuar con el registro

---

### AC-02 (RF-002): Uso de GPS

**Dado** que el usuario otorgó permisos de ubicación / **Cuando** crea un mural / **Entonces** el sistema debe asociar la ubicación actual al registro

---

### AC-03 (RF-003): Ingreso manual exitoso

**Dado** que el usuario se encuentra registrando un mural / **Cuando** ingresa manualmente coordenadas de latitud y longitud válidas / **Entonces** el sistema debe asociar esa ubicación al mural

---

### AC-04 (RF-004): Registro exitoso

**Dado** que existe una fotografía válida y una ubicación válida / **Cuando** el usuario presiona "Guardar" / **Entonces** el sistema debe almacenar el mural con su fotografía, ubicación, fecha de creación y estado inicial "pendiente" en la base de datos

---

### AC-05 (RF-005): Búsqueda dentro de radio

**Dado** que existen murales registrados y el usuario comparte su ubicación / **Cuando** solicita murales cercanos sin modificar el radio de búsqueda / **Entonces** el sistema debe mostrar únicamente murales ubicados dentro de los 5 kilómetros configurados por defecto

---

### AC-06 (RF-006): Visualización de marcadores

**Dado** que existen murales publicados dentro del área visible del mapa / **Cuando** el usuario abre la vista de mapa / **Entonces** el sistema debe mostrar un marcador por cada mural en su ubicación correspondiente

---

### AC-07 (RF-007): Consulta de detalle

**Dado** que existe un mural publicado / **Cuando** el usuario lo selecciona desde la lista o el mapa / **Entonces** el sistema debe mostrar su fotografía, fecha de creación y ubicación

---

### AC-08 (RF-008): Resultados ordenados

**Dado** que existen múltiples murales cercanos / **Cuando** se muestran los resultados / **Entonces** los murales deben aparecer ordenados de menor a mayor distancia

---

### AC-09 (RF-009): Detección de denegación de permisos

**Dado** que el usuario está creando un mural / **Cuando** rechaza el permiso de geolocalización / **Entonces** el sistema debe detectar la denegación pero no interrumpir el flujo de registro

---

### AC-10 (RF-010): Rechazo de imagen inválida

**Dado** que el usuario intenta subir una imagen / **Cuando** el archivo supera los 10 MB o no está en formato JPEG, PNG o WebP / **Entonces** el sistema debe rechazar la imagen

---

### AC-11 (RF-011): Detección de fallo en el guardado

**Dado** que el servicio de almacenamiento no está disponible / **Cuando** el usuario intenta guardar un mural con fotografía y ubicación válidas / **Entonces** el sistema debe detectar el error y no registrar el mural como guardado exitosamente

---

### AC-12 (RF-012): Sin resultados cercanos

**Dado** que el usuario solicita murales cercanos / **Cuando** no existen murales dentro del radio configurado / **Entonces** el sistema debe mostrar un mensaje informando que no se encontraron resultados

---

### AC-13 (RF-013): Estado pendiente

**Dado** que un usuario guarda un nuevo mural / **Cuando** el registro se completa exitosamente / **Entonces** el mural debe quedar en estado "pendiente" y no debe aparecer en el mapa ni en las búsquedas públicas hasta ser validado

---

### AC-14 (RF-014): Reporte exitoso

**Dado** que existe un mural publicado / **Cuando** un usuario lo reporta por contenido inapropiado, duplicado o ubicación incorrecta / **Entonces** el sistema debe registrar el reporte y notificar que el mural será revisado

---

### AC-15 (RF-015): Imagen bloqueada por contenido NSFW

**Dado** que un usuario sube una fotografía / **Cuando** la validación automática detecta contenido NSFW / **Entonces** el sistema debe cambiar el mural al estado "rechazado", impedir su publicación y notificar al usuario que el contenido fue rechazado

---

### AC-16 (RF-016): Alternativa manual al denegar permisos

**Dado** que el sistema detectó la denegación de permisos de geolocalización / **Cuando** el usuario continúa con el registro del mural / **Entonces** el sistema debe presentar el campo de ingreso manual de ubicación como alternativa

---

### AC-17 (RF-017): Mensaje de motivo de rechazo

**Dado** que el usuario intentó subir una imagen inválida / **Cuando** el sistema rechaza la imagen / **Entonces** el sistema debe mostrar un mensaje indicando si el motivo fue el tamaño (supera los 10 MB) o el formato no soportado

---

### AC-18 (RF-018): Bloqueo con imagen inválida

**Dado** que el usuario adjuntó una imagen que fue rechazada / **Cuando** intenta avanzar en el registro del mural / **Entonces** el sistema debe impedir continuar hasta que se cargue una imagen válida

---

### AC-19 (RF-019): Notificación de error de guardado

**Dado** que ocurrió un error durante el proceso de guardado del mural / **Cuando** el sistema detecta el fallo / **Entonces** el sistema debe mostrar un mensaje de error visible al usuario

---

### AC-20 (RF-020): Preservación de datos tras error de guardado

**Dado** que el usuario completó fotografía y ubicación válidas / **Cuando** ocurre un error al guardar el mural / **Entonces** el sistema debe mantener la fotografía y la ubicación ingresadas disponibles

---

### AC-21 (RF-021): Sugerencia de ampliar radio

**Dado** que el sistema informó que no existen murales dentro del radio configurado / **Cuando** el usuario visualiza el mensaje de ausencia de resultados / **Entonces** el sistema debe ofrecer la opción de ampliar el radio de búsqueda

---

### AC-22 (RF-022): Login con usuario y contraseña

**Dado** que el usuario tiene una cuenta registrada / **Cuando** ingresa su usuario y contraseña correctos / **Entonces** el sistema debe autenticarlo y permitirle acceder a las funcionalidades de la aplicación

---

### AC-23 (RF-023): Login con Google

**Dado** que el usuario tiene una cuenta de Google / **Cuando** selecciona la opción de iniciar sesión con Google y otorga los permisos requeridos / **Entonces** el sistema debe autenticarlo y permitirle acceder a la aplicación

---

### AC-24 (RF-025): Control de acceso a aprobación de murales

**Dado** que existe un mural en estado "pendiente" / **Cuando** un usuario sin rol de administrador intenta cambiar su estado a "publicado" / **Entonces** el sistema debe rechazar la acción y mantener el mural en estado "pendiente"

---

### AC-25 (RF-024): Reintento sin recargar datos

**Dado** que ocurrió un error de guardado y el usuario retiene fotografía y ubicación válidas / **Cuando** vuelve a presionar "Guardar" / **Entonces** el sistema debe intentar el guardado sin solicitar nuevamente la fotografía ni la ubicación

---

### AC-26 (RF-025): Aprobación de mural por administrador

**Dado** que existe un mural en estado "pendiente" / **Cuando** un usuario administrador lo aprueba / **Entonces** el sistema debe cambiar su estado a "publicado"

---

### AC-27 (RF-026): Creación bloqueada para usuario no autenticado

**Dado** que el usuario no ha iniciado sesión / **Cuando** intenta acceder a la funcionalidad de creación de mural / **Entonces** el sistema debe redirigirlo al flujo de autenticación

---

### AC-28 (RF-027): Rechazo de mural pendiente por administrador

**Dado** que existe un mural en estado "pendiente" / **Cuando** un usuario administrador lo rechaza / **Entonces** el sistema debe cambiar su estado a "rechazado"

---

### AC-29 (RF-027): Control de acceso a rechazo de murales

**Dado** que existe un mural en estado "pendiente" / **Cuando** un usuario sin rol de administrador intenta cambiar su estado a "rechazado" / **Entonces** el sistema debe rechazar la acción y mantener el mural en estado "pendiente"

---

### AC-30 (RF-028): Despublicación de mural reportado

**Dado** que existe un mural publicado con al menos un reporte / **Cuando** un usuario administrador lo despublica / **Entonces** el sistema debe cambiar su estado a "rechazado" y dejar de mostrarlo en el mapa y las búsquedas públicas

---

### AC-31 (RF-029): Listado de murales pendientes

**Dado** que existen murales en estado "pendiente" / **Cuando** un usuario administrador solicita el listado de murales pendientes / **Entonces** el sistema debe devolver todos los murales en ese estado

---

### AC-32 (RF-030): Listado de murales reportados

**Dado** que existen murales publicados con al menos un reporte / **Cuando** un usuario administrador solicita el listado de murales reportados / **Entonces** el sistema debe devolver todos los murales con al menos un reporte activo

---

### AC-33 (RF-031): Ver murales propios

**Dado** que un usuario colaborador subió al menos un mural / **Cuando** accede a la sección "mis murales" / **Entonces** el sistema debe mostrar todos los murales que subió junto con su estado actual (pendiente, publicado o rechazado)

---

### AC-34 (RF-031): Aislamiento de datos entre usuarios en "mis murales"

**Dado** que un usuario colaborador tiene murales en estado "pendiente" o "rechazado" / **Cuando** un usuario distinto accede a la sección "mis murales" / **Entonces** el sistema no debe mostrarle los murales pendientes ni rechazados del primer usuario

---

### AC-35 (RF-032): Eliminación de mural propio

**Dado** que un usuario colaborador es dueño de un mural / **Cuando** lo elimina / **Entonces** el sistema debe quitarlo de las búsquedas, el mapa y de "mis murales"

---

### AC-36 (RF-032): Control de acceso a eliminación de murales

**Dado** que existe un mural subido por un usuario / **Cuando** un usuario distinto al dueño intenta eliminarlo / **Entonces** el sistema debe rechazar la acción y mantener el mural sin cambios

---

### AC-37 (RF-033): Cierre de sesión

**Dado** que un usuario tiene una sesión activa / **Cuando** cierra sesión / **Entonces** el sistema debe invalidar su sesión y requerir autenticación nuevamente para acceder a funcionalidades protegidas

---

### AC-38 (RF-034): Vinculación automática de cuentas

**Dado** que existe una cuenta registrada con usuario/contraseña asociada a un email / **Cuando** esa misma persona inicia sesión con Google usando el mismo email / **Entonces** el sistema debe autenticarla en la cuenta existente sin crear una cuenta duplicada

---

### AC-39 (RF-035): Idioma por defecto ante idioma no soportado

**Dado** que el navegador del usuario está configurado en un idioma distinto de español, inglés o portugués / **Cuando** el usuario accede a la aplicación / **Entonces** el sistema debe mostrar la interfaz en español

---

### AC-40 (RF-035): Idioma según navegador

**Dado** que el navegador del usuario está configurado en inglés o portugués / **Cuando** el usuario accede a la aplicación / **Entonces** el sistema debe mostrar la interfaz en ese idioma

---

### AC-41 (RF-036): Solicitud de reseteo con email registrado

**Dado** que el usuario ingresa un email asociado a una cuenta existente / **Cuando** solicita el reseteo de contraseña / **Entonces** el sistema debe enviar a ese email un enlace de reseteo de un solo uso

---

### AC-42 (RF-036): Confirmación uniforme ante email no registrado

**Dado** que el usuario ingresa un email que no está asociado a ninguna cuenta / **Cuando** solicita el reseteo de contraseña / **Entonces** el sistema debe mostrar el mismo mensaje de confirmación que si el email existiera, sin enviar ningún enlace

---

### AC-43 (RF-037): Reseteo exitoso

**Dado** que el usuario accede a un enlace de reseteo válido y no utilizado / **Cuando** ingresa una nueva contraseña / **Entonces** el sistema debe actualizar la contraseña de la cuenta y permitir el login con la nueva contraseña

---

### AC-44 (RF-038): Enlace expirado por tiempo

**Dado** que el usuario accede a un enlace de reseteo generado hace más de 1 hora / **Cuando** intenta establecer una nueva contraseña / **Entonces** el sistema debe rechazar la operación e informar que el enlace expiró

---

### AC-45 (RF-039): Enlace inválido tras su uso

**Dado** que un enlace de reseteo ya fue utilizado para establecer una nueva contraseña / **Cuando** el usuario intenta usar el mismo enlace nuevamente / **Entonces** el sistema debe rechazar la operación e informar que el enlace ya no es válido

---

### AC-46 (RF-040): Registro exitoso

**Dado** que un visitante no tiene una cuenta / **Cuando** completa el registro con un nombre de usuario, contraseña y email no utilizados previamente / **Entonces** el sistema debe crear la cuenta y permitir iniciar sesión con esas credenciales

---

### AC-47 (RF-040): Rechazo por email duplicado

**Dado** que ya existe una cuenta registrada con un email / **Cuando** un visitante intenta registrarse utilizando ese mismo email / **Entonces** el sistema debe rechazar el registro e informar que el email ya está en uso

---

### AC-48 (RF-040): Rechazo por nombre de usuario duplicado

**Dado** que ya existe una cuenta registrada con un nombre de usuario / **Cuando** un visitante intenta registrarse utilizando ese mismo nombre de usuario / **Entonces** el sistema debe rechazar el registro e informar que el usuario ya está en uso

---

### AC-49 (RF-040): Rechazo por contraseña débil

**Dado** que un visitante intenta registrarse / **Cuando** la contraseña ingresada tiene menos de 8 caracteres o no incluye letras y números / **Entonces** el sistema debe rechazar el registro e informar el motivo

---

### AC-50 (RF-028): Control de acceso a despublicación de murales

**Dado** que existe un mural publicado con al menos un reporte / **Cuando** un usuario sin rol de administrador intenta cambiar su estado a "rechazado" / **Entonces** el sistema debe rechazar la acción y mantener el mural en estado "publicado"

---

### AC-51 (RF-029): Control de acceso a listado de murales pendientes

**Dado** que existen murales en estado "pendiente" / **Cuando** un usuario sin rol de administrador solicita el listado de murales pendientes / **Entonces** el sistema debe rechazar la solicitud

---

### AC-52 (RF-030): Control de acceso a listado de murales reportados

**Dado** que existen murales publicados con al menos un reporte / **Cuando** un usuario sin rol de administrador solicita el listado de murales reportados / **Entonces** el sistema debe rechazar la solicitud

---

### AC-53 (RF-050): Pantalla de entrada sin sesión

**Dado** que un usuario no tiene sesión activa / **Cuando** abre la aplicación / **Entonces** el sistema debe mostrarle la pantalla de inicio de sesión

---

### AC-54 (RF-050): Pantalla de entrada con sesión estándar

**Dado** que un usuario tiene sesión activa con rol Colaborador/Explorador / **Cuando** abre la aplicación / **Entonces** el sistema debe mostrarle la pantalla de exploración de murales (mapa/lista)

---

### AC-55 (RF-050): Pantalla de entrada con sesión de administrador

**Dado** que un usuario tiene sesión activa con rol Administrador / **Cuando** abre la aplicación / **Entonces** el sistema debe mostrarle la pantalla de moderación

---

### AC-56 (RF-051): Conflicto de concurrencia en moderación

**Dado** que dos administradores intentan moderar el mismo mural pendiente casi al mismo tiempo / **Cuando** ambas acciones se envían / **Entonces** el sistema aplica la primera que se completa y rechaza la segunda con un error explícito de conflicto

---

### AC-57 (RF-052): Bloqueo tras intentos fallidos de login

**Dado** que el usuario ingresó su contraseña incorrectamente varias veces seguidas / **Cuando** excede el límite de intentos fallidos / **Entonces** el sistema bloquea temporalmente los intentos de login sobre esa cuenta, incluso con la contraseña correcta

---

### AC-58 (RF-053): Mural pendiente ante fallo de validación NSFW

**Dado** que un usuario sube una fotografía / **Cuando** la validación automática de NSFW falla o no responde / **Entonces** el sistema mantiene el mural en estado "pendiente" para revisión manual de un administrador, sin publicarlo ni bloquear el flujo

---

### AC-59 (RF-054): Rechazo por contraseña demasiado larga

**Dado** que un usuario se registra o establece una nueva contraseña / **Cuando** la contraseña ingresada supera los 128 caracteres / **Entonces** el sistema rechaza la operación e informa el motivo

---

### AC-60 (RF-055): Invalidación de sesiones tras reseteo de contraseña

**Dado** que un usuario establece una nueva contraseña mediante un enlace de reseteo válido / **Cuando** la operación se completa / **Entonces** el sistema invalida todas las demás sesiones activas de esa cuenta

---

### AC-61 (RF-056): Reintento de envío del email de reseteo

**Dado** que el envío del email de reseteo falla de forma transitoria / **Cuando** el sistema lo detecta / **Entonces** reintenta el envío automáticamente y solo muestra un error visible al usuario si el envío no puede completarse tras los reintentos

---

### AC-62 (RF-057): Límite de solicitudes de reseteo de contraseña

**Dado** que un mismo email ya generó varias solicitudes de reseteo en un período corto / **Cuando** se solicita una adicional que supera el límite / **Entonces** el sistema rechaza la nueva solicitud sin enviar otro enlace

---

## Fuera de Alcance

* Comentarios.
* Likes.
* Moderación humana con equipo dedicado / paneles de moderación avanzados.
* Panel de administración avanzado (analíticas, gestión de usuarios/roles, auditoría). La moderación de contenido en sí (listados de RF-029/RF-030 y las acciones de RF-025/RF-027/RF-028) sí cuenta con una interfaz simple, restringida al rol Administrador — ver "Alcance del MVP".
* Perfiles sociales.
* Seguimiento de usuarios.
* Gamificación.
* Posibilidad de que el usuario cambie el idioma de la interfaz manualmente (se detecta automáticamente según el navegador, RF-035).
* Ingreso de ubicación manual mediante dirección/geocoding (solo se admiten coordenadas de latitud/longitud, RF-003).
* Edición de un mural ya creado.

## Riesgos y Dependencias

### Fotografías de baja calidad

**Riesgo:** imágenes borrosas, oscuras o poco representativas del mural, que reducen el valor de la plataforma.

**Mitigación:**
* Validación técnica básica al subir la imagen (resolución mínima, nitidez aproximada).
* Feedback inmediato al usuario si la imagen no cumple criterios mínimos, con opción de volver a tomarla o seleccionarla.
* Mostrar recomendaciones (buena luz, encuadre) antes de la captura.

### Ubicaciones incorrectas

**Riesgo:** murales geolocalizados de forma errónea, dificultando su descubrimiento o generando confusión.

**Mitigación:**
* Uso de GPS como opción por defecto (mayor precisión) según RNF-004, con ingreso manual de coordenadas como alternativa (RF-003 / RF-009).
* Confirmación visual en un mapa antes de guardar, para que el usuario valide el pin.
* Posibilidad de reportar ubicación incorrecta como parte del flujo de reportes (RF-014).

### Contenido inapropiado cargado por usuarios

**Riesgo:** imágenes NSFW, ofensivas o que no correspondan a murales/arte urbano.

**Mitigación:**
* Validación automática básica de contenido NSFW antes de publicar (RF-015).
* Estado "pendiente" para todo contenido nuevo hasta su validación (RF-013).
* Sistema de reportes de usuarios para contenido ya publicado (RF-014), con posibilidad de despublicación por parte de un administrador (RF-028).
* Rechazo manual por parte de un administrador si la validación automática no detecta contenido inapropiado o falla (RF-027), apoyado en los listados de murales pendientes/reportados (RF-029/RF-030).
* Definición de criterios claros de contenido aceptable, comunicados al usuario al momento de subir una foto.

### Falsos positivos en validación NSFW

**Riesgo:** murales legítimos rechazados automáticamente por error, generando frustración en el usuario colaborador.

**Mitigación:**
* El usuario puede ver que su mural fue rechazado a través de "mis murales" (RF-031), en vez de desaparecer silenciosamente.
* No existe en el MVP un mecanismo de apelación; el usuario deberá volver a subir el mural si considera que fue un error (limitación aceptada para el MVP).

### Dependencia de autenticación de Google

**Riesgo:** indisponibilidad o cambios en la API de Google OAuth afectan el login (RF-023) y la vinculación automática de cuentas (RF-034).

**Mitigación:**
* La autenticación con usuario/contraseña (RF-022) queda disponible como vía alternativa independiente de Google.

### Exposición por sesiones de larga duración

**Riesgo:** una sesión válida por 7 días (RNF-006) aumenta la ventana de exposición si un dispositivo es robado o compartido.

**Mitigación:**
* Cierre de sesión explícito disponible para el usuario (RF-033).
* Sesión servida exclusivamente sobre HTTPS y token no persistido en texto plano (RNF-006).

### Exposición de fotografías de murales por URL pública

**Riesgo:** si el contenedor de almacenamiento de imágenes queda con acceso público por defecto (comportamiento típico del SDK si no se configura explícitamente), la fotografía de un mural "pendiente" o "rechazado" queda descargable por cualquiera que tenga la URL, sin pasar por ningún control de acceso — incluso si el mural nunca aparece en búsquedas ni en el mapa (RF-013).

**Mitigación:**
* Contenedor de almacenamiento de imágenes configurado explícitamente como privado, nunca con acceso público anónimo (RNF-009).
* Fotografías servidas mediante URLs de acceso temporal firmadas (SAS o equivalente), de corta duración, generadas en el momento de la respuesta y respetando la misma visibilidad que ya aplica al mural (RF-013).

### Dependencia de servicio de envío de emails (SendGrid)

**Riesgo:** indisponibilidad o fallas en SendGrid impiden el envío del enlace de reseteo de contraseña (RF-036).

**Mitigación:**
* Reintento automático de envío ante fallos transitorios del servicio.
* Mensaje de error visible al usuario si el envío no puede completarse.
