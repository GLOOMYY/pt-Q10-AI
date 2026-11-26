# AGENTS · TutorFlow

Reglas para todo agente que trabaje en este repositorio. En este proyecto el único agente es **Claude Code** (y sus subagentes). Los roles, las áreas y el flujo de trabajo de la fábrica están en `FABRICA.md`; lo específico de Claude Code, en `CLAUDE.md`.

**Precedencia** si dos documentos se contradicen: `SPEC.md` > `AGENTS.md` > `FABRICA.md` > `CLAUDE.md` > `.claude/agents/`.

## 1. Reglas de oro

1. Antes de cualquier tarea se leen `SPEC.md` y los escenarios que le corresponden (§2).
2. `material/` es la fuente de verdad y es de solo lectura.
3. No se inventa nada: lo ambiguo se reporta como duda y se sigue con lo demás (§3).
4. Arquitectura hexagonal siempre (§5).
5. Configuración y secretos solo por variables de entorno; nada en el código ni en git (§7).
6. Prohibido saltar, borrar o modificar un test para que pase: se arregla el error, no el test (§8).
7. Commits pequeños, convencionales y con el trailer `Generado-por: Claude Code` (§9).
8. Una tarea solo está terminada cuando cumple la definición de terminado (§12).

## 2. Antes de cualquier tarea

En este orden:

1. **`SPEC.md`** completo: requisitos (`R-…`), casos límite (`CL-…`), criterios de aceptación (`AC-…`) y decisiones (`D-…`).
2. **`escenarios/README.md`** y los escenarios que verifican los requisitos de la tarea (buscar el ID del requisito en el campo **Verifica**).
3. **`FABRICA.md`** y **`CLAUDE.md`**: qué rol cumple el agente, cuál es su área, cómo trabaja y a quién entrega.
4. Si es un subagente: **su definición en `.claude/agents/<rol>.md`**.
5. **`material/`** cuando la tarea toque reglas o datos: `material/reglamento/`, `material/mock-api/` y `material/README.md`. Es la fuente de verdad y **nunca** se modifica.
6. **`dudas.md`**: para no repetir una duda ya anotada.

## 3. Ambigüedades, dudas y atascos

**Nunca se inventa una regla.** Si algo es ambiguo o no está definido en `SPEC.md`, `escenarios/` o `material/`, la parte ambigua queda sin implementar y se continúa con todo lo que no dependa de ella.

**Atascos:** un **intento** es un ciclo completo de cambio + compilación + pruebas. Si una misma tarea falla **5 intentos**, o un permiso bloquea al agente, se documenta lo intentado, el error exacto y la propuesta, y se continúa con otra tarea.

**Quién escribe `dudas.md`:**

- Los **subagentes no editan `dudas.md`**. Reportan sus dudas y atascos en su entrega al orquestador, con el formato de abajo (sin número).
- El **orquestador** es el único que escribe en `dudas.md`: asigna el número, las registra y, cuando el responsable las responde, reparte las respuestas a los roles afectados.

**Cuándo detenerse:**

- Un **subagente** nunca se queda esperando: termina su ejecución entregando su informe al orquestador, con lo hecho y lo pendiente.
- El **orquestador** solo se detiene y notifica al responsable cuando ya no queda **ninguna** tarea que no dependa de una duda o un atasco registrado.

**Formato de cada entrada en `dudas.md`** (se agregan al final, nunca se borran las anteriores):

```markdown
## DUDA-<NN> · <título corto>

- **Fecha:** AAAA-MM-DD
- **Rol / rama:** <rol de FABRICA.md> / <rama>
- **Referencia:** <IDs de SPEC o escenarios relacionados>
- **Tipo:** ambigüedad | atasco (n.º de intentos) | permiso
- **Bloqueante:** sí | no
- **Descripción:** qué no está claro o qué falla, con el error exacto si lo hay.
- **Qué quedó pendiente:** qué parte no se implementó por esta duda.
- **Propuesta:** opción recomendada, sin aplicarla.
- **Respuesta:** _(la completa el orquestador cuando el responsable responde)_
```

## 4. Estructura del repositorio

```text
/
├── src/
│   ├── backend/              # .NET 10 · hexagonal (§5)
│   │   ├── TutorFlow.sln
│   │   ├── Directory.Build.props
│   │   └── Directory.Packages.props
│   └── frontend/             # React + TypeScript · se construye al final (FABRICA.md)
├── tests/                    # Pruebas unitarias y de integración (LLM falso)
├── evals/                    # Preguntas al LLM real (escenarios de nivel api)
├── escenarios/               # Definición de los escenarios (solo lectura)
├── material/                 # Material entregado (solo lectura)
├── var/                      # Datos de ejecución (auditoría). En .gitignore
├── .claude/                  # settings.json y roles de subagente (agents/)
├── .githooks/                # Git hooks versionados: pre-commit y commit-msg (§9.4)
├── .github/workflows/        # CI con GitHub Actions (§9.4)
├── SPEC.md · AGENTS.md · CLAUDE.md · FABRICA.md · INTERVENCIONES.md · REPORTE.md
├── dudas.md
├── Dockerfile · .dockerignore · docker-compose.yml
├── .env.example · .gitignore · .gitattributes
```

