# FÁBRICA · TutorFlow

Cómo se organiza la fábrica de agentes que construye TutorFlow: roles, modelos, áreas de archivos, worktrees y ramas, orden de trabajo, PR e integración.

Se apoya en `AGENTS.md` (reglas de todo agente) y `CLAUDE.md` (lo propio de Claude Code, incluidos los procedimientos del orquestador). Lo que se construye está en `SPEC.md`; lo que debe pasar, en `escenarios/`.

## 1. Organización

- **Claude Code es el orquestador** (sesión principal). Es una fábrica ***lights out***: funciona sin intervención del responsable (CLAUDE §7).
- Hasta **10 subagentes en paralelo** (revisores incluidos), con **una sola instancia por rol** a la vez. Cada uno trabaja en **su propio worktree y su propia rama**, creados por el orquestador (CLAUDE §3).
- **Todo cambio entra por un PR en GitHub, siempre con CI** (única excepción: el primer PR de F0, §5). Ningún subagente mergea.
- Cada rol está definido en `.claude/agents/<rol>.md`, con su modelo fijado por ID completo: `claude-opus-5-5`, `claude-sonnet-5-5` y `claude-haiku-5-5`.
- Los permisos de la fábrica están en `.claude/settings.json` (CLAUDE §9).
- Los subagentes reportan sus dudas al orquestador, que es el único que escribe en `dudas.md` (AGENTS §3).

## 2. Roles

Cada requisito tiene **un solo rol dueño**.

| Rol | Modelo | Responsabilidad | Requisitos que le pertenecen | Escenarios que debe hacer pasar |
| --- | --- | --- | --- | --- |
| **orquestador** | `claude-opus-5-5` | Sesión principal. Crea worktrees y ramas, reparte tareas, lanza subagentes, registra dudas, revisa con el revisor y **mergea los PR**. No escribe código de producción. | — | — |
| **plataforma** | `claude-sonnet-5-5` | Esqueleto de la solución hexagonal: `.sln`, todos los csproj y sus referencias, `Directory.*.props`, `Program.cs` completo (composición, configuración, `/api/salud` mínimo en F0, servido del frontend en F4) y adaptador de reloj. | R-DEP-02 (configuración y error si falta `LLM_API_KEY`), D-14 (servido estático) | — |
| **dominio** | `claude-sonnet-5-5` | Reglas puras: definitiva, promedio, acumulada, inasistencia, habilitación, becas y simulación. | R-CAL, R-ASI, R-HAB, R-BEC, R-QA-02 | ESC-RAR, ESC-SIM (unitarios) |
| **nucleo** | `claude-opus-5-5` | La solución: puertos, caso de uso del asistente, contrato de §5, validación de entradas (400), identidad y matriz de permisos (403), herramientas del modelo incluida `rechazar`, citas de datos, formato de números y errores 503. | §4, §5, R-SEG-01..06, R-SEG-10..12, R-ARQ, R-RES-02, R-RES-04, R-RES-06..07, R-QA-05, API-06 | ESC-BAN, ESC-ENT, ESC-EST y ESC-COO (consultas permitidas), ESC-SIM (api) |
| **conocimiento** | `claude-sonnet-5-5` | Base de conocimiento del corpus, índice de artículos y secciones, validación de citas de documentos, puerto del LLM con su adaptador real y el LLM falso de pruebas, tokens y costo. | R-FUE-01, R-FUE-02, R-FUE-04, R-RES-01, R-RES-03, R-RES-05, R-QA-03, R-QA-04 (timeout del LLM) | ESC-CIT, ESC-ABU |
| **datos** | `claude-sonnet-5-5` | Adaptador de la API simulada y su doble en proceso para pruebas. | API-01..05, API-07..10, R-QA-04 (timeout de la API simulada) | — (lo usan los demás) |
| **seguridad** | `claude-sonnet-5-5` | Defensas adicionales: barrera determinista, inyección, todo o nada, agregados, extracción del sistema, y la auditoría completa (adaptador, middleware y log en memoria para pruebas). | R-FUE-03, R-SEG-07..09, R-SEG-13..14, R-LOG | ESC-INY, ESC-EST y ESC-COO (casos 403) |
| **integracion** | `claude-sonnet-5-5` | Pruebas de integración de punta a punta en `tests/` y ejecutor de `evals/`. Corre las pruebas finales. | R-QA-01, AC-01..04 | Todos (verificación final) |
| **devops** | `claude-haiku-5-5` | `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.gitignore`, `.gitattributes`, git hooks, CI y AGENTS §11. | R-DEP-01, R-DEP-03..04, AGENTS §9.4 | — |
| **revisor** | `claude-opus-5-5` | **Solo lectura.** Revisa cada PR contra el SPEC, los escenarios y `AGENTS.md`. **No hace commits:** solo comenta el PR y entrega su veredicto. | — | — |
| **frontend** | `claude-sonnet-5-5` | Selector de usuario de prueba y chat. **Se construye al final.** | R-FRO | — |

