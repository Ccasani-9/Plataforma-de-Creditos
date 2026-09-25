# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PlataformaCreditos/PlataformaCreditos.csproj PlataformaCreditos/
RUN dotnet restore PlataformaCreditos/PlataformaCreditos.csproj

COPY . .
RUN dotnet publish PlataformaCreditos/PlataformaCreditos.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Render termina TLS en su proxy: se confía en X-Forwarded-Proto/For para que la app sepa que es HTTPS (wss).
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    ConnectionStrings__DefaultConnection="Data Source=/var/data/plataforma-creditos.db"

# /var/data es el punto de montaje del disco persistente de Render (SQLite).
# Se ejecuta como root porque el disco montado por Render pertenece a root.
USER root
RUN mkdir -p /var/data

# Script de inicio: expande PORT y arranca la app (ver start.sh).
COPY start.sh /app/start.sh
RUN sed -i 's/\r$//' /app/start.sh && chmod +x /app/start.sh

EXPOSE 10000

# ${PORT} NO se expande dentro de una variable de entorno: se expande en start.sh, el comando de inicio.
CMD ["sh", "/app/start.sh"]
