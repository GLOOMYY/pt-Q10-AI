# TutorFlow · image for the `backend` service (SPEC R-DEP-01, R-DEP-04, D-14).
#
# Three stages:
#   1. frontend · builds the React + TypeScript frontend (src/frontend, Vite -> dist/, DUDA-07).
#                 While src/frontend/package.json does not exist the stage produces an
#                 empty /out and does not fail.
#   2. build    · restores and publishes the .NET 10 backend (TutorFlow.Api only).
#   3. runtime  · minimal ASP.NET 10 image, non-root user, no secrets.
#
# No secrets (R-DEP-04): no ARG or ENV holds a key. LLM_API_KEY is injected at runtime
# from the container environment (docker-compose.yml, env_file: .env).

# ---------------------------------------------------------------------------
# 1. Frontend (F4): tolerated as empty while src/frontend/package.json does not exist
# ---------------------------------------------------------------------------
FROM node:22-alpine AS frontend
WORKDIR /src
# Copies src/ only (.dockerignore drops the rest). src/ exists even without src/frontend.
COPY src/ ./src/
# Once src/frontend/package.json exists: npm ci (needs package-lock.json), npm run build
# (Vite -> dist/) and copy dist/ to /out. The runtime stage serves /out as wwwroot (D-14).
RUN if [ -f src/frontend/package.json ]; then \
      cd src/frontend && npm ci && npm run build && mkdir -p /out && cp -r dist/. /out/ ; \
    else \
      echo "src/frontend does not exist yet (F4): skipping the frontend build" && mkdir -p /out ; \
    fi

# ---------------------------------------------------------------------------
# 2. Backend: restore and publish TutorFlow.Api (Release)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/backend/ ./src/backend/
RUN dotnet restore src/backend/TutorFlow.Api/TutorFlow.Api.csproj
RUN dotnet publish src/backend/TutorFlow.Api/TutorFlow.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app

# ---------------------------------------------------------------------------
# 3. Runtime: ASP.NET runtime plus the published application only
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./
# Static frontend served from the same origin (D-14). Empty while src/frontend does not exist.
COPY --from=frontend /out/ ./wwwroot/

# Reglamento corpus read by the knowledge base (R-FUE-01). .dockerignore lets only the three
# indexed files through; the loader reads them from KNOWLEDGE_CORPUS_PATH.
COPY material/reglamento/ ./material/reglamento/
# Explicit corpus location (a path, not a secret). Overridable from .env, see .env.example.
ENV KNOWLEDGE_CORPUS_PATH=/app/material/reglamento

# Audit directory (R-LOG-07). docker-compose.yml mounts ./var/auditoria over it.
# Relative to WORKDIR, i.e. var/auditoria inside /app.
RUN mkdir -p /app/var/auditoria && chown app:app /app/var/auditoria

# Internal port of the backend (R-DEP-01). Secrets are not set here: LLM_API_KEY is injected at runtime.
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Unprivileged user: `app` (UID 1654) ships in the aspnet image.
USER app

ENTRYPOINT ["dotnet", "TutorFlow.Api.dll"]
