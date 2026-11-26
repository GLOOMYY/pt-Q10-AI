# TutorFlow · image for the `backend` service (SPEC R-DEP-01, R-DEP-04, D-14).
#
# Three stages:
#   1. frontend · builds the React + TypeScript frontend. Until F4 `src/frontend` does
#                 not exist: the stage then produces an empty /out and does not fail.
#   2. build    · restores and publishes the .NET 10 backend (TutorFlow.Api only).
#   3. runtime  · minimal ASP.NET 10 image, non-root user, no secrets.
#
# No secrets (R-DEP-04): no ARG or ENV holds a key. LLM_API_KEY is injected at runtime
# from the container environment (docker-compose.yml, env_file: .env).

# ---------------------------------------------------------------------------
# 1. Frontend (completed in F4; until then this stage is tolerated as empty)
# ---------------------------------------------------------------------------
FROM node:22-alpine AS frontend
WORKDIR /src
# Copies src/ only (.dockerignore drops the rest). src/ exists even without src/frontend.
COPY src/ ./src/
# F4: once src/frontend/package.json exists, install dependencies and copy the build
# output (dist/) to /out. The runtime stage serves /out as wwwroot (D-14).
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
# Static frontend served from the same origin (D-14). Empty until F4.
COPY --from=frontend /out/ ./wwwroot/

# Audit directory (R-LOG-07). docker-compose.yml mounts ./var/auditoria over it.
# Relative to WORKDIR, i.e. var/auditoria inside /app.
RUN mkdir -p /app/var/auditoria && chown app:app /app/var/auditoria

# Internal port of the backend (R-DEP-01). Secrets are not set here: LLM_API_KEY is injected at runtime.
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Unprivileged user: `app` (UID 1654) ships in the aspnet image.
USER app

ENTRYPOINT ["dotnet", "TutorFlow.Api.dll"]
