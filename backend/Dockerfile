# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless project files change)
COPY BeautyAgenda.slnx Directory.Build.props ./
COPY src/BeautyAgenda.Domain/BeautyAgenda.Domain.csproj src/BeautyAgenda.Domain/
COPY src/BeautyAgenda.Application/BeautyAgenda.Application.csproj src/BeautyAgenda.Application/
COPY src/BeautyAgenda.Infrastructure/BeautyAgenda.Infrastructure.csproj src/BeautyAgenda.Infrastructure/
COPY src/BeautyAgenda.Api/BeautyAgenda.Api.csproj src/BeautyAgenda.Api/
RUN dotnet restore src/BeautyAgenda.Api/BeautyAgenda.Api.csproj

COPY src/ src/
RUN dotnet publish src/BeautyAgenda.Api/BeautyAgenda.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app .
# Non-root user provided by the official image
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BeautyAgenda.Api.dll"]
