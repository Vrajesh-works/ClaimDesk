# syntax=docker/dockerfile:1
# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ClaimDesk.Domain/ClaimDesk.Domain.csproj src/ClaimDesk.Domain/
COPY src/ClaimDesk.Application/ClaimDesk.Application.csproj src/ClaimDesk.Application/
COPY src/ClaimDesk.Infrastructure/ClaimDesk.Infrastructure.csproj src/ClaimDesk.Infrastructure/
COPY src/ClaimDesk.Api/ClaimDesk.Api.csproj src/ClaimDesk.Api/
RUN dotnet restore src/ClaimDesk.Api/ClaimDesk.Api.csproj
COPY src/ src/
RUN dotnet publish src/ClaimDesk.Api/ClaimDesk.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    EvidenceStorage__RootPath=/app/data/evidence
COPY --from=build /out .
RUN mkdir -p /app/data/evidence && chown -R $APP_UID /app/data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClaimDesk.Api.dll"]
