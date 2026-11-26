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
- **Respuesta:** _Resuelta: devops añadió el modo `--strict` de `commit-msg` y la CI lo usa (PR #10)._

## DUDA-13 · `Program.cs` no invoca `AddApplication()` ni `MapAssistant()`

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f1a (PR #4)
- **Referencia:** DUDA-01, AGENTS §5, SPEC §5
- **Tipo:** ambigüedad
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
- **Bloqueante:** no
- **Descripción:** (habría sido bloqueante al cablear `AddKnowledge()` en el arranque). `.dockerignore` excluye `material/` y el Dockerfile solo copia `src/backend/`, así que la imagen no contiene el corpus y `AddKnowledge()` fallaría al arrancar en Docker.
- **Qué quedó pendiente:** que el corpus llegue a la imagen.
- **Propuesta:** devops copia `material/reglamento` (solo los tres documentos vigentes o la carpeta completa; el 2023 no se indexa de todas formas) a `/app/material/reglamento` en la etapa de runtime, y documenta `KNOWLEDGE_CORPUS_PATH` si se usa otra ruta. Se hace junto con el cableado de plataforma.
- **Respuesta:** _Resuelta: el corpus vigente viaja en la imagen con `KNOWLEDGE_CORPUS_PATH` y la CI lo comprueba (PR #10)._

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

## DUDA-23 · Fórmula de costo duplicada en el LLM falso

- **Fecha:** 2026-11-26
- **Rol / rama:** conocimiento / conocimiento/f2 (PR #12)
- **Referencia:** R-RES-05, FABRICA §3
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `FakeLlmClient` (en `tests/TutorFlow.Testing/`) no puede reutilizar `LlmCostCalculator` (Infrastructure) porque `TutorFlow.Testing` solo referencia Application. La fórmula está duplicada y una prueba comprueba que ambas coinciden.
- **Qué quedó pendiente:** una única implementación de la fórmula.
- **Propuesta:** mover el cálculo de costo a Application (nucleo) para que lo compartan el adaptador real y el falso, sin que Testing dependa de Infrastructure.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta en una tarea posterior de nucleo y conocimiento; mientras tanto la prueba de coincidencia protege contra divergencias._

## DUDA-24 · Carpetas de pruebas del middleware y del adaptador de auditoría

- **Fecha:** 2026-11-26
- **Rol / rama:** seguridad / seguridad/f2 (PR #13)
- **Referencia:** FABRICA §3, R-LOG-01, R-LOG-07, R-LOG-09
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** FABRICA §3 da a seguridad `Api/Middleware/` e `Infrastructure/Audit/`, pero no las carpetas de pruebas correspondientes (`tests/TutorFlow.Api.Tests/` es de nucleo; `tests/TutorFlow.Infrastructure.Tests/Audit/` no tiene dueño).
- **Qué quedó pendiente:** nada; ver respuesta.
- **Propuesta:** asignar a seguridad `tests/TutorFlow.Api.Tests/Middleware/` y `tests/TutorFlow.Infrastructure.Tests/Audit/`, igual que datos y conocimiento tienen sus subcarpetas en `Infrastructure.Tests`.
- **Respuesta:** _Resuelta por el orquestador de forma provisional aplicando la propuesta; el responsable puede reflejarlo en FABRICA §3._

## DUDA-25 · Registro de auditoría con status 500 o 499

- **Fecha:** 2026-11-26
- **Rol / rama:** seguridad / seguridad/f2 (PR #13)
- **Referencia:** R-LOG-10, SPEC §5.0
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** ante una excepción no controlada se registra `status` 500 (y 499 si el cliente cierra la conexión) con `motivo` null, porque el catálogo cerrado de R-LOG-10 no tiene valor para esos casos. El registro no incluye el texto de la excepción.
- **Qué quedó pendiente:** nada fuera del catálogo.
- **Propuesta:** mantenerlo; el responsable decide si §5.0 debe definir una respuesta 500 con contrato y su motivo.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-26 · Partículas y palabras cortas en la barrera determinista

- **Fecha:** 2026-11-26
- **Rol / rama:** seguridad / seguridad/f2 (PR #13)
- **Referencia:** R-SEG-14
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** la barrera bloquea "cada palabra del nombre". Un nombre con partículas ("de", "la") bloquearía casi cualquier pregunta. Con los datos actuales no ocurre; no se excluyó ninguna palabra para no inventar la regla.
- **Qué quedó pendiente:** nada con los datos actuales.
- **Propuesta:** ignorar partículas comunes o palabras de menos de 3 letras si el responsable lo aprueba.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-27 · Restricción del `X-Correlation-Id` reutilizado

- **Fecha:** 2026-11-26
- **Rol / rama:** seguridad / seguridad/f2 (PR #13)
- **Referencia:** R-LOG-09, §5
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** R-LOG-09 dice que si la petición trae `X-Correlation-Id` se reutiliza. Seguridad solo lo reutiliza si es ASCII imprimible de 1 a 128 caracteres; si no, genera uno nuevo, para no escribir valores arbitrarios en el log ni en la cabecera de respuesta.
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantener la restricción (los evals envían un identificador único razonable).
- **Respuesta:** _(pendiente del responsable)_

## DUDA-28 · Variable `AUDIT_LOG_PATH`

- **Fecha:** 2026-11-26
- **Rol / rama:** seguridad / seguridad/f2 (PR #13)
- **Referencia:** R-LOG-07, R-DEP-02, DUDA-06
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** la ruta del log de auditoría es configurable con `AUDIT_LOG_PATH`, opcional, con valor por defecto `var/auditoria/auditoria.jsonl`. No figura en R-DEP-02.
- **Qué quedó pendiente:** documentarla en `.env.example`.
- **Propuesta:** devops la documenta como opcional (igual que DUDA-16 y DUDA-18).
- **Respuesta:** _Resuelta por el orquestador: se asigna a devops en F4._

## DUDA-29 · Límite de llamadas al LLM por pregunta

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f2 (PR #14)
- **Referencia:** AGENTS §7, R-DEP-02, R-RES-05
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el bucle del LLM tiene un tope (`AssistantOptions.MaxLlmCalls`, 8) para limitar el costo por pregunta; al agotarse responde 503 con los tokens ya gastados. R-DEP-02 no define variable para esto.
- **Qué quedó pendiente:** exponerlo por variable de entorno.
- **Propuesta:** mantener el valor por defecto sin variable nueva; el responsable decide si la añade a R-DEP-02.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-30 · Motivo de `rechazar` fuera del catálogo

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f2 (PR #14)
- **Referencia:** R-ARQ-04, R-LOG-10, D-29
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el esquema de `rechazar` restringe `motivo` a "datos de terceros", "intento de inyección" e "intento de extracción del sistema". Si el modelo manda otro texto o ninguno, se registra "datos de terceros".
- **Qué quedó pendiente:** nada.
- **Propuesta:** confirmar.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-31 · Respaldos `TryAdd` en `AddApplication()`

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f2 (PR #14)
- **Referencia:** AGENTS §5, FABRICA §3
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `AddApplication()` registra con `TryAdd` un LLM que siempre responde 503 y un contexto de auditoría vacío, para que el host compile y valide dependencias mientras faltan los registros reales. Los registros reales (Gemini con `AddHttpClient`, auditoría con `AddScoped`, PR #12 y #13) ganan en cualquier orden.
- **Qué quedó pendiente:** retirar los respaldos cuando todo esté cableado.
- **Propuesta:** nucleo los retira en F3 si ya no hacen falta.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta._

## DUDA-32 · Coordinador que busca un nombre sin coincidencias en sus programas

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f2 (PR #14)
- **Referencia:** R-SEG-05, R-SEG-12, ESC-COO-19
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** si un coordinador busca un nombre que no coincide con ningún estudiante de sus programas, se responde 403, igual que un ID inexistente, para no confirmar ni negar que exista. Un nombre mal escrito de un estudiante propio también acaba en 403.
- **Qué quedó pendiente:** nada.
- **Propuesta:** confirmar; alternativa: 200 con "sin coincidencias en tus programas".
- **Respuesta:** _(pendiente del responsable)_

## DUDA-33 · Orden de `log.estudiantesConsultados` en los escenarios

- **Fecha:** 2026-11-26
- **Rol / rama:** integracion / integracion/f3 (PR #17)
- **Referencia:** escenarios/README §4.1 ("lista exacta de IDs"), R-LOG-04
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** §4.1 dice "lista exacta de IDs" sin precisar si importa el orden. Con el LLM real el orden de consulta puede variar. Los ejecutores lo comparan como multiconjunto (mismos IDs, mismas veces, sin importar el orden).
- **Qué quedó pendiente:** nada.
- **Propuesta:** confirmar la comparación sin orden.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-34 · Escenarios 403 de coordinador guionizados sin `rechazar`

- **Fecha:** 2026-11-26
- **Rol / rama:** integracion / integracion/f3 (PR #17)
- **Referencia:** D-32, R-SEG-03, ESC-COO-16, ESC-COO-19
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** en `tests/`, ESC-COO-16 y ESC-COO-19 se guionizan con el modelo llamando a una herramienta de datos no autorizada (en lugar de `rechazar`), para probar el 403 por autorización de la herramienta. El resto de los 403 por `rechazar` lo invocan antes de cualquier herramienta de datos.
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantenerlo; el revisor del PR #17 lo valida.
- **Respuesta:** _Resuelta por el orquestador: se acepta si el revisor confirma que verifica lo que el escenario pide._

## DUDA-35 · `evals/` y la `LLM_API_KEY`

- **Fecha:** 2026-11-26
- **Rol / rama:** revisor / integracion/f3 (PR #17)
- **Referencia:** AGENTS §7 ("`evals/` la recibe por el entorno del proceso"), AGENTS §10 (`.env`), R-QA-01, F5
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** AGENTS §7 dice que `evals/` recibe la clave real por el entorno del proceso. Con la arquitectura actual, el ejecutor de `evals/` solo hace peticiones HTTP al backend levantado con `docker compose`, y quien usa la clave es el backend, que la lee del `.env` del responsable. Exigirla también en el ejecutor no aporta nada e impediría a la fábrica lanzar F5, porque ningún agente puede leer `.env`.
- **Qué quedó pendiente:** nada.
- **Propuesta:** el ejecutor no exige la clave; antes de empezar comprueba con una pregunta real que el backend responde 200 con `tokens > 0`. El responsable mantiene la clave real en el `.env` del stack. Si quiere, ajusta la frase de AGENTS §7.
- **Respuesta:** _Resuelta por el orquestador de forma provisional aplicando la propuesta (PR #17)._

## DUDA-36 · Scripts `test` y `build` del frontend que asume la CI

- **Fecha:** 2026-11-26
- **Rol / rama:** devops / devops/f4 (PR #20)
- **Referencia:** DUDA-07, R-DEP-01
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el job `frontend` de la CI y el Dockerfile asumen que `src/frontend/package.json` define `test` (ejecución única no interactiva con `CI=true`) y `build` (salida en `dist/`), y que `package-lock.json` está versionado (lo exigen `npm ci` y la caché de `setup-node`).
- **Qué quedó pendiente:** confirmarlo con el PR de frontend.
- **Propuesta:** frontend cumple esa convención; el orquestador se lo indicó.
- **Respuesta:** _Resuelta por el orquestador: se aplica la propuesta en F4._

## DUDA-37 · F5 bloqueada por saturación del proveedor del LLM

- **Fecha:** 2026-11-26
- **Rol / rama:** orquestador / —
- **Referencia:** F5, R-QA-01, R-QA-04, CL-23, §5.0 (503)
- **Tipo:** atasco (1 intento)
- **Bloqueante:** sí
- **Descripción:** con el sistema levantado con `docker compose` y la clave del `.env` del responsable, la primera pregunta documental respondió bien (200, cita del Artículo 5, 4 936 tokens). Las siguientes peticiones reciben de Gemini `HTTP 503: This model is currently experiencing high demand. Spikes in demand are usually temporary. Please try again later.` o agotan el timeout de 30 s. El backend responde correctamente 503 sin inventar datos, pero así `evals/` daría fallos falsos en casi todos los escenarios.
- **Qué quedó pendiente:** ejecutar `evals/` con el LLM real (F5).
- **Propuesta:** reintentar cuando el proveedor se normalice (el orquestador deja un sondeo periódico). Si persiste, el responsable puede valorar otro `LLM_MODEL` en su `.env` (sin cambiar el SPEC) o ejecutar F5 en otro momento.
- **Respuesta:** _(pendiente; continúa en DUDA-43)_

## DUDA-38 · Ejecución de `evals/` cortada por memoria del equipo

- **Fecha:** 2026-11-26
- **Rol / rama:** integracion / — (F5)
- **Referencia:** F5, R-QA-01
- **Tipo:** atasco (1 intento)
- **Bloqueante:** no
- **Descripción:** la primera ejecución completa de `evals/` (unos 140 escenarios enviados) fue detenida por el entorno con "the system is running low on memory" antes de escribir el informe. El equipo tenía 2,2 GB libres de 15,9 GB (WSL de Docker con unos 2,9 GB).
- **Qué quedó pendiente:** nada; el orquestador relanzó F5 por tandas (un archivo de escenarios por ejecución, `--file`) tras cerrar el servidor de compilación de .NET.
- **Propuesta:** en el equipo del responsable, ejecutar `evals/` por tandas o con más memoria libre.
- **Respuesta:** _Resuelta por el orquestador: ejecución por tandas._

## DUDA-39 · Reintento de errores transitorios del proveedor del LLM

- **Fecha:** 2026-11-26
- **Rol / rama:** orquestador / conocimiento/f5
- **Referencia:** R-QA-04, §5.0, CL-23, DUDA-37
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** en F5, Gemini devuelve a menudo `HTTP 503` ("high demand") en una de las llamadas de una pregunta, y la pregunta entera termina en 503. El SPEC exige 503 cuando el LLM no responde y un timeout de 30 s, pero no dice si se puede reintentar.
- **Qué quedó pendiente:** nada.
- **Propuesta:** reintentar solo errores transitorios del proveedor (500/502/503/504), pocas veces y con backoff corto, sin superar los 30 s por llamada; nunca 429 (cuota, CL-23).
- **Respuesta:** _Resuelta por el orquestador de forma provisional aplicando la propuesta; el responsable puede confirmarla o pedir que se retire._

## DUDA-40 · Posible prueba intermitente en `TutorFlow.Integration.Tests`

- **Fecha:** 2026-11-26
- **Rol / rama:** conocimiento / conocimiento/f5 (PR #25)
- **Referencia:** AGENTS §8 (las pruebas no dependen del orden ni de la red)
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el hook `pre-commit` falló una vez en 1 de las 197 pruebas de integración y pasó al repetirlo. El orquestador ejecutó la suite 3 veces sobre `main` (197/197 en las tres) sin reproducirlo. El equipo estaba con poca memoria en ese momento (DUDA-38).
- **Qué quedó pendiente:** identificar la prueba si vuelve a fallar.
- **Propuesta:** si reaparece, asignar a integracion su diagnóstico con el nombre exacto de la prueba y el error; no se marca ni se salta ninguna prueba.
- **Respuesta:** _(en observación)_

## DUDA-41 · ESC-INY-04: Output esperado 200 frente a la nota "si se detecta, 403"

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f5 (PR #26)
- **Referencia:** ESC-INY-04, escenarios/README §4 (solo se comprueba lo que aparece en el Output esperado)
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** el Output esperado de ESC-INY-04 exige `"status": 200`, pero sus Notas dicen "Si se detecta, 403". Se implementó 200: el texto codificado no se decodifica ni se ejecuta y no se fuga nada.
- **Qué quedó pendiente:** nada.
- **Propuesta:** mantener 200, porque el README dice que se comprueba el Output esperado; el responsable puede aclarar la nota del escenario.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-42 · Motivo de "no habilitable" no expuesto por Domain

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo / nucleo/f5b (PR #27)
- **Referencia:** R-HAB-01, R-HAB-02, DUDA-22
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** `HabilitationStatus.NotEligible` no dice por qué una asignatura no es habilitable (definitiva < 2.0, pérdida por inasistencia o tipo de asignatura). Application usa un texto neutro salvo para la pérdida por inasistencia, que Domain sí indica.
- **Qué quedó pendiente:** exponer el motivo desde Domain.
- **Propuesta:** dominio añade el motivo al resultado de habilitación en una tarea posterior; con los datos actuales no cambia ninguna respuesta.
- **Respuesta:** _Resuelta: Domain expone `CourseEvaluation.NotHabilitableReason` (PR #28) y Application lo usa para explicar el motivo (PR #29)._

## DUDA-43 · F5 detenida: memoria del equipo y saturación de Gemini

- **Fecha:** 2026-11-26
- **Rol / rama:** orquestador / —
- **Referencia:** F5, R-QA-01, DUDA-37, DUDA-38
- **Tipo:** atasco (3 intentos)
- **Bloqueante:** sí
- **Descripción:** se hicieron tres pasadas de `evals/` con el LLM real. Las dos primeras terminaron; la tercera se cortó tras banco, estudiante y coordinador porque Claude Code detuvo el proceso en segundo plano por memoria crítica del equipo ("the system is running low on memory") y pide no relanzarlo sin autorización. Además, en todas las pasadas una parte grande de los escenarios recibió 503 de Gemini ("This model is currently experiencing high demand") pese a los reintentos (PR #25). El stack de `docker compose` quedó detenido (`docker compose stop`) para liberar memoria; `var/auditoria` y los informes de `var/evals/` se conservan.
- **Qué quedó pendiente:** una pasada completa y limpia de `evals/` (sin 503 del proveedor) tras la última ronda de correcciones de nucleo.
- **Propuesta:** el responsable libera memoria (o arranca Claude Code con `CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1`) y relanza: `docker compose up -d --build --wait` y luego `dotnet run --project evals/TutorFlow.Evals -c Release -- --file <archivo>` por cada archivo de escenarios, en un momento de menor demanda de Gemini o con otro `LLM_MODEL` en su `.env`.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-44 · Citas que el backend completa de forma determinista

- **Fecha:** 2026-11-26
- **Rol / rama:** nucleo y revisor / nucleo/f5c (PR #29)
- **Referencia:** R-ARQ-03, R-FUE-02, R-RES-01, R-RES-02, ESC-BAN-04, ESC-COO-01
- **Tipo:** ambigüedad
- **Bloqueante:** no
- **Descripción:** para ESC-BAN-04 y ESC-COO-01 el backend añade, validándolas con el índice, la cita de Evaluación Sección 2 cuando la respuesta cita la Sección 3 y menciona los pesos de los cortes, y la del Artículo 11 cuando los datos consultados tienen una pérdida por inasistencia y la respuesta la menciona. La segunda regla busca la palabra en toda la respuesta, así que podría añadir el Artículo 11 cuando la respuesta habla de inasistencia de otra asignatura o de otro estudiante del programa. Queda sin resolver la cita sobrante del Artículo 11 que el modelo propone a veces en ESC-BAN-04 (el fragmento sí se recupera).
- **Qué quedó pendiente:** confirmar el criterio y, si persiste en ESC-BAN-04, mejorar la ordenación de la búsqueda (conocimiento).
- **Propuesta:** mantener las reglas (cada cita añadida existe y se valida); afinar la del Artículo 11 a la asignatura perdida si los evals muestran falsos positivos.
- **Respuesta:** _(pendiente del responsable)_

## DUDA-45 · Cuota diaria de Gemini agotada

- **Fecha:** 2026-11-26
- **Rol / rama:** orquestador / —
- **Referencia:** F5, CL-23, DUDA-43
- **Tipo:** atasco (5 intentos)
- **Bloqueante:** sí
- **Descripción:** al reanudar F5 con el stack reconstruido sobre `main` (incluye el PR #29), Gemini responde `HTTP 429: You exceeded your current quota, please check your plan and billing details` en todas las peticiones durante más de 5 minutos (12 respuestas 429 seguidas). El backend responde 503 como exige CL-23, sin inventar datos. Es la cuota de la clave del `.env` del responsable, consumida por las tres pasadas anteriores.
- **Qué quedó pendiente:** la pasada completa y limpia de `evals/` que verifica los PR #26 a #29 con el LLM real. El stack quedó detenido (`docker compose stop`).
- **Propuesta:** el responsable amplía la cuota o el plan de la clave, o espera al reinicio diario de la cuota, y relanza: `docker compose up -d --build --wait` y `dotnet run --project evals/TutorFlow.Evals -c Release -- --file <archivo>` por cada archivo de escenarios (con memoria libre suficiente, DUDA-43).
- **Respuesta:** _(pendiente del responsable)_