## 3. Áreas de archivos

Cada rol **solo modifica los archivos de su área** (AGENTS §9.1). Si necesita un cambio fuera de ella, lo reporta como duda al orquestador, que lo asigna al rol dueño.

| Rol | Puede modificar |
| --- | --- |
| plataforma | `src/backend/TutorFlow.sln`, `src/backend/Directory.Build.props`, `src/backend/Directory.Packages.props`, la **creación** de todos los `*.csproj` (de `src/` y `tests/`) y sus `ProjectReference`, `src/backend/TutorFlow.Api/Program.cs`, `src/backend/TutorFlow.Infrastructure/DependencyInjection.cs`, `src/backend/TutorFlow.Infrastructure/Clock/`, `src/backend/TutorFlow.Api/Endpoints/HealthEndpoint.cs` (solo en F0) |
| dominio | `src/backend/TutorFlow.Domain/`, `tests/TutorFlow.Domain.Tests/` |
| nucleo | `src/backend/TutorFlow.Application/` (excepto `Security/Defenses/` y `Audit/`), `src/backend/TutorFlow.Api/` (excepto `Program.cs` y `Middleware/`), `tests/TutorFlow.Application.Tests/` (excepto `Security/`), `tests/TutorFlow.Api.Tests/` |
| conocimiento | `src/backend/TutorFlow.Infrastructure/Knowledge/`, `src/backend/TutorFlow.Infrastructure/Llm/`, `tests/TutorFlow.Testing/Llm/`, `tests/TutorFlow.Infrastructure.Tests/Knowledge/`, `tests/TutorFlow.Infrastructure.Tests/Llm/` |
| datos | `src/backend/TutorFlow.Infrastructure/AcademicApi/`, `tests/TutorFlow.Testing/AcademicApi/`, `tests/TutorFlow.Infrastructure.Tests/AcademicApi/` |
| seguridad | `src/backend/TutorFlow.Application/Security/Defenses/`, `src/backend/TutorFlow.Application/Audit/`, `src/backend/TutorFlow.Infrastructure/Audit/`, `src/backend/TutorFlow.Api/Middleware/`, `tests/TutorFlow.Testing/Audit/`, `tests/TutorFlow.Application.Tests/Security/` |
| integracion | `tests/TutorFlow.Integration.Tests/`, `evals/` |
| devops | `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.gitignore`, `.gitattributes`, `.githooks/`, `.github/workflows/`, `AGENTS.md` §11 (solo esa sección) |
| revisor | Ninguno (solo comenta PR). |
| frontend | `src/frontend/` |

- **csproj:** plataforma los crea con sus referencias en F0. Después, cada rol puede agregar `PackageReference` en los csproj de su área; las versiones van en `Directory.Packages.props` (las pide a plataforma por duda si falta una).
- **Registro de dependencias:** cada área expone su `AddXxx()` en su propia carpeta (ej. `AddAcademicApi()` en `AcademicApi/`). `DependencyInjection.cs` (plataforma) y `Program.cs` (plataforma) solo los invocan.
- **Opciones de configuración:** cada clase Options vive en el área del adaptador que la usa.
- **Puertos** (`TutorFlow.Application/Ports/`): los define **nucleo**. Si otro rol necesita uno nuevo o un cambio, lo reporta al orquestador.
- **`tests/TutorFlow.Testing/`:** proyecto compartido de dobles de prueba (API simulada, LLM falso y log en memoria); cada carpeta tiene su dueño según la tabla.
- Nadie toca los archivos protegidos (AGENTS §10), salvo las excepciones de devops que esa sección indica.