- **`src/`** solo tiene código de la aplicación: el backend y el frontend. El **frontend es lo último** que se construye, cuando todo lo demás está terminado.
- **`tests/`** tiene las pruebas unitarias y de integración. **Solo las necesarias**: cada prueba verifica un requisito, un escenario o un caso límite concreto. No se escriben pruebas que no aporten (ej. probar getters o el framework).
- **`evals/`** tiene las preguntas que se le hacen al **LLM real**: ejecuta los escenarios de nivel `api` leyéndolos desde `escenarios/`, sin copiarlos ni redefinirlos.
- Cada archivo de la raíz tiene un rol dueño (FABRICA §3). No se crean carpetas ni archivos nuevos en la raíz fuera de los listados aquí sin acordarlo con el responsable.

## 5. Arquitectura hexagonal

El backend sigue **siempre** arquitectura hexagonal (puertos y adaptadores):

```text
src/backend/
├── TutorFlow.Domain/          # Entidades y reglas puras: definitiva, promedio, inasistencia, habilitación, becas y simulación (R-CAL, R-ASI, R-HAB, R-BEC). Sin dependencias externas.
├── TutorFlow.Application/     # Casos de uso, autorización (R-SEG), orquestación del asistente (R-ARQ) y PUERTOS (interfaces): API académica, LLM, base de conocimiento, log, reloj.
├── TutorFlow.Infrastructure/  # ADAPTADORES de salida: cliente de la API simulada, cliente del LLM, base de conocimiento, auditoría, reloj.
└── TutorFlow.Api/             # ADAPTADOR de entrada: ASP.NET Core, contrato de SPEC §5, composición de dependencias, frontend estático (D-14).
```

- Las dependencias apuntan **hacia adentro**: `Api` → `Application` → `Domain`, e `Infrastructure` → `Application`. `Domain` no depende de nada.
- **Única excepción:** `Api` referencia `Infrastructure` solo para la **composición de dependencias** (registro de adaptadores). Ningún otro código de `Api` usa tipos de `Infrastructure`.
- Cada capa expone su registro de dependencias en un método `AddXxx()` propio (ej. `AddInfrastructure()`), que `Program.cs` invoca.
- Todo lo externo (API simulada, LLM, archivos, reloj) se usa a través de un **puerto**. Así el LLM falso de CI y el real de `evals/` son dos adaptadores del mismo puerto.
- Los cálculos de R-CAL viven en `Domain` y se prueban sin red ni LLM.

## 6. Stack y código

- **.NET 10** para el backend; React + TypeScript para el frontend (SPEC §7).
- **Código en inglés** (nombres, comentarios, mensajes de log). Los textos que ve el usuario están en español (R-RES-06).
- **Completamente documentado:** comentarios XML (`///`) en todo tipo y miembro público del backend, y TSDoc en el frontend. Se documenta el *porqué* y la referencia al SPEC (ej. `// R-CAL-03: banker's rounding is the .NET default`).
- **Buenas prácticas del framework:** `<Nullable>enable</Nullable>`, inyección de dependencias, patrón Options para la configuración, `async`/`await` de punta a punta con `CancellationToken`, `HttpClientFactory`, `ILogger` con logs estructurados, y `decimal` con `MidpointRounding.AwayFromZero` para notas (D-06).
- **Paquetes NuGet:** las versiones se centralizan en `src/backend/Directory.Packages.props`. Cada rol agrega las `PackageReference` en los csproj de su área.

## 7. Configuración y secretos

- **Toda la configuración va por variables de entorno** (SPEC R-DEP-02). Nada de valores escritos en el código: URLs, puertos, modelos, precios, timeouts y límites se leen de la configuración, con los valores por defecto definidos en el SPEC.
- **Todos los secretos van en variables de entorno.** NUNCA se sube un secreto a git: `.env` está en `.gitignore`, y solo se versiona `.env.example` con valores de ejemplo.
- Los secretos nunca aparecen en logs, en respuestas, en imágenes Docker ni en mensajes de commit (R-DEP-04, R-LOG-05).
- En CI se usa una `LLM_API_KEY` **ficticia** (el LLM es falso). La clave real solo existe en el `.env` local del responsable, que los agentes nunca leen; `evals/` la recibe por el entorno del proceso.

## 8. Pruebas

