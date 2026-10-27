# Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY .editorconfig global.json Directory.Build.props Directory.Packages.props Nirengi.slnx ./
COPY src/ src/
RUN dotnet publish src/Host/Host.csproj -c Release -o /app /p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD ["dotnet", "Nirengi.Host.dll", "--health-probe"]
ENTRYPOINT ["dotnet", "Nirengi.Host.dll"]
