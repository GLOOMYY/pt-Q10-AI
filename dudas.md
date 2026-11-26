# Dudas · TutorFlow

Registro de dudas, atascos y bloqueos de permisos de la fábrica (AGENTS §3). Solo lo escribe el orquestador. Las entradas se agregan al final y nunca se borran.

## DUDA-01 · `AddApplication()` todavía no existe

- **Fecha:** 2026-11-26
- **Rol / rama:** plataforma / plataforma/f0a
- **Referencia:** AGENTS §5, FABRICA §3
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `TutorFlow.Application` es área de nucleo, así que plataforma no creó `AddApplication()`. `Program.cs` solo invoca `AddInfrastructure()`.
- **Qué quedó pendiente:** la invocación de `AddApplication()` en `Program.cs`.
- **Propuesta:** nucleo crea `AddApplication()` en F1a y plataforma lo invoca desde `Program.cs` en una tarea posterior.
- **Respuesta:** _Resuelta por el orquestador dentro de las reglas vigentes: se aplica la propuesta (F1a nucleo, luego plataforma)._

## DUDA-02 · Adaptador de reloj pendiente del puerto `IClock`

- **Fecha:** 2026-11-26
- **Rol / rama:** plataforma / plataforma/f0a
- **Referencia:** FABRICA §3 (los puertos los define nucleo)
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `Infrastructure/Clock/` no se creó porque el puerto del reloj lo define nucleo en F1a.
- **Qué quedó pendiente:** el adaptador de reloj.
- **Propuesta:** plataforma crea el adaptador cuando nucleo publique el puerto.
- **Respuesta:** _Resuelta por el orquestador dentro de las reglas vigentes: se aplica la propuesta._

## DUDA-03 · `.gitignore` sin `bin/` ni `obj/`

- **Fecha:** 2026-11-26
- **Rol / rama:** plataforma / plataforma/f0a
- **Referencia:** FABRICA §3 (devops es dueño de `.gitignore`)
- **Tipo:** permiso (área de otro rol)
- **Bloqueante:** no
- **Descripción:** el `.gitignore` solo ignora `.env`; los artefactos de compilación pueden colarse en los commits.
- **Qué quedó pendiente:** ignorar `bin/` y `obj/`.
- **Propuesta:** devops los añade en F0b.
- **Respuesta:** _Resuelta por el orquestador: asignada a devops en F0b._

## DUDA-04 · Clases Options del LLM y de la API simulada fuera de su área

