# Multi-stage: Vite frontend → .NET API serving API + wwwroot SPA.
# Build context = repo root. Railway: attach Postgres, set DATABASE_URL + AI keys.

# ---- Frontend ----
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
ENV VITE_API_BASE_URL=
RUN npm run build

# ---- Backend ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/backend.csproj backend/
RUN dotnet restore backend/backend.csproj
COPY backend/ ./backend/
RUN dotnet publish backend/backend.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /frontend/dist ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
EXPOSE 8080

CMD ["dotnet", "backend.dll"]