## 4. Worktrees y ramas

- **Rama:** `<rol>/<id-tarea>`, ej. `dominio/r-cal-01`, `seguridad/r-seg-14`.
- **Worktree:** `../tutorflow-wt/<rol>-<id-tarea>`, creado por el orquestador desde `origin/main` (CLAUDE §3).
- Una rama, una tarea. Al mergear el PR, el orquestador elimina el worktree y la rama (CLAUDE §4).

## 5. Orden de trabajo

| Fase | Roles | Entrega | Depende de |
| --- | --- | --- | --- |
| **F0a · Esqueleto** | plataforma | Solución hexagonal que compila, proyectos de `tests/` vacíos, `Program.cs` con configuración por entorno y un `GET /api/salud` mínimo. **Único PR sin CI** (todavía no existe). | — |
| **F0b · CI y contenedores** | devops | `docker compose up` con la API simulada y el backend, `.env.example`, `.gitignore` (`.env`, `var/`), `.gitattributes`, git hooks, CI en verde y AGENTS §11. El Dockerfile tolera que todavía no exista `src/frontend`. | F0a |
| **F1a · Contratos** | nucleo | Puertos de `Application/Ports/` y contrato de §5 (DTO y validación de entrada). Se mergea antes de seguir. | F0b |
| **F1b · Núcleo** (en paralelo) | dominio · datos · conocimiento | Reglas de cálculo; adaptador y doble de la API simulada; base de conocimiento, índice de citas y LLM falso. | F1a |
| **F2 · Asistente y seguridad** (en paralelo) | nucleo · seguridad · conocimiento | Caso de uso completo, autorización y `rechazar`; barrera, defensas y auditoría; adaptador del LLM real. | F1b |
| **F3 · Integración** | integracion | Integración de punta a punta en `tests/` y ejecutor de `evals/`. | F2 |
| **F4 · Frontend** (en paralelo) | frontend · plataforma · devops | Selector y chat; servido estático en `Program.cs`; etapa de frontend en el Dockerfile. | F3 |
| **F5 · Pruebas finales** | integracion | `evals/` con el LLM real sobre todos los escenarios de nivel `api`. Los fallos se reportan al orquestador, que asigna la corrección al rol dueño del requisito; luego se repite F5. | F4 |

El **revisor** actúa en todas las fases, sobre cada PR.

## 6. Ciclo de una tarea

1. El **orquestador** elige una tarea (un requisito o un escenario), crea el worktree y la rama, y lanza el rol con: ruta del worktree, rama, IDs del SPEC y de los escenarios, y respuestas a dudas que le afecten (CLAUDE §3).
2. El **subagente** lee lo indicado en AGENTS §2.
3. Implementa con commits pequeños, uno por requisito o escenario, con `git commit -m` y el trailer `Generado-por: Claude Code` (AGENTS §9.2, CLAUDE §6).
4. Verifica los puntos 1–3 y 6 de la definición de terminado (AGENTS §12).
5. Integra `origin/main` en su rama (`git fetch origin` + `git merge origin/main`, nunca rebase), sube la rama y **abre un PR en GitHub hacia `main`**.
6. Espera la **CI** del PR; si falla, la corrige en la misma rama.
7. Entrega al orquestador: qué hizo, qué IDs cubre, el número del PR y las dudas o atascos (AGENTS §3).
8. El **orquestador** aplica el procedimiento de revisión e integración (CLAUDE §4): CI en verde, revisor sin bloqueantes (con nueva revisión después de cada corrección), rama al día con `main` y **merge commit** (nunca squash ni rebase).
