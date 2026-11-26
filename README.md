# TutorFlow

Asistente académico para estudiantes y coordinadores. Responde con base en el reglamento vigente y en los datos académicos del usuario, y cita el documento y la sección de cada fuente. Backend .NET 10 (arquitectura hexagonal), frontend React + TypeScript y LLM Gemini.

- **Qué hace y qué no:** `SPEC.md`.
- **Casos de prueba:** `escenarios/`.
- **Cómo se construyó:** `FABRICA.md` y `PROGRESO.md`.
- **Resultados y costos:** `REPORTE.md`.

## Requisitos

- Docker con Docker Compose 2.24 o superior.
- Para desarrollar y ejecutar pruebas: .NET 10 SDK y Node.js 22 o superior.
- Una API key de Gemini. Para ejecutar los evals completos conviene una clave con alta disponibilidad (ver `REPORTE.md`, Limitaciones).

## Ejecutar el sistema

1. **Configurar la clave:**

   ```bash
   cp .env.example .env
   ```

   Edita `.env` y pon tu `LLM_API_KEY` real. El `.env` no se versiona. Las demás variables son opcionales y están documentadas en `.env.example`.

2. **Levantar:**

   ```bash
   docker compose up -d --build --wait
   ```

   Levanta la API simulada (`mock-api`, solo en la red interna) y el backend, que sirve también el frontend.

3. **Usar:**
   - Frontend: http://localhost:8080 (selector de usuario de prueba y chat).
   - Salud: `curl http://localhost:8080/api/salud` → `200 {"estado":"ok"}`.
   - API:

     ```bash
     curl -X POST http://localhost:8080/api/asistente/preguntar \
       -H "Content-Type: application/json" \
       -d '{"pregunta":"¿Cuál es la nota mínima para aprobar una asignatura?","usuario":{"rol":"estudiante","id":"EST-001"}}'
     ```

     Usuarios de prueba: `EST-001` a `EST-006` (estudiantes) y `COORD-01` y `COORD-02` (coordinadores).

4. **Auditoría:** una línea JSON por pregunta en `var/auditoria/auditoria.jsonl` y en la salida del contenedor (`docker compose logs backend`).

5. **Parar:**

   ```bash
   docker compose down
   ```

## Pruebas

| Qué | Comando |
| --- | --- |
| Compilar | `dotnet build src/backend/TutorFlow.sln` |
| Pruebas del backend, incluidos los 145 escenarios `api` (LLM falso, sin red) | `dotnet test src/backend/TutorFlow.sln` |
| Pruebas del frontend | `npm --prefix src/frontend ci` y luego `npm --prefix src/frontend test` |
| Construir el frontend | `npm --prefix src/frontend run build` |

La CI de GitHub Actions ejecuta todo esto en cada PR, junto con la validación de los commits y un smoke de `docker compose`.

## Evals con el LLM real

Ejecutan los escenarios `api` de `escenarios/` contra el sistema levantado y la clave real del `.env`:

```bash
docker compose up -d --build --wait
dotnet run --project evals/TutorFlow.Evals -c Release -- --file banco.md --delay-ms 15000
```

- Ejecuta un archivo de escenarios cada vez (`--file`) o escenarios sueltos (`--only ESC-BAN-01,ESC-BAN-02`).
- Con una clave de capa gratuita, mantén una pausa entre preguntas (`--delay-ms`) para no superar el límite por minuto.
- `--list` lista los escenarios sin usar red ni clave.
- Los informes quedan en `var/evals/`.
- Detalle en `evals/README.md`.

## Desarrollo local sin Docker

1. **API simulada:**

   ```bash
   node material/mock-api/server.js
   ```

   Queda en http://localhost:4000.

2. **Backend:** define en tu terminal `LLM_API_KEY` (y, si quieres, las variables opcionales de `.env.example`) y `MOCK_API_URL=http://localhost:4000`. No definas `KNOWLEDGE_CORPUS_PATH`, porque su valor de ejemplo es una ruta del contenedor; sin ella, el backend encuentra `material/reglamento` solo. Luego:

   ```bash
   dotnet run --project src/backend/TutorFlow.Api
   ```

   Queda en http://localhost:8080.

3. **Frontend en modo desarrollo** (opcional; reenvía `/api` al backend):

   ```bash
   npm --prefix src/frontend run dev
   ```

## Estructura

```text
src/backend/   .NET 10 · Domain, Application, Infrastructure, Api
src/frontend/  React + TypeScript (Vite)
tests/         Pruebas unitarias e integración (LLM falso)
evals/         Ejecutor de escenarios con el LLM real
escenarios/    Casos de prueba (fuente de verdad)
material/      Reglamento, API simulada y preguntas de ejemplo
```

## Contribuir

- Activa los git hooks una vez por clon:

  ```bash
  git config core.hooksPath .githooks
  ```

- Commits en formato Conventional Commits, con el trailer `Generado-por: Claude Code` (commits de la fábrica) o `Generado-por: Humano` (commits del responsable) como última línea.
- Reglas completas en `AGENTS.md`.
