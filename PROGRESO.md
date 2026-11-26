# Progreso · TutorFlow

Estado de la fábrica por fase (FABRICA §5). Lo actualiza el orquestador al cerrar cada fase.

| Fase | Estado | PR | Fecha de cierre |
| --- | --- | --- | --- |
| F0a · Esqueleto | Cerrada | #1 | 2026-11-26 |
| F0b · CI y contenedores | Cerrada | #2 | 2026-11-26 |
| F1a · Contratos | Cerrada | #4 | 2026-11-26 |
| F1b · Núcleo | Cerrada | #5, #6, #7, #8, #9, #10 | 2026-11-26 |
| F2 · Asistente y seguridad | Cerrada | #12, #13, #14, #15 | 2026-11-26 |
| F3 · Integración | Cerrada | #17 | 2026-11-26 |
| F4 · Frontend | Cerrada | #19, #20, #21 | 2026-11-26 |
| F5 · Pruebas finales | En curso | — | — |

PR de documentación del orquestador: #3 (cierre de F0), #11 (cierre de F1), #16 (cierre de F2), #18 (cierre de F3) y el de cierre de F4.

## F0 · Esqueleto, CI y contenedores (cerrada el 2026-11-26)

**Entregado**

- **F0a (plataforma, PR #1):** solución .NET 10 hexagonal (`Domain`, `Application`, `Infrastructure`, `Api`) con las referencias de AGENTS §5, proyectos xUnit de `tests/` y la biblioteca `TutorFlow.Testing`, configuración por variables de entorno con los valores por defecto del SPEC, arranque abortado con mensaje claro si falta `LLM_API_KEY` (R-DEP-02) y `GET /api/salud` → `200 {"estado":"ok"}`. Único PR sin CI. Revisor: sin bloqueantes.
- **F0b (devops, PR #2):** `Dockerfile` multi-stage (tolera que no exista `src/frontend`), `docker-compose.yml` con `mock-api` (red interna, solo lectura), `audit-init` (prepara `./var/auditoria` para el UID del backend) y `backend` en 8080 con health checks; `.env.example`, `.gitignore`, `.gitattributes`, hooks `pre-commit` y `commit-msg`, CI de GitHub Actions (build + tests con LLM falso, formato y trailer de cada commit, smoke de `docker compose` y `/api/salud`) y AGENTS §11. Revisor: con bloqueantes (volumen de auditoría no escribible sin paso manual) → corregido → sin bloqueantes. CI en verde.

**Dudas registradas:** DUDA-01 a DUDA-12 (`dudas.md`). Pendientes del responsable: DUDA-05 (protección de `main` en GitHub) y DUDA-11 (incidente `taskkill` de procesos Node).

## F1 · Contratos y núcleo (cerrada el 2026-11-26)

**Entregado**

- **F1a (nucleo, PR #4):** puertos de `Application/Ports/` (API académica con IDs validados, LLM con herramientas y uso de tokens, base de conocimiento con validación de citas, auditoría con los campos de R-LOG-08 y el catálogo de R-LOG-10, contexto de auditoría por petición, reloj), contrato de §5 con validación 400 (longitud en puntos de código tras recortar espacios) y 403 por formato de identidad, `AddApplication()` y caso de uso provisional que responde 503 sin inventar datos. Revisor: con bloqueantes (faltaba el contrato de auditoría por petición) → corregido → sin bloqueantes.
- **F1b · dominio (PR #8):** definitiva, acumulada, promedio (provisional y alternativo), inasistencia con enteros, habilitación, cancelación, becas y simulación, todo en `decimal` con `AwayFromZero`; evaluación completa por estudiante verificada contra los valores de referencia de §6.10 y los 24 escenarios unitarios (ESC-RAR-01..17, ESC-SIM-01..07) leídos desde `escenarios/`. Revisor: con bloqueantes (definitiva 3.8 en una asignatura perdida por inasistencia) → corregido (0.0) → sin bloqueantes.
- **F1b · datos (PR #6):** adaptador HTTP de la API simulada (timeout 5 s, IDs validados y escapados, dos tipos de 404 en logs, `null` ≠ 0, notas en `decimal`, fallos → 503) y doble en proceso desde `datos.json` con modo detenido y registro de llamadas.
- **F1b · conocimiento (PR #7):** base de conocimiento con índice de citas construido desde los tres documentos vigentes (2023 y Anexo excluidos), búsqueda determinista, Options del LLM con validación de precios, cálculo de costo de R-RES-05 y LLM falso con guiones, tokens, cuota agotada (429) y registro de peticiones.
- **F1b · plataforma (PR #5 y #9):** `Program.cs` invoca `AddApplication()` y `MapAssistant()`, adaptador de reloj UTC, abstracciones `Microsoft.Extensions.*` en Application en lugar de ASP.NET Core, y composición de `AddAcademicApi`, `AddKnowledge` y `AddLlm` retirando las Options provisionales.
- **F1b · devops (PR #10):** el corpus vigente viaja en la imagen (`KNOWLEDGE_CORPUS_PATH`), comprobado en el smoke de CI; variables opcionales documentadas en `.env.example`; validación estricta del trailer en la CI.

**Dudas registradas:** DUDA-13 a DUDA-22. Pendientes del responsable: DUDA-15 (`usuario.id` ausente o no textual), DUDA-16 y DUDA-18 (variables opcionales nuevas), DUDA-19 a DUDA-21 (criterios de dominio no definidos en el SPEC). Ninguna bloquea.

## F2 · Asistente y seguridad (cerrada el 2026-11-26)

**Entregado**

- **conocimiento (PR #12):** adaptador real de Gemini (`generateContent`) con herramientas, salida estructurada, firmas de razonamiento, temperatura 0, timeout de 30 s, uso de tokens y costo (R-RES-05); 429, 5xx, timeout y red → 503; la clave solo en la cabecera `x-goog-api-key`. La base de conocimiento se carga al arrancar. El LLM falso valida D-32 también entre turnos (configurable).
- **seguridad (PR #13):** contexto de auditoría por petición, `FileAuditLog` (stdout + `var/auditoria/auditoria.jsonl`, campos exactos de R-LOG-08, sin datos sensibles), middleware con `X-Correlation-Id` en toda respuesta y un único registro por petición (también en 500), log en memoria para pruebas, barrera determinista de nombres e IDs (R-SEG-14) y defensas deterministas de inyección y extracción del sistema sin falsos positivos en los escenarios. Revisor: con bloqueantes (500 sin cabecera) → corregido → sin bloqueantes.
- **nucleo (PR #14):** caso de uso completo: identidad contra la API (R-SEG-04), autorización determinista de la matriz §4 (sin consultar al estudiante pedido), herramientas por rol con cálculos de Domain y números formateados, `rechazar`, bucle del LLM con límite configurable, validación de citas de documentos y de datos, tokens y costo, 503 sin inventar datos, y `DefenseGuard`, que conecta las defensas de seguridad antes del LLM. Revisor: con bloqueantes (defensas sin conectar) → corregido → sin bloqueantes.
- **plataforma (PR #15):** `Program.cs` compone auditoría, log de archivo, defensas y `UseAuditTrail()` al inicio del pipeline.

**Dudas registradas:** DUDA-23 a DUDA-32. Pendientes del responsable: DUDA-25, DUDA-26, DUDA-27, DUDA-29, DUDA-30, DUDA-32. Ninguna bloquea.

## F3 · Integración (cerrada el 2026-11-26)

**Entregado**

- **integracion (PR #17):** biblioteca `evals/TutorFlow.Scenarios` que lee `escenarios/` con la gramática del README (§6), resuelve los marcadores `<generar: …>` (§3.3) y aplica las reglas de comparación (§4.1), la comprobación global de citas (§4.3) y la tabla `tests/` frente a `evals/` (§4.4).
  - `tests/TutorFlow.Integration.Tests`: los **145 escenarios `api`** se ejecutan uno a uno contra el backend real en proceso con la API simulada en memoria, el LLM falso guionizado por escenario y el log en memoria. **145 de 145 aprobados, 0 excluidos.** Un escenario sin guion o sin comprobaciones falla de forma visible; los 403 resueltos antes del modelo fallan si se llama al LLM. Pruebas de punta a punta de auditoría (AC-03): `X-Correlation-Id`, un registro por petición con el status real y sin datos sensibles.
  - `evals/TutorFlow.Evals`: ejecutor de consola contra `docker compose` y el LLM real para F5 (fuera de la CI). Envía su propio `X-Correlation-Id`, lee el registro en `var/auditoria/auditoria.jsonl`, detiene y vuelve a levantar `mock-api` para `api-simulada-detenida`, nunca ejecuta `llm-cuota-agotada` y escribe el informe en `var/evals/`. No necesita la clave: comprueba antes con una pregunta real que el backend tiene un LLM operativo.
  - Revisor: con bloqueantes (el ejecutor exigía la clave en su entorno) → corregido → sin bloqueantes.
- **Verificación del orquestador:** `docker compose up` sobre `main` con una clave ficticia levanta `mock-api`, `audit-init` y `backend` sanos; `/api/salud` = 200 con `X-Correlation-Id`; un ID inexistente da el 403 exacto de §5.0 y deja un registro de auditoría con los campos de R-LOG-08.

**Dudas registradas:** DUDA-33 a DUDA-35. Pendiente del responsable: DUDA-33. Ninguna bloquea.

## F4 · Frontend (cerrada el 2026-11-26)

**Entregado**

- **frontend (PR #21):** React 19 + TypeScript estricto con Vite (salida en `src/frontend/dist/`). Selector con los 8 usuarios de prueba (R-FRO-01), pregunta limitada a 1 000 caracteres y sin envío vacío (R-FRO-02), chat solo visual sin historial (R-FRO-03), citas sin inventar (R-FRO-04), tokens y costo con 6 decimales (R-FRO-05), errores 400/403/503 con el texto del backend y diferenciados (R-FRO-06), y limpieza del chat al cambiar de usuario descartando respuestas tardías (R-FRO-07). Solo llama a `/api/*` del mismo origen. 12 pruebas con Vitest.
- **plataforma (PR #19):** el backend sirve `wwwroot` en el mismo puerto con fallback a `index.html` solo fuera de `/api` y para rutas que no son archivos (D-14, R-DEP-01). Revisor: con bloqueantes (el fallback capturaba los JS/CSS) → corregido con `:nonfile` → sin bloqueantes.
- **devops (PR #20):** la imagen compila el frontend y lo copia a `wwwroot`; la CI tiene un job de frontend (`npm ci`, `npm test`, `npm run build`) y el smoke comprueba `GET /` con HTML; `.env.example` documenta `AUDIT_LOG_PATH` y `Assistant__MaxLlmCalls`; AGENTS §11 con los comandos de frontend y de `evals/`.
- **Verificación del orquestador:** `docker compose up` sobre `main` levanta los servicios sanos; `/` sirve el frontend y `/api/salud` = 200.

**Dudas registradas:** DUDA-36 (resuelta).
