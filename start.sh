#!/bin/sh
# Comando de inicio del contenedor (Render y local).
# ${PORT} lo inyecta Render en tiempo de ejecución y NO se expande dentro del valor de otra
# variable de entorno: por eso se expande aquí y se asigna a ASPNETCORE_URLS antes de arrancar.
set -e
export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-10000}"
echo "Iniciando PlataformaCreditos en ${ASPNETCORE_URLS}"
exec dotnet /app/PlataformaCreditos.dll
