# PRD FEAT-012: Comando único para levantar frontend+backend visibles en la LAN

| Field | Value |
|-------|-------|
| Ticket | FEAT-012 |
| Tracker | none |
| Date | 2026-08-30 |
| PRD loops | 0 |

## Context and Problem

Hoy, para probar la app desde el celular (u otro dispositivo) dentro de la misma red local, hay que
levantar frontend y backend en dos terminales separadas, editar configuración a mano cada vez
(host de Kestrel, `--host` de `ng serve`, orígenes de CORS, base URL del cliente NSwag) y el
certificado HTTPS de desarrollo no lo confía el celular. No existe un comando único y repetible
para esto — solo instrucciones manuales que hay que rehacer cada sesión.

## Goals

Un único comando que levante frontend y backend escuchando en la LAN (no solo `localhost`), sin
exponer nada a internet, sin requerir reconfiguración manual cuando cambia la IP de LAN de la
máquina (asignación DHCP), y sin alterar el flujo de desarrollo local existente (`dotnet run`/
`ng serve` sin flags siguen funcionando exactamente igual que hoy).

## Functional Requirements

- FR-01: El sistema debe ofrecer un comando único que levante backend y frontend juntos, ambos
  escuchando en todas las interfaces de red de la máquina (no solo loopback).
- FR-02: El comando `dotnet run` del backend sin flags adicionales debe seguir comportándose
  exactamente igual que hoy (HTTPS en `localhost:7126`, sin escuchar en la LAN).
- FR-03: El comando `ng serve` del frontend sin flags adicionales debe seguir comportándose
  exactamente igual que hoy (solo `localhost:4200`).
- FR-04: En modo LAN, el backend debe escuchar por HTTP (no HTTPS) — el certificado de desarrollo
  no es confiado por otros dispositivos de la red.
- FR-05: El frontend debe resolver la base URL del backend dinámicamente a partir del host desde el
  que fue servido: si es `localhost`, usar el flujo HTTPS actual (`https://localhost:7126`); si es
  cualquier otro host (una IP de LAN), usar HTTP contra el puerto del backend en esa misma IP.
- FR-06: El backend debe aceptar requests CORS desde el origen del frontend en la LAN, sin que el
  desarrollador tenga que editar configuración a mano cuando cambia la IP de LAN de la máquina.
- FR-07: La Content-Security-Policy de desarrollo del frontend debe permitir conexiones hacia el
  backend en su origen de LAN.
- FR-08: El comando único debe detener ambos procesos (backend y frontend) de forma limpia ante una
  interrupción (Ctrl+C).

## Non-Functional Requirements

- NFR-01: El modo LAN no debe ser alcanzable fuera de la red local — sin port forwarding, sin
  exposición pública; el comando solo debe habilitar interfaces ya presentes en la LAN privada de
  la máquina.
- NFR-02: El comando no debe requerir que el desarrollador conozca ni hardcodee la IP de LAN actual
  de su máquina en ningún archivo versionado.

## Acceptance Criteria

- AC-01: WHEN el desarrollador corre el comando de modo LAN, THE sistema SHALL levantar backend y
  frontend escuchando en todas las interfaces de red de la máquina. (FR-01, FR-04)
- AC-02: WHEN un dispositivo de la misma LAN abre `http://<ip-lan>:4200`, THE sistema SHALL cargar
  el frontend y comunicarse exitosamente con el backend, sin errores de CORS ni de CSP en la
  consola del navegador. (FR-05, FR-06, FR-07)
- AC-03: WHEN el desarrollador corre `dotnet run` sin el comando de modo LAN, THE sistema SHALL
  comportarse exactamente igual que antes de esta feature (HTTPS en `localhost:7126`, sin escuchar
  en la LAN). (FR-02)
- AC-04: WHEN el desarrollador corre `ng serve` sin el comando de modo LAN, THE sistema SHALL
  comportarse exactamente igual que antes de esta feature (solo `localhost:4200`). (FR-03)
- AC-05: IF la IP de LAN de la máquina cambia entre una corrida y otra (DHCP), THEN THE sistema
  SHALL seguir funcionando correctamente en la corrida siguiente del comando de modo LAN, sin
  edición manual de configuración. (FR-06, NFR-02)
- AC-06: WHEN el desarrollador presiona Ctrl+C sobre el comando de modo LAN, THE sistema SHALL
  detener tanto el backend como el frontend. (FR-01, FR-08)
- AC-07: IF un dispositivo fuera de la LAN intenta alcanzar los puertos expuestos por el comando de
  modo LAN, THEN THE sistema SHALL ser inalcanzable (sin port forwarding ni exposición pública
  introducidos por esta feature). (NFR-01)
- AC-08: IF la máquina no tiene ninguna interfaz de red de LAN activa (solo loopback) al correr el
  comando de modo LAN, THEN THE sistema SHALL reportar un error claro en vez de fallar en silencio
  o quedar colgado. (FR-01)
- AC-09: WHEN el desarrollador abre `http://localhost:4200` mientras el comando de modo LAN está
  corriendo, THE sistema SHALL seguir funcionando contra el backend por HTTPS `localhost:7126`
  (mismo comportamiento que el flujo normal). (FR-05)

## Out of Scope

- HTTPS en modo LAN — se decidió HTTP plano dentro de la LAN (fuera de alcance instalar/confiar el
  certificado de desarrollo en otros dispositivos).
- Acceso desde fuera de la LAN (VPN, port forwarding, túneles tipo ngrok) — explícitamente fuera de
  alcance, ver NFR-01.
- Deploy a producción o a la nube — esta feature es exclusivamente para pruebas manuales en
  desarrollo local.
- Cambiar el comportamiento por defecto de `dotnet run`/`ng serve` — quedan intactos (FR-02/FR-03).
- Cualquier mecanismo de descubrimiento automático del otro dispositivo (QR code, mDNS, etc.) — el
  desarrollador sigue necesitando conocer/copiar la IP de LAN para abrirla en el otro dispositivo.

## Risks and Mitigations

- **Backend expuesto en la LAN sin autenticación adicional**: mismo `[Authorize]`/rate limiting ya
  vigentes en toda la API — el modo LAN no relaja autorización, solo la alcanzabilidad de red.
  Mitigado además por ser un comando explícito, nunca el comportamiento por defecto (AC-03).
- **CORS con origen de LAN dinámico amplía el conjunto de orígenes confiables** respecto al
  whitelist fijo actual (`Cors:AllowedOrigins`) — el mecanismo exacto (rango de IPs privadas,
  detección de IP en el propio script, etc.) se decide en PLAN; debe evaluarse en threat modeling
  antes de implementar.
- **Relajar la CSP de desarrollo para permitir el origen de LAN** podría enmascarar un problema de
  CSP real que solo aparecería en producción — mitigado porque el cambio queda acotado a
  `index.development.html` (nunca al `index.html` de producción, mismo patrón ya establecido en
  FIX-002).

## Dependencies

- Cliente NSwag generado (`api-client.generated.ts`, `API_BASE_URL` como `InjectionToken` ya
  existente en `app.config.ts`) — FR-05 lo reconfigura, no lo reemplaza.
- Configuración de CORS existente (`Cors:AllowedOrigins`, FIX-001) — FR-06 la extiende para el caso
  de LAN.
- CSP de desarrollo existente (`index.development.html`, FIX-002) — FR-07 la extiende.
- `launchSettings.json` del backend (perfiles `http`/`https` ya existentes) — FR-01/FR-04 pueden
  requerir un perfil nuevo o una variable de entorno adicional, a definir en PLAN.
