# Progreso · TutorFlow

Estado de la fábrica por fase (FABRICA §5). Lo actualiza el orquestador al cerrar cada fase.

| Fase | Estado | PR | Fecha de cierre |
| --- | --- | --- | --- |
| F0a · Esqueleto | ✅ Cerrada | #1 | 2026-11-26 |
| F0b · CI y contenedores | ✅ Cerrada | #2 | 2026-11-26 |
| F1a · Contratos | ⏳ En curso | — | — |
| F1b · Núcleo | Pendiente | — | — |
| F2 · Asistente y seguridad | Pendiente | — | — |
| F3 · Integración | Pendiente | — | — |
| F4 · Frontend | Pendiente | — | — |
| F5 · Pruebas finales | Pendiente | — | — |

## F0 · Esqueleto, CI y contenedores (cerrada el 2026-11-26)

**Entregado**

- **F0a (plataforma, PR #1):** solución .NET 10 hexagonal (`Domain`, `Application`, `Infrastructure`, `Api`) con las referencias de AGENTS §5, proyectos xUnit de `tests/` y la biblioteca `TutorFlow.Testing`, configuración por variables de entorno con los valores por defecto del SPEC, arranque abortado con mensaje claro si falta `LLM_API_KEY` (R-DEP-02) y `GET /api/salud` → `200 {"estado":"ok"}`. Único PR sin CI. Revisor: sin bloqueantes.
- **F0b (devops, PR #2):** `Dockerfile` multi-stage (tolera que no exista `src/frontend`), `docker-compose.yml` con `mock-api` (red interna, solo lectura), `audit-init` (prepara `./var/auditoria` para el UID del backend) y `backend` en 8080 con health checks; `.env.example`, `.gitignore`, `.gitattributes`, hooks `pre-commit` y `commit-msg`, CI de GitHub Actions (build + tests con LLM falso, formato y trailer de cada commit, smoke de `docker compose` y `/api/salud`) y AGENTS §11. Revisor: con bloqueantes (volumen de auditoría no escribible sin paso manual) → corregido → sin bloqueantes. CI en verde.

**Dudas registradas:** DUDA-01 a DUDA-12 (`dudas.md`). Pendientes del responsable: DUDA-05 (protección de `main` en GitHub) y DUDA-11 (incidente `taskkill` de procesos Node).
