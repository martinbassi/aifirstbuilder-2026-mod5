#!/usr/bin/env bash
# scripts/dev-lan.sh — FEAT-012: levanta backend + frontend visibles en la LAN.
#
# Arranca `dotnet run` (backend) y `ng serve --host 0.0.0.0` (frontend) en background, exportando
# las variables de entorno que Block 1 (Program.cs) y Block 2 (app.config.ts) esperan para exponer
# el backend en HTTP en todas las interfaces (preservando el HTTPS de siempre en localhost) y
# aceptar CORS desde la IP de LAN. No toca ningún archivo versionado — la IP se detecta en tiempo
# de ejecución (NFR-02) y ningún flujo sin este script cambia (FR-02/FR-03/AC-03/AC-04).
set -euo pipefail
# Job control (monitor mode): sin esto, los procesos lanzados en background comparten el grupo de
# procesos del script y `kill -PGID` no aísla uno del otro. Con `set -m`, cada `&` arranca su
# propio grupo — necesario para el cleanup de abajo, que mata por grupo (ver comentario en
# cleanup()).
set -m

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend/src/Paretto.Api"
FRONTEND_DIR="$REPO_ROOT/frontend"

LAN_IP="$(hostname -I | awk '{print $1}')"
if [ -z "$LAN_IP" ]; then
  echo "Error: no se detectó ninguna interfaz de LAN activa (hostname -I no devolvió ninguna IP)." >&2
  echo "Conectate a una red (WiFi/Ethernet) y volvé a intentar." >&2
  exit 1
fi

# LanMode=true es el punto donde se activa R1 (docs/daw/security/threat-FEAT-012.md): el
# backend queda alcanzable por HTTP plano desde cualquier dispositivo de la LAN — riesgo aceptado
# explícitamente por el usuario en PLAN, acotado a este script opt-in.
export ASPNETCORE_URLS="https://localhost:7126;http://0.0.0.0:5267"
export ASPNETCORE_ENVIRONMENT=Development
export LanMode=true
export Cors__AllowedOrigins__1="http://${LAN_IP}:4200"

BACKEND_PID=""
FRONTEND_PID=""
CLEANED_UP=false

cleanup() {
  # trap sobre INT/TERM/EXIT: un Ctrl+C real dispara INT y después EXIT sobre el mismo proceso —
  # esta guarda evita correr la limpieza (y el mensaje) dos veces para el mismo evento.
  if [ "$CLEANED_UP" = true ]; then
    return
  fi
  CLEANED_UP=true

  echo ""
  echo "Deteniendo procesos..."
  # Se mata por grupo de procesos (PID negativo), no solo el PID guardado: `npx` no reenvía
  # TERM/INT a su hijo real (el binario `ng`), y matar solo el PID de npx deja ese hijo huérfano
  # (verificado manualmente — ver reporte del bloque). El fallback a `kill "$PID"` cubre el caso en
  # que el proceso ya haya salido solo.
  if [ -n "$BACKEND_PID" ] && kill -0 "$BACKEND_PID" 2>/dev/null; then
    kill -TERM -- "-$BACKEND_PID" 2>/dev/null || kill "$BACKEND_PID" 2>/dev/null || true
  fi
  if [ -n "$FRONTEND_PID" ] && kill -0 "$FRONTEND_PID" 2>/dev/null; then
    kill -TERM -- "-$FRONTEND_PID" 2>/dev/null || kill "$FRONTEND_PID" 2>/dev/null || true
  fi
  if [ -n "$BACKEND_PID" ]; then
    wait "$BACKEND_PID" 2>/dev/null || true
  fi
  if [ -n "$FRONTEND_PID" ]; then
    wait "$FRONTEND_PID" 2>/dev/null || true
  fi
}
trap cleanup INT TERM EXIT

echo "Iniciando backend (dotnet run)..."
# `exec` reemplaza el proceso del subshell por el de dotnet: el PID capturado en $! es el del
# proceso real, no el de un subshell intermedio — sin esto, matar $! no mataría a dotnet y lo
# dejaría huérfano al cortar con Ctrl+C (AC-05/AC-06).
# `--no-launch-profile`: sin este flag, `dotnet run` lee backend/src/Paretto.Api/Properties/
# launchSettings.json y pisa el ASPNETCORE_URLS/ASPNETCORE_ENVIRONMENT que exportamos arriba con el
# del primer perfil ahí (`http://localhost:5267` solamente) — verificado manualmente: sin el flag,
# Kestrel terminaba escuchando solo en localhost:5267, sin el binding LAN ni el HTTPS de siempre.
(cd "$BACKEND_DIR" && exec dotnet run --no-launch-profile) &
BACKEND_PID=$!

echo "Iniciando frontend (ng serve --host 0.0.0.0)..."
(cd "$FRONTEND_DIR" && exec npx ng serve --host 0.0.0.0) &
FRONTEND_PID=$!

echo ""
echo "Abrí esta URL desde otro dispositivo de la misma red local:"
echo "  http://${LAN_IP}:4200"
echo ""

wait "$BACKEND_PID" "$FRONTEND_PID"