- **Fecha:** 2026-11-26
- **Rol / rama:** revisor / plataforma/f0a (PR #1)
- **Referencia:** FABRICA §3 ("cada clase Options vive en el área del adaptador que la usa"), R-DEP-02
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** F0a dejó `LlmOptions` y `MockApiOptions` en `Infrastructure/DependencyInjection.cs` (área de plataforma) porque las carpetas `Llm/` y `AcademicApi/` aún no existían. El revisor lo aceptó como provisional.
- **Qué quedó pendiente:** mover las Options a `Infrastructure/Llm/` (conocimiento) e `Infrastructure/AcademicApi/` (datos) con `AddLlm()` y `AddAcademicApi()`, y dejar `DependencyInjection.cs` solo invocándolos, conservando la validación de `LLM_API_KEY` y su prueba. Además, que un precio inválido o negativo falle en la validación en lugar de usar el valor por defecto.
- **Propuesta:** se planifica en F1b (conocimiento y datos) y una tarea posterior de plataforma.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta._

## DUDA-05 · `main` sin protección de rama en GitHub

- **Fecha:** 2026-11-26
- **Rol / rama:** orquestador / —
- **Referencia:** AGENTS §9.4 ("protección de `main` en GitHub")
- **Tipo:** permiso
- **Bloqueante:** no
- **Descripción:** `gh api repos/GLOOMYY/pt-Q10-AI/branches/main/protection` devuelve `404 Branch not protected`. AGENTS §9.4 cuenta con esa protección contra force push y merges sin CI.
- **Qué quedó pendiente:** configurar la protección (sin force push, sin borrado, CI obligatoria, merge commit).
- **Propuesta:** el responsable la configura en GitHub (es una configuración del repositorio fuera del área de cualquier rol). Mientras tanto, la fábrica cumple las prohibiciones por regla y por `.claude/settings.json`.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-06 · Ruta del archivo de auditoría dentro del contenedor

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** R-LOG-07, R-DEP-01
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el SPEC no define una variable para la ruta del log. El volumen `./var/auditoria` se monta en `/app/var/auditoria` (relativo al `WORKDIR` `/app`).
- **Qué quedó pendiente:** nada en F0b; seguridad debe escribir en `var/auditoria/auditoria.jsonl` relativo al directorio de trabajo.
- **Propuesta:** seguridad usa esa ruta relativa (configurable por Options con ese valor por defecto, sin variable nueva obligatoria).
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta y se pasa a seguridad en F2._

## DUDA-07 · Carpeta de salida del build del frontend

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** D-14, R-DEP-01
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** la etapa de frontend del Dockerfile asume que el build deja su salida en `src/frontend/dist/` y la copia a `wwwroot`.
- **Qué quedó pendiente:** confirmarlo en F4.
- **Propuesta:** frontend usa Vite con salida `dist/` (convención por defecto).
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta y se pasa a frontend en F4._

## DUDA-08 · `node:22-alpine` para la API simulada

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** R-DEP-01 ("`node:18-alpine` o superior")
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** se usó `node:22-alpine` porque Node 18 ya no tiene soporte. Cumple "o superior".
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantener `node:22-alpine`.
- **Respuesta:** _Resuelta por el orquestador: cumple R-DEP-01._

## DUDA-09 · Sin acceso local a los registros de imágenes Docker

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** AGENTS §12.5, F5 (`evals/` contra `docker compose`)
- **Tipo:** atasco
- **Bloqueante:** no (para F0–F4); **sí** para F5 si persiste
- **Descripción:** desde el equipo local no se resuelven `mcr.microsoft.com` ni `docker.io`, así que no se pudo construir la imagen ni levantar `docker compose` en local. La CI sí lo verificó (`backend-1 Healthy`, `GET /api/salud -> 200`).
- **Qué quedó pendiente:** verificación local de `docker compose up`. En F5, `evals/` necesita el sistema levantado con `docker compose` y el LLM real.
- **Propuesta:** el responsable revisa la red/DNS de Docker Desktop antes de F5; si no, F5 puede levantar el backend con `dotnet run` y la API simulada con `node` como alternativa documentada.
- **Respuesta:** _Comprobación del orquestador: desde la sesión principal `docker pull node:22-alpine` funciona; la falta de red era del entorno aislado del subagente. Para F5 el orquestador levanta `docker compose` desde la sesión principal si el subagente no puede._

## DUDA-10 · No se puede crear un `.env` de prueba en el worktree

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** CLAUDE §9, AGENTS §7
- **Tipo:** permiso
- **Bloqueante:** no
- **Descripción:** `.claude/settings.json` deniega `Write(**/.env)` en cualquier ruta, incluso un `.env` ficticio en un worktree. Devops validó con un override de compose en un directorio temporal. El `.env` del repositorio principal no se leyó ni se modificó.
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantener la regla; las pruebas locales usan variables de entorno del proceso o `--env-file` con otro nombre de archivo.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta._

## DUDA-11 · Incidente: `taskkill /IM node.exe /F` en el equipo del responsable

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f0b (PR #2)
- **Referencia:** —
- **Tipo:** atasco (incidente)
- **Bloqueante:** no
- **Descripción:** para limpiar sus pruebas, devops ejecutó `taskkill /IM node.exe /F`, que termina **todos** los procesos Node del equipo, no solo los suyos. Pudo afectar a otros procesos del responsable.
- **Qué quedó pendiente:** que el responsable revise si algún proceso Node suyo se cerró.
- **Propuesta:** en adelante los subagentes solo terminan procesos por PID propio; el orquestador lo indica en cada prompt.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-12 · `commit-msg` ignora las líneas que empiezan por `#`

- **Fecha:** 2026-11-26
- **Rol / rama:** devops y revisor / devops/f0b (PR #2)
- **Referencia:** AGENTS §9.2, §9.4
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `.githooks/commit-msg` elimina las líneas `#` antes de validar. Con `git commit -m` esas líneas se conservan, así que un mensaje con una línea `# …` después del trailer pasaría el hook y la CI (que usa el mismo script) aunque el trailer no sea la última línea real. Ningún commit actual tiene ese caso. El comentario de `ci.yml` que dice "no line is removed here" es inexacto.
- **Qué quedó pendiente:** un modo estricto en la CI y corregir el comentario.
- **Propuesta:** devops lo corrige en su próxima tarea (F4).
- **Respuesta:** _Resuelta por el orquestador: se asigna a devops en F4._
