# 1. Build Frontend
FROM node:20-alpine AS build-frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# 2. Build Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-backend
WORKDIR /src
COPY Archlab.Backend/Archlab.Backend.csproj Archlab.Backend/
RUN dotnet restore Archlab.Backend/Archlab.Backend.csproj
COPY Archlab.Backend/ Archlab.Backend/
WORKDIR /src/Archlab.Backend
RUN dotnet publish -c Release -o /app/publish

# 3. Final Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build-backend /app/publish .
COPY --from=build-frontend /src/frontend/dist ./wwwroot

# Configurações de ambiente
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SERVE_FRONTEND=true
ENV DatabaseProvider=PostgreSQL
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "Archlab.Backend.dll"]