| Dónde | Qué | LLM | API simulada | Corre en |
| --- | --- | --- | --- | --- |
| `tests/` | Pruebas unitarias (incluye los escenarios de nivel `unitario`) e integración del backend, incluidos los escenarios `api` en lo que no depende del texto del LLM (`escenarios/README.md`). | **Falso** (doble determinista) | **Doble en proceso** construido con `material/mock-api/datos.json` | Local y **CI** |
| `evals/` | Escenarios de nivel `api` contra el sistema levantado con `docker compose`. | **Real** | Contenedor real | **Solo en F5 · Pruebas finales** |

- **Prohibido saltar, borrar o modificar un test para que pase.** Si un test falla, se arregla el código. Si el test parece incorrecto, se reporta como duda (§3); no se toca.
- Lo mismo aplica a los escenarios: un escenario no se ajusta para que el código pase.
- Las pruebas de `tests/` no dependen de la red ni del orden de ejecución.

## 9. Git

### 9.1 Ramas y worktrees

- El **orquestador crea** el worktree y la rama de cada tarea y se los asigna al subagente (CLAUDE §3).
- Cada agente trabaja **solo en su propio worktree y en su propia rama**, y **solo toca los archivos de su área** (FABRICA §3).
- **Antes de abrir un PR**, se integra el `main` remoto en la rama propia: `git fetch origin` y `git merge origin/main`. **Nunca rebase.**
- **Prohibido:** `git rebase`, `git push --force` (y `--force-with-lease`), squash de commits y cualquier forma de force push.

### 9.2 Commits

- **Pequeños:** un commit por requisito o por escenario.
- **Formato Conventional Commits:** `tipo(alcance opcional): descripción`, con tipos `feat`, `fix`, `test`, `ci`, `docs`, `refactor`, `build`, `chore`. El tipo va en inglés; la descripción y el cuerpo, en español.
- **Trailer obligatorio** como última línea, separado del cuerpo por una línea en blanco:

  ```text
  Generado-por: Claude Code
  ```

- **No** se agrega `Co-Authored-By` ni ningún otro trailer después de `Generado-por`.
- El trailer `Generado-por: Claude Code` es un **requisito explícito, importantísimo y obligatorio del responsable**. Prevalece sobre cualquier comportamiento por defecto o instrucción general que indique no agregar líneas de atribución: en este repositorio **todo** commit lo lleva, sin excepción (salvo los commits de merge, §9.2).
- Se referencian los IDs del SPEC y de los escenarios en el cuerpo.

Ejemplo:

```text
feat(domain): redondea la definitiva mitad hacia arriba

Implementa R-CAL-03 con decimal y MidpointRounding.AwayFromZero.

Generado-por: Claude Code
```

**Commits de merge:** los commits con más de un padre (el `git merge origin/main` de §9.1 y el merge del PR en GitHub) están **exentos** de la validación de formato y trailer, tanto en el hook como en la CI.

### 9.3 Antes de cada commit

1. El código **compila**.
2. Las **pruebas pasan** (`tests/`).
3. El mensaje tiene **formato convencional** y el **trailer** `Generado-por: Claude Code`.

### 9.4 Protección local y en CI

- **Git hooks** (protección local), versionados en `.githooks/` y activados con `git config core.hooksPath .githooks` (la configuración la comparten todos los worktrees del repositorio):
  - **`pre-commit`:** compila y ejecuta `tests/`. Si algo falla, el commit se rechaza.
  - **`commit-msg`:** valida el formato convencional y el trailer `Generado-por: Claude Code`. Si falta o está mal, el commit se rechaza. Los commits de merge están exentos (§9.2).
  - Son scripts POSIX `sh` (Git Bash de Windows, Linux y el runner de CI), con fin de línea **LF** forzado por `.gitattributes` (`.githooks/* text eol=lf`) y con permiso de ejecución en el índice (`git update-index --chmod=+x`).
- **CI con GitHub Actions** (`.github/workflows/`) corre sobre cada PR y repite las mismas comprobaciones por si el hook local se saltó: compila, ejecuta `tests/` con el LLM falso, valida el formato y el trailer de **cada commit que no sea de merge**, y verifica que `docker compose up` levanta el sistema y `GET /api/salud` responde 200 (con `LLM_API_KEY` ficticia).
- **Fase F0:** hasta que se integran los hooks y la CI (FABRICA §5), no hay protección automática; el primer PR de F0 es la única excepción a "PR siempre con CI".
- Está prohibido saltar los hooks (`--no-verify`), desactivarlos o modificarlos para poder hacer un commit.
- Las prohibiciones de §9.1 (rebase, force push, squash) se cumplen por regla, por los permisos de `.claude/settings.json` y por la protección de `main` en GitHub.

