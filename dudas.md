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
- **Tipo:** atasco (1 intento)
- **Bloqueante:** no
- **Descripción:** (sería bloqueante para F5 si persistiera). Desde el equipo local no se resuelven `mcr.microsoft.com` ni `docker.io`, así que no se pudo construir la imagen ni levantar `docker compose` en local. La CI sí lo verificó (`backend-1 Healthy`, `GET /api/salud -> 200`).
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
- **Tipo:** atasco (1 intento)
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

## DUDA-13 · `Program.cs` no invoca `AddApplication()` ni `MapAssistant()`

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f1a (PR #4)
- **Referencia:** DUDA-01, AGENTS §5, SPEC §5
- **Tipo:** permiso
- **Bloqueante:** no
- **Descripción:** `Program.cs` es de plataforma. Mientras no los invoque, `POST /api/asistente/preguntar` no existe en la aplicación real; las pruebas de Api montan su propio servidor.
- **Qué quedó pendiente:** registrar `AddApplication()` y `MapAssistant()` en `Program.cs`.
- **Propuesta:** asignarlo a plataforma antes de F2.
- **Respuesta:** _Resuelta por el orquestador: asignada a plataforma en F1b (rama `plataforma/f1b`)._

## DUDA-14 · `FrameworkReference` de ASP.NET Core en `TutorFlow.Application`

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo y revisor / nucleo/f1a (PR #4)
- **Referencia:** AGENTS §5–6, FABRICA §3
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `AddApplication()` necesita las abstracciones de inyección de dependencias y `Directory.Packages.props` no tenía su versión, así que `TutorFlow.Application.csproj` referencia `Microsoft.AspNetCore.App`. Ningún tipo de ASP.NET Core se usa en Application, pero deja la dependencia al alcance de la capa.
- **Qué quedó pendiente:** cambiar a `PackageReference` de `Microsoft.Extensions.*`.
- **Propuesta:** plataforma agrega las versiones y cambia la referencia.
- **Respuesta:** _Resuelta por el orquestador: asignada a plataforma en F1b._

## DUDA-15 · `usuario.id` ausente o que no es texto

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f1a (PR #4)
- **Referencia:** SPEC §5.0, R-SEG-04, D-08, D-26, ESC-ENT-11, ESC-ENT-17
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** §5.0 da 400 solo si falta `pregunta` o `usuario` o el `rol` es inválido, y D-26 da 403 al ID vacío. No se define qué pasa si falta el campo `id` o si viene como número. Implementado: `id` ausente → 403 (como vacío); `id` no textual → 400 (cuerpo que no cumple el contrato, como ESC-ENT-11).
- **Qué quedó pendiente:** nada; es fácil de cambiar.
- **Propuesta:** confirmar este comportamiento.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-16 · Variable de entorno para el timeout de la API simulada

- **Fecha:** 2026-11-26
- **Rol / rama:** datos / datos/f1b (PR #6)
- **Referencia:** R-QA-04, R-DEP-02, AGENTS §7
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** R-QA-04 fija 5 s para la API simulada y AGENTS §7 pide que los timeouts se lean de la configuración, pero R-DEP-02 no define la variable. Datos usó `MOCK_API_TIMEOUT_SECONDS`, opcional, con valor por defecto 5.
- **Qué quedó pendiente:** documentarla (el SPEC solo lo edita el responsable).
- **Propuesta:** mantener la variable opcional con el valor por defecto del SPEC y que el responsable la añada a R-DEP-02 si lo desea; devops la documenta en `.env.example`.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-17 · La imagen Docker no incluye el corpus `material/reglamento`

- **Fecha:** 2026-11-26
- **Rol / rama:** conocimiento / conocimiento/f1b (PR #7)
- **Referencia:** R-FUE-01, R-DEP-01
- **Tipo:** ambigüedad
- **Bloqueante:** no (lo será al cablear `AddKnowledge()` en el arranque)
- **Descripción:** `.dockerignore` excluye `material/` y el Dockerfile solo copia `src/backend/`, así que la imagen no contiene el corpus y `AddKnowledge()` fallaría al arrancar en Docker.
- **Qué quedó pendiente:** que el corpus llegue a la imagen.
- **Propuesta:** devops copia `material/reglamento` (solo los tres documentos vigentes o la carpeta completa; el 2023 no se indexa de todas formas) a `/app/material/reglamento` en la etapa de runtime, y documenta `KNOWLEDGE_CORPUS_PATH` si se usa otra ruta. Se hace junto con el cableado de plataforma.
- **Respuesta:** _Resuelta por el orquestador: asignada a devops antes de cablear `AddKnowledge()`._

## DUDA-18 · Variable `KNOWLEDGE_CORPUS_PATH` fuera de R-DEP-02

- **Fecha:** 2026-11-26
- **Rol / rama:** conocimiento / conocimiento/f1b (PR #7)
- **Referencia:** R-DEP-02, AGENTS §7
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** la ruta del corpus es configurable con `KNOWLEDGE_CORPUS_PATH`, opcional; si no está, se busca `material/reglamento` junto al binario y hacia arriba. No figura en R-DEP-02.
- **Qué quedó pendiente:** documentarla.
- **Propuesta:** igual que DUDA-16: opcional, documentada por devops en `.env.example`; el responsable decide si la añade a R-DEP-02.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-19 · Criterios adicionales de "dato inconsistente"

- **Fecha:** 2026-11-26
- **Rol / rama:** dominio / dominio/f1b (PR #8)
- **Referencia:** R-CAL-12, D-20
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** R-CAL-12 nombra la fila de notas sin asistencia (o al revés) y la nota fuera de 0.0–5.0. Dominio también trata como inconsistentes los créditos faltantes o ≤ 0, las clases programadas faltantes o ≤ 0 y las inasistencias negativas, porque con ellos no se puede calcular. Ningún dato de prueba tiene estos casos.
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantenerlo; el responsable puede ampliar R-CAL-12 para nombrarlo.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-20 · Asignatura perdida por inasistencia con un corte sin nota

- **Fecha:** 2026-11-26
- **Rol / rama:** dominio / dominio/f1b (PR #8)
- **Referencia:** R-CAL-05, R-CAL-06
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** con faltas > 15 % y un corte `null`, R-CAL-06 ("0.0 sin importar las notas de los cortes") y R-CAL-05 ("sin corte no hay definitiva") chocan. Implementado: prevalece la pérdida por inasistencia (definitiva 0.0, entra en el promedio). Ningún estudiante de prueba tiene este caso.
- **Qué quedó pendiente:** nada.
- **Propuesta:** confirmar la precedencia de R-CAL-06.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-21 · Cuándo una conclusión provisional pasa a "no determinable"

- **Fecha:** 2026-11-26
- **Rol / rama:** dominio / dominio/f1b (PR #8)
- **Referencia:** R-CAL-10, R-BEC-01, R-BEC-02, D-05, D-23
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** R-CAL-10 pide informar como provisionales las conclusiones que dependen de un promedio provisional. Implementado: se evalúa con la asistencia desconocida como "no perdida" y como "perdida"; si ambos resultados coinciden se informa ese resultado como provisional (EST-003: no elegible para beca y sin prueba académica en ambos casos, como dice §6.10); si difieren, "no determinable". Las asignaturas inconsistentes (R-CAL-12) se tratan como las que no tienen definitiva para la beca (D-05: "no determinable").
- **Qué quedó pendiente:** nada.
- **Propuesta:** confirmar el criterio.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-22 · Tipo de asignatura no habilitable

- **Fecha:** 2026-11-26
- **Rol / rama:** dominio / dominio/f1b (PR #8)
- **Referencia:** R-HAB-02
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** la API simulada no indica si una asignatura es práctica profesional o trabajo de grado. La regla recibe ese dato del llamador y por defecto es "habilitable por tipo".
- **Qué quedó pendiente:** nada con los datos actuales.
- **Propuesta:** el caso de uso lo deja en "no es práctica ni trabajo de grado" mientras los datos no traigan el tipo; no se deduce por el nombre.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta (no se inventa el tipo)._
