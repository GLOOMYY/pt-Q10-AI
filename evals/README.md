# evals/ · Pruebas con el LLM real (F5)

Ejecutor de los escenarios de nivel `api` de `escenarios/` contra el sistema levantado con `docker compose`, con el **LLM real** y el contenedor `mock-api` real (R-QA-01, AGENTS §8). **No corre en CI**: solo en **F5 · Pruebas finales**.

## Qué hace

- **Lee los escenarios** desde `escenarios/` con la gramática de `escenarios/README.md` §6; no los copia ni los redefine. Resuelve los marcadores `<generar: …>` exactamente según §3.3 (un marcador desconocido hace fallar el escenario).
- Las 10 preguntas de `material/preguntas_ejemplo.json` son los escenarios `ESC-BAN-01..10` (una prueba en `tests/` comprueba que coinciden); se ejecutan una sola vez, como `ESC-BAN`.
- Por cada escenario envía su propia cabecera `X-Correlation-Id` (única: `evals-<run>-<ID>`), busca el registro en `./var/auditoria/auditoria.jsonl` por ese `correlationId` (debe haber exactamente uno) y aplica **todas** las comprobaciones de la columna `evals/` de §4.4: `status`, `respuesta.igual/contiene/noContiene` (reglas de §4.1: sin mayúsculas ni tildes salvo `ñ`, límites numéricos), `citas.*` (multiconjunto), comprobación global de citas (§4.3), `usoDatosEstudiante`, `tokens`/`costoUsd` (`0` o `">0"`), `log.*` (§5.3) y `despues`.
- `log.estudiantesConsultados` de los 403 resueltos por `rechazar` (escenarios que verifican `R-ARQ-04`) no se comprueba aquí: solo en `tests/` (D-32).
- `api-simulada-detenida`: detiene el contenedor `mock-api` (`docker compose stop mock-api`) una sola vez para todos esos escenarios y lo vuelve a levantar y esperar a que esté sano (siempre, también si hay error). Si `docker` no está en el `PATH` o `mock-api` no se puede detener o relevantar, esos escenarios se marcan como fallidos con el motivo y el informe se escribe igual.
- `llm-cuota-agotada` **nunca** se ejecuta aquí (solo `tests/`); se informa como "no ejecutado". Los escenarios `unitario` tampoco (solo `tests/`).
- Produce un informe por consola y dos archivos en `var/evals/` (ignorado por git): `informe-<run>.md` y `informe-<run>.json`, con aprobado/fallido por escenario, agrupado por archivo de escenarios, el motivo de cada fallo y los requisitos afectados (campo **Verifica**).

Código de salida: `0` todo aprobado, `1` algún fallo, `2` no se pudo empezar (sistema no levantado, sin LLM real operativo, etc.).

## Requisitos

1. **Sistema levantado:** `docker compose up -d --build --wait` (backend en `http://localhost:8080`, auditoría en `./var/auditoria/`).
2. **Clave real en el `.env` del stack:** el responsable configura la `LLM_API_KEY` real en el `.env` que usa `docker compose`; solo el backend la usa. El ejecutor no la necesita, no la lee y no la imprime. Antes de empezar envía una pregunta de comprobación (la de `ESC-BAN-01`) y, si responde 503 o con `tokens = 0`, termina con código 2 y el mensaje "el backend no tiene un LLM real operativo".
3. .NET 10 SDK y `docker` en el `PATH`.

## Cómo se ejecuta

Desde la raíz del repositorio (PowerShell o Git Bash), con el sistema levantado:

```bash
dotnet run --project evals/TutorFlow.Evals -c Release
```

Opciones útiles:

| Opción | Efecto |
| --- | --- |
| `--list` | Solo lee y lista los escenarios (sin red ni clave). Sirve para comprobar el análisis. |
| `--only ESC-BAN-01,ESC-BAN-02` | Solo esos escenarios. |
| `--file banco.md,citas.md` | Solo los escenarios de esos archivos. |
| `--base-url`, `--audit-file`, `--report-dir` | Cambian la URL del sistema, el archivo de auditoría o el directorio del informe. |
| `--delay-ms`, `--timeout-s` | Pausa entre escenarios (500 por defecto) y timeout de cada petición (120 s). |

## Estructura

- `TutorFlow.Scenarios/`: biblioteca compartida con `tests/` (lector de escenarios, `<generar>`, reglas de comparación, comprobador por sitio). Es la única definición de las reglas de `escenarios/README.md`.
- `TutorFlow.Evals/`: el ejecutor (consola). Puertos `IAuditSource` (archivo `auditoria.jsonl`) e `IMockApiSwitch` (`docker compose`), `EvalRunner` e informe.

`tests/TutorFlow.Integration.Tests` ejecuta los mismos escenarios en proceso con el **LLM falso** (columna `tests/` de §4.4) y prueba el propio `EvalRunner` sin LLM real.
