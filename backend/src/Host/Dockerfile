# Build context: repo root (the Curriculum module compiles in data/seed).
# The admin panel is built here and served by the API at /admin (same origin, ADR-0019).
FROM node:22-alpine AS admin
WORKDIR /repo/admin
COPY admin/package.json admin/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY admin/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo/backend
COPY backend/.editorconfig backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Nirengi.slnx ./
COPY backend/src/ src/
COPY data/seed/ /repo/data/seed/
RUN dotnet publish src/Host/Host.csproj -c Release -o /app /p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --from=admin /repo/admin/dist ./wwwroot/admin
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD ["dotnet", "Nirengi.Host.dll", "--health-probe"]
ENTRYPOINT ["dotnet", "Nirengi.Host.dll"]