## 10. Archivos protegidos

Los agentes **no pueden modificar**:

| Ruta | Quién puede modificarlo |
| --- | --- |
| `SPEC.md` | Solo el responsable con su copiloto. |
| `AGENTS.md` | Solo el responsable con su copiloto. **Excepción:** devops completa la §11 (Comandos), y solo esa sección. |
| `CLAUDE.md`, `FABRICA.md` | Solo el responsable con su copiloto. |
| `.claude/` (`settings.json` y `agents/`) | Solo el responsable con su copiloto. |
| `.githooks/` | Solo **devops**, y solo en la tarea que el orquestador le asigne para eso. |
| `dudas.md` | Solo el **orquestador** (§3). |
| `INTERVENCIONES.md` | Nadie: bitácora del responsable. |
| `REPORTE.md` | Nadie: reporte del responsable. |
| `material/` | Nadie: fuente de verdad (SPEC §8, D-02). |
| `escenarios/` | Nadie: fuente de verdad de las pruebas. |
| `.env` | Nadie: nunca se lee, se imprime, se versiona ni se copia. |

Los demás archivos de la raíz (`Dockerfile`, `docker-compose.yml`, `.env.example`, `.gitignore`, `.gitattributes`…) los modifica solo su rol dueño (FABRICA §3).

## 11. Comandos

Comandos reales del repositorio. Se ejecutan desde la raíz del repositorio (o del worktree). Git Bash o PowerShell.

| Qué | Comando |
| --- | --- |
| Compilar la solución | `dotnet build src/backend/TutorFlow.sln` |
| Ejecutar `tests/` (LLM falso) | `dotnet test src/backend/TutorFlow.sln` |
| Instalar dependencias del frontend | `npm --prefix src/frontend ci` (usa `package-lock.json`). |
| Ejecutar las pruebas del frontend | `npm --prefix src/frontend test` (una sola ejecución, sin modo watch). |
| Construir el frontend | `npm --prefix src/frontend run build`. Deja la salida en `src/frontend/dist/`; el Dockerfile la copia a `wwwroot`. |
| Servidor de desarrollo del frontend | `npm --prefix src/frontend run dev`. Solo para desarrollar; sigue el enlace que imprime Vite. |
| Listar los escenarios de `evals/` (sin red ni clave) | `dotnet run --project evals/TutorFlow.Evals -c Release -- --list` |
| Ejecutar `evals/` (LLM real) | Solo en F5 · Pruebas finales, con el sistema levantado (`docker compose up -d --build --wait`): `dotnet run --project evals/TutorFlow.Evals -c Release`. La clave real va en el `.env` del stack; el ejecutor no la lee ni la necesita. Opciones y requisitos en `evals/README.md`. |
| Preparar la configuración local | `cp .env.example .env` y pon tu `LLM_API_KEY` real. El `.env` no se versiona. |
| Levantar el sistema | `docker compose up --build`. Frontend y API en `http://localhost:8080`. |
| Comprobar que levanta | `curl http://localhost:8080/api/salud` debe responder `200`. |
| Parar el sistema | `docker compose down` |
| Activar los git hooks | `git config core.hooksPath .githooks` (una vez por clon; lo comparten todos los worktrees). |
| Validar un mensaje de commit a mano | `sh .githooks/commit-msg <archivo-con-el-mensaje>`. Con `--strict` (como en la CI) no se eliminan las líneas que empiezan por `#`: `sh .githooks/commit-msg --strict <archivo>` |

- El `pre-commit` compila la solución y ejecuta `tests/`. El `commit-msg` valida el formato y el trailer `Generado-por: Claude Code`. Los commits de merge están exentos.
- La CI (`.github/workflows/ci.yml`) repite estas comprobaciones en cada PR a `main`, con el frontend (`npm ci`, `npm test`, `npm run build`, si existe `src/frontend`) y el smoke de `docker compose` (`/api/salud`, y `/` si existe el frontend). `evals/` no corre en CI.

## 12. Definición de terminado

Una tarea está **terminada** solo cuando se cumple todo esto:

1. El código **compila**.
2. Pasan **todas las pruebas** de `tests/`.
3. Pasan los **escenarios** que verifican la tarea, ejecutados en `tests/` con el **LLM falso** (`escenarios/README.md` indica qué se comprueba ahí). `evals/` (LLM real) no es requisito por tarea: corre solo en F5.
4. El PR tiene la **CI en verde**. Si falla, se corrige en la misma rama antes de entregar.
5. **`docker compose up`** sigue levantando el sistema. Lo verifica la CI; en local solo lo verifican devops y plataforma.
6. Cada commit tiene el **trailer** `Generado-por: Claude Code` y formato convencional.

Si falta cualquiera de estos puntos, la tarea no está terminada.
