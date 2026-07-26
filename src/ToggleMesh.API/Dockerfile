FROM --platform=$BUILDPLATFORM node:22-alpine AS build-ui
WORKDIR /ui
COPY src/ToggleMesh.AdminUI/package*.json ./
RUN npm install --legacy-peer-deps
COPY src/ToggleMesh.AdminUI/ .
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/ToggleMesh.API/ToggleMesh.API.csproj", "src/ToggleMesh.API/"]
COPY ["src/ToggleMesh.Common/ToggleMesh.Common.csproj", "src/ToggleMesh.Common/"]
RUN dotnet restore "src/ToggleMesh.API/ToggleMesh.API.csproj"

COPY . .
WORKDIR "/src/src/ToggleMesh.API"
RUN dotnet publish "./ToggleMesh.API.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /app/keys && chown -R 1000:1000 /app
USER 1000
EXPOSE 8080

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build-ui /ui/dist ./wwwroot
ENTRYPOINT ["dotnet", "ToggleMesh.API.dll"]
