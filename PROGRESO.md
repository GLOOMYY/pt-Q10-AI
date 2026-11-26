# Progreso · TutorFlow

Estado de la fábrica por fase (FABRICA §5). Lo actualiza el orquestador al cerrar cada fase.

| Fase | Estado | PR | Fecha de cierre |
| --- | --- | --- | --- |
| F0a · Esqueleto | Cerrada | #1 | 2026-11-26 |
| F0b · CI y contenedores | Cerrada | #2 | 2026-11-26 |
| F1a · Contratos | Cerrada | #4 | 2026-11-26 |
| F1b · Núcleo | Cerrada | #5, #6, #7, #8, #9, #10 | 2026-11-26 |
| F2 · Asistente y seguridad | En curso | — | — |
| F3 · Integración | Pendiente | — | — |
| F4 · Frontend | Pendiente | — | — |
| F5 · Pruebas finales | Pendiente | — | — |

PR de documentación del orquestador: #3 (dudas de F0 y este archivo) y el de cierre de F1.

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
