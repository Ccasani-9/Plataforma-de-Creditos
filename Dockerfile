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

EXPOSE 10000

# ${PORT} NO se expande dentro de una variable de entorno: se expande aquí, en el comando de inicio.
CMD ["sh", "-c", "export ASPNETCORE_URLS=\"http://0.0.0.0:${PORT:-10000}\" && exec dotnet PlataformaCreditos.dll"]
