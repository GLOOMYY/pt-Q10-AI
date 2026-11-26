# Reporte · TutorFlow

Fecha: 2026-11-26. Construido por una fábrica de agentes de Claude Code (un orquestador y diez roles de subagente) según `SPEC.md`, `AGENTS.md`, `FABRICA.md` y `CLAUDE.md`. Detalle por fase en `PROGRESO.md`; decisiones y bloqueos en `dudas.md`.

## Resumen

- **Construcción:** 48 PR integrados en `main` con revisión y CI, fases F0 a F5, en unas 9,5 horas de fábrica y 4 horas de preparación del responsable.
- **Pruebas:** 837 pruebas automáticas en verde con el LLM falso, entre ellas los 145 escenarios `api` de punta a punta.
- **Evals con el LLM real (Gemini):** **141 de 145** en la última pasada completa. De los 4 restantes, 1 no se ejecuta por diseño (simula la cuota agotada del LLM) y 3 se corrigieron después (PR #44 y #45) y se verificaron uno a uno con Gemini. La disponibilidad del LLM (saturación, timeouts y cuota agotada de la capa gratuita) fue el principal reto de la construcción, e impidió repetir la pasada completa final.
- **Costo:** construcción ≈ 139,99 USD de costo estimado de API, cubierto por la suscripción de Claude Code (≈ 20 % de la cuota semanal); aplicativo ≈ 1,65 USD en Gemini durante todas las pruebas.
- **Operación:** unos 3 USD por cada 1000 preguntas (entre 2,4 y 3,2 USD según la base de cálculo).

## Corridas

### Fábrica (construcción)

- **48 PR** integrados en `main`, todos con revisión del rol `revisor` y merge commit; todos con CI en verde salvo el primero (F0a), que por regla no tenía CI.
- **219 commits** (144 sin contar merges).
- **88 ejecuciones de subagentes** sobre los 10 roles (implementación, correcciones y revisiones; las continuaciones de un mismo subagente no se cuentan aparte).
- **Fases F0 a F4** completas; **F5** cerrada por congelación del alcance (ver Limitaciones).

**Roles de la fábrica** (definidos en `FABRICA.md` y `.claude/agents/`):

| Rol | Modelo | Qué hizo |
| --- | --- | --- |
| orquestador | Opus | Sesión principal: creó worktrees y ramas, repartió tareas, lanzó subagentes, registró dudas, revisó con el revisor y mergeó los PR. No escribió código de producción. |
| plataforma | Sonnet | Esqueleto .NET 10 hexagonal (solución, proyectos y referencias), `Program.cs` con la composición y la configuración por entorno, reloj y servido del frontend. |
| dominio | Sonnet | Reglas académicas puras: definitiva, promedio, acumulada, inasistencia, habilitación, becas y simulación, con aritmética decimal exacta. |
| nucleo | Opus | Puertos, contrato de la API y validación, identidad y matriz de permisos, caso de uso del asistente con sus herramientas y `rechazar`, citas e instrucciones del modelo. |
| conocimiento | Sonnet | Base de conocimiento e índice de citas del reglamento vigente, adaptador real de Gemini y LLM falso de pruebas. |
| datos | Sonnet | Adaptador de la API simulada (timeouts, IDs, valores nulos) y su doble en memoria para pruebas. |
| seguridad | Sonnet | Barrera determinista de nombres e IDs, defensas de inyección y extracción del sistema, y log de auditoría con su middleware. |
| integracion | Sonnet | Ejecución de los escenarios de punta a punta en `tests/` y ejecutor de `evals/` con el LLM real. |
| devops | Haiku | Dockerfile, `docker-compose.yml`, `.env.example`, git hooks, CI de GitHub Actions y comandos de AGENTS §11. |
| frontend | Sonnet | Interfaz React + TypeScript: selector de usuario de prueba y chat con citas, tokens y costo. |
| revisor | Opus | Revisión de solo lectura de cada PR contra el SPEC, los escenarios y las reglas; veredicto "sin bloqueantes" o "con bloqueantes". |

### Pasadas de `evals/` con el LLM real (Gemini)

Cada pasada ejecuta los escenarios de nivel `api` de `escenarios/` contra el sistema levantado con `docker compose`. Costo y tokens: suma de `costoUsd` y `tokens` del registro de auditoría (`var/auditoria/auditoria.jsonl`) para las peticiones de esa pasada.

| Pasada         | Código desplegado           | Resultado                        | Peticiones |          Tokens |   Costo (USD) | Observaciones                                                                                         |
| -------------- | --------------------------- | -------------------------------- | ---------: | --------------: | ------------: | ----------------------------------------------------------------------------------------------------- |
| 0              | F0–F4                       | Sin informe                      |        144 |         252 635 |        0,0673 | Ejecutada por el rol integracion; el entorno la cortó por falta de memoria del equipo.                |
| 1              | F0–F4                       | 31 de 77 aprobados               |         85 |         196 500 |        0,0525 | Saturación de Gemini (503 "high demand"); cuatro archivos no llegaron a ejecutarse. 10 fallos reales. |
| 2              | + PR #25 (reintentos) y #26 | 83 de 145                        |        155 |         618 483 |        0,1646 | 44 fallos por saturación del proveedor; 17 fallos reales.                                             |
| 3 (parcial)    | + PR #27                    | 41 de 66                         |         69 |         429 821 |        0,1133 | Cortada por memoria tras 3 archivos. 3 fallos reales.                                                 |
| 4              | + PR #29 y clave nueva      | 130 de 145                       |        194 |       1 427 300 |        0,3744 | Incluye la reejecución lenta de los 503 por límite por minuto de la clave. 6 fallos reales y 8 por timeout de Gemini. |
| 5              | + PR #33 a #36              | 113 de 115 con respuesta del LLM |        151 |       1 043 247 |        0,2737 | 22 escenarios con 503 por cuota; `simulacion.md` sin ejecutar. 2 fallos reales.                       |
| 5 bis          | igual que la 5              | 137 de 145 (acumulado con la 5)  |       ≈ 45 | ≈ 0,41 M (est.) | ≈ 0,11 (est.) | Reejecución de los pendientes con la segunda clave nueva.                                             |
| 6              | hasta el PR #43             | **141 de 145**                   |      ≈ 170 | ≈ 1,57 M (est.) | ≈ 0,41 (est.) | Incluye la reejecución de los 503. 3 fallos reales, corregidos en los PR #44 y #45.                   |
| Verificaciones | PR #41 a #45                | Puntuales                        |       ≈ 30 | ≈ 0,28 M (est.) | ≈ 0,07 (est.) | ESC-COO-10, ESC-COO-12 y ESC-SIM-09 verificados tras los PR #44 y #45.                                |

En todas las pasadas 1 escenario no se ejecuta por diseño (`llm-cuota-agotada`, solo en `tests/`). Las peticiones incluyen las comprobaciones previas del ejecutor y los reintentos.

**Valores estimados (est.):** las pasadas 5 bis, 6 y las verificaciones se ejecutaron desde un worktree separado, cuyo registro de auditoría se perdió al borrarlo. Sus resultados están verificados (salieron de los informes antes de borrarlos); sus tokens y costo se estiman con el promedio medido por petición que llamó al LLM (9 213 tokens y 0,002428 USD).

## Horas

Propuesta:

| Quién       | Tramo              | Horas | Detalle                                                                                                     |
| ----------- | ------------------ | ----: | ----------------------------------------------------------------------------------------------------------- |
| Responsable | Preparación        |   4,0 | Material, `SPEC.md`, escenarios, `AGENTS.md`, `CLAUDE.md`, `FABRICA.md`, `.env` (commits de 00:53 a 04:38). |
| Responsable | Durante la fábrica | ≈ 1,0 | Intervenciones 16 a 23 de `INTERVENCIONES.md` (estimación del orquestador; a confirmar por el responsable). |
| Responsable | Cierre             |     — | Revisión de este reporte (intervención 24).                                                                 |
| Fábrica     | ≈ 04:40 a 14:05    |  ≈ 9,5 | Reloj de pared: 9 h 27 min según Claude Code (primer commit de la fábrica a las 04:43), de las que 4 h 36 min fueron de uso de la API. |

Reparto de la fábrica por fase, según los merges de `main`:

| Fase                                | Tramo         | Duración |
| ----------------------------------- | ------------- | -------: |
| F0 · Esqueleto, CI y contenedores   | 04:39 – 05:03 | ≈ 25 min |
| F1 · Contratos y núcleo             | 05:03 – 05:43 | ≈ 40 min |
| F2 · Asistente y seguridad          | 05:43 – 06:20 | ≈ 37 min |
| F3 · Integración                    | 06:20 – 06:46 | ≈ 26 min |
| F4 · Frontend                       | 06:46 – 07:02 | ≈ 16 min |
| F5 · Pruebas finales y correcciones | 07:02 – 14:00 |    ≈ 7 h |

En F5, buena parte del tiempo se fue en esperar cuota de Gemini y memoria del equipo y en ejecutar los evals con pausas de 12 a 20 s entre preguntas.

## Intervenciones

Propuesta basada en `INTERVENCIONES.md`:

| Momento                           | N.º | Intervenciones                                                                                                                                                                                                                                                                                                                                                                  |
| --------------------------------- | --: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Preparación (antes de la fábrica) |  13 | Archivos base, `.env` y `.env.example`, `.gitignore`, material, subida inicial; búsqueda de casos de prueba y contradicciones con un agente; `SPEC.md` con plantilla y afinado con agente; escenarios con agente; `AGENTS.md`, `CLAUDE.md`, `FABRICA.md`, `INTERVENCIONES.md` y `REPORTE.md`.                                                                                   |
| Durante la fábrica                |   8 | Dos actualizaciones de la clave del LLM por cuota agotada (16 y 19); respuestas a las dudas acumuladas en unas 4 horas (17); actualización de `.env` con los nuevos parámetros (18); trailer `Generado-por: Humano` para commits propios (20); resolución del conflicto de `.env.example` (21); consulta del estado de los agentes (22); congelación del alcance y cierre (23). |
| Cierre                            |   1 | Apoyo del orquestador para este reporte (24).                                                                                                                                                                                                                                                                                                                                   |

Además, durante la fábrica el responsable liberó memoria del equipo en dos ocasiones para que pudieran seguir los evals (DUDA-38 y DUDA-43).

Ninguna intervención corrigió código: las correcciones las hicieron los roles de la fábrica mediante PR revisados. Las 51 dudas están registradas en `dudas.md`; el responsable contestó 21 (19 decisiones y 2 actualizaciones de la clave del LLM) y la fábrica resolvió el resto dentro de las reglas, salvo las pendientes que se listan en Limitaciones.

## Tokens

### Construcción (fábrica de Claude Code)

| Modelo                                                                                         |      Entrada |       Salida | Lectura de caché | Escritura de caché | Costo (USD) |
| ---------------------------------------------------------------------------------------------- | -----------: | -----------: | ---------------: | -----------------: | ----------: |
| claude-opus-5-5 (orquestador, nucleo, revisor)                                                 |       26,3 k |        1,3 M |          350,0 M |              5,5 M |      125,04 |
| claude-sonnet-5-5 (plataforma, dominio, datos, conocimiento, seguridad, integracion, frontend) |          934 |      511,4 k |           38,8 M |              1,3 M |       12,28 |
| claude-haiku-5-5 (devops)                                                                      |        1,2 M |      268,7 k |           11,7 M |              3,6 M |        2,66 |
| **Total**                                                                                      | **≈ 1,23 M** | **≈ 2,08 M** |    **≈ 400,5 M** |       **≈ 10,4 M** |  **139,99** |

**Costo de la fábrica: 139,99 USD equivalentes a tarifa de API.** Se trabajó con la suscripción de Claude Code, que consumió cerca del 20 % de la cuota semanal; no hubo cobro por uso de API. La cifra sirve para comparar y para estimar el costo de repetir la construcción.

- **Opus (89 %, 125,04 USD):** el modelo más capaz y el más costoso; lo usaron el orquestador, nucleo y revisor. Casi todo su consumo es lectura de caché (350 M tokens): el orquestador mantuvo una sesión larga (≈ 9,5 h) con un contexto grande que se vuelve a leer en cada turno, sobre todo durante las ≈ 7 h de F5.
- **Sonnet (9 %, 12,28 USD):** equilibrio entre capacidad, costo y velocidad; lo usó la mayoría de los roles que escriben código.
- **Haiku (2 %, 2,66 USD):** el más rápido y económico; lo usó devops, cuyas tareas requieren menos razonamiento.

**Cómo se reduciría en una próxima corrida:**

- Orquestador con Sonnet, y Opus solo para el revisor y las decisiones de arquitectura.
- Sesiones del orquestador más cortas: una por fase, en lugar de una sola de 9 h que arrastra todo el contexto.
- Menos rondas de F5: en cada ronda, evals por muestra (las 10 preguntas oficiales y los críticos) y la pasada completa solo al final.

### Aplicativo en funcionamiento (Gemini, `gemini-3.1-flash-lite`)

| Origen                                                 |  Peticiones |      Tokens |
| ------------------------------------------------------ | ----------: | ----------: |
| Auditoría conservada (pasadas 0 a 5 y sondas manuales) |         841 |   4 035 207 |
| Pasadas y verificaciones sin auditoría (estimado)      |       ≈ 245 |    ≈ 2,26 M |
| **Total**                                              | **≈ 1 086** | **≈ 6,3 M** |

De las 841 peticiones auditadas: 273 respondieron 200, 227 dieron 403, 40 dieron 400, 300 dieron 503 (sobre todo por saturación y cuota del proveedor) y 1 dio 499. Los rechazos deterministas (validación, identidad, barrera) no llaman al LLM y cuestan 0 tokens.

## Costo total

| Concepto                                   |  Costo (USD) |
| ------------------------------------------ | -----------: |
| Construcción (fábrica de Claude Code)      |       139,99 |
| Aplicativo: Gemini, medido en la auditoría |         1,06 |
| Aplicativo: Gemini, estimado sin auditoría |       ≈ 0,59 |
| **Total**                                  | **≈ 141,64** |

Los 139,99 USD de construcción son un costo **estimado de API**: no se pagaron aparte, porque la fábrica corrió con la suscripción de Claude Code y consumió alrededor del 20 % de la cuota semanal. El costo de Gemini sí corresponde al uso real de la API, valorado con la tarifa de pago.

El costo de Gemini se calcula con la tarifa de pago de R-RES-05 (0,25 USD por millón de tokens de entrada y 1,50 USD por millón de tokens de salida, incluido el razonamiento), aunque se trabajó en la capa gratuita.

## Costo estimado por 1000 preguntas

Promedio por pregunta, según el registro de auditoría:

| Base                                         | Preguntas | Tokens por pregunta | Costo por pregunta (USD) | Tokens por 1000 | **Costo por 1000 (USD)** |
| -------------------------------------------- | --------: | ------------------: | -----------------------: | --------------: | -----------------------: |
| Todas las que llamaron al LLM (incluye 403 por `rechazar` y 503 a mitad de respuesta) | 438 | 9 213 | 0,002428 | ≈ 9,2 M | **≈ 2,43** |
| Respondidas con 200 (todas las auditadas)    |       273 |              11 102 |                 0,002952 |        ≈ 11,1 M |               **≈ 2,95** |
| Respondidas con 200 en la pasada 5 (código más reciente con auditoría) | 72 | 12 294 | 0,003241 | ≈ 12,3 M | **≈ 3,24** |

**Estimación: unos 3 USD por cada 1000 preguntas.** Una pregunta que se rechaza antes del modelo (400 o 403 determinista) cuesta 0, así que un tráfico real con rechazos baja el promedio. Los cambios posteriores a la auditoría (límite de 4 llamadas, PR #35; razonamiento `low`, PR #45) deberían reducirlo, pero no hay medición completa que lo confirme.

## Limitaciones

1. **F5 sin verificación completa tras el PR #45.** El PR cambia el nivel de razonamiento de Gemini y afecta a todas las respuestas. La última pasada completa (141 de 145) es anterior; después solo se verificaron escenarios puntuales. La pasada completa quedó bloqueada por la cuota de la clave (DUDA-51).
2. **Dependencia de la cuota y la disponibilidad del proveedor.** La capa gratuita de Gemini dio 503 ("high demand"), timeouts y 429 de cuota. Los evals tuvieron que ejecutarse con pausas y reintentos, y el sistema responde 503 sin inventar datos cuando el LLM no responde (CL-23). Para ejecutar todas las pruebas sin estas limitaciones se debe usar una API key con alta disponibilidad (plan de pago con cuota y límites por minuto suficientes), porque la clave de Gemini usada durante las pruebas presentó intermitencias.
3. **Resultados del LLM no deterministas.** Con temperatura 0 las respuestas varían entre ejecuciones; algunos escenarios fallaron en una pasada y aprobaron en la siguiente sin cambios de código.
4. **Correcciones basadas en reglas del backend.** Para estabilizar al modelo pequeño se añadieron reglas deterministas acotadas: completar citas validadas (DUDA-44), retirar citas fuera de tema de los Artículos 11 y 7 (DUDA-46 y DUDA-47, pendientes) y detectar por palabras si se usó la lista de estudiantes de un programa. Funcionan con los escenarios actuales, pero dependen de palabras clave.
5. **Límite de 4 llamadas al LLM por pregunta** (DUDA-29). Contiene el costo, pero una pregunta muy compleja que necesite más pasos responde con lo que tenga en la cuarta llamada o 503.
6. **Recursos del equipo.** La falta de memoria cortó varias pasadas de evals (DUDA-38 y DUDA-43).
7. **Auditoría perdida de las últimas pasadas.** Por un error del orquestador al borrar un worktree se perdieron el registro de auditoría y los informes de las pasadas 5 bis y 6 y de las verificaciones; sus costos en este reporte son estimados.
8. **Cifras de costo de `PROGRESO.md`.** Los costos por pasada que figuran allí están subestimados por un error de cálculo; los correctos son los de este reporte.
9. **Alcance del producto.** Sin autenticación real (D-10, identidad por selector); datos de prueba pequeños (6 estudiantes y 2 coordinadores); un solo periodo, por lo que la pérdida de cupo no es determinable (R-CAL-08).
10. **Pendientes del responsable** (`dudas.md`): DUDA-11 (incidente de `taskkill`), DUDA-46 y DUDA-47 (citas retiradas), DUDA-49 (AGENTS y CLAUDE siguen exigiendo solo el trailer de Claude Code), DUDA-50 (`LLM_THINKING_LEVEL` fuera de `.env.example` y de R-DEP-02) y DUDA-51 (pasada completa pendiente).

## Resultados de pruebas y evals

### Pruebas automáticas (`tests/`, LLM falso, en cada PR por CI)

| Proyecto                       | Pruebas | Qué cubre                                                                                                                                           |
| ------------------------------ | ------: | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| TutorFlow.Domain.Tests         |      57 | Reglas de cálculo y los 24 escenarios unitarios (ESC-RAR-01..17 y ESC-SIM-01..07), leídos desde `escenarios/`; valores de referencia de SPEC §6.10. |
| TutorFlow.Application.Tests    |     382 | Caso de uso, autorización, herramientas, citas, defensas de seguridad y auditoría.                                                                  |
| TutorFlow.Infrastructure.Tests |     141 | Adaptador de la API simulada, base de conocimiento, adaptador de Gemini y log de auditoría.                                                         |
| TutorFlow.Api.Tests            |      48 | Contrato de §5, validación 400, middleware de auditoría y composición.                                                                              |
| TutorFlow.Integration.Tests    |     197 | Los **145 escenarios `api`** de punta a punta, uno por caso, más las pruebas de auditoría y del ejecutor de evals.                                  |
| Frontend (Vitest)              |      12 | Selector, límite de la pregunta, citas, costo, errores y cambio de usuario.                                                                         |
| **Total**                      | **837** | Todas en verde en `main`.                                                                                                                           |

La CI comprueba además el formato y el trailer de cada commit, compila y prueba el frontend, y verifica que `docker compose up` levanta el sistema con `/api/salud` = 200 y el frontend en `/`.

### Evals con el LLM real

- **Evolución:** 31 de 77 → 83 de 145 → 130 de 145 → 137 de 145 → **141 de 145** (pasada 6, código hasta el PR #43). Cada ronda de fallos reales se asignó al rol dueño y se corrigió por PR revisado.
- **Pasada 6:** 141 de 145 aprobados; 1 no ejecutado por diseño. Los 3 fallos reales (ESC-COO-10, ESC-COO-12 y ESC-SIM-09) se corrigieron en los PR #44 y #45 y se verificaron uno a uno con Gemini.
- **Principales problemas encontrados y corregidos con los evals:**
  - El modelo usaba `rechazar` en preguntas legítimas o fuera del corpus.
  - El modelo elegía "sin información" aunque tenía los datos.
  - Faltaban cifras (desglose del promedio, notas, clases) y citas de reglas en las respuestas.
  - Un coordinador obtenía datos con solo el nombre de pila (R-SEG-12).
  - Había timeouts por bucles de razonamiento de Gemini con temperatura 0.
- **Seguridad:** no se detectó ninguna fuga de datos no autorizados en los informes conservados; ESC-COO-13 mostró datos de una estudiante autorizada a partir de solo el nombre de pila (R-SEG-12), corregido en el PR #35. Los 403 se resolvieron antes del modelo (barrera determinista, con 0 tokens) o con `rechazar`.
