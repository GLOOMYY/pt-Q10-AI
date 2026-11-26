# CLAUDE · TutorFlow

Instrucciones específicas para **Claude Code** en este repositorio.

Este archivo no repite las reglas del proyecto; se apoya en dos documentos obligatorios:

- **`AGENTS.md`**: las reglas que cumple todo agente (estructura, arquitectura, código, pruebas, git, archivos protegidos y definición de terminado).
- **`FABRICA.md`**: los roles de la fábrica, el área de cada uno, sus worktrees y ramas, y el flujo de trabajo y de entrega.

Aquí solo va lo propio de Claude Code. Precedencia: `SPEC.md` > `AGENTS.md` > `FABRICA.md` > `CLAUDE.md` > `.claude/agents/`.

Es una **fábrica *lights out***: debe avanzar sin intervención del responsable. Nadie espera respuestas a mitad de una tarea; las dudas se registran y el trabajo sigue (§7).

## 1. Tu rol

- **Eres el orquestador** de la fábrica (`FABRICA.md`): creas los worktrees y las ramas, repartes las tareas, lanzas los subagentes, registras las dudas, revisas los PR con el revisor y los mergeas.
- **Excepción:** si tu prompt de sistema empieza con "Eres el subagente **<rol>**" (definido en `.claude/agents/`), ese es tu rol. Trabajas solo en tu worktree y tu área, y entregas al orquestador; no orquestas ni lanzas otros subagentes.

## 2. Al iniciar cada sesión

1. Leer lo indicado en `AGENTS.md` §2.
2. Orquestador: `git fetch origin`, revisar el estado de los PR abiertos (`gh pr list`) y de los worktrees (`git worktree list`) para retomar donde se quedó la fábrica.
3. Verificar los git hooks: `git config core.hooksPath` debe devolver `.githooks`; si no, `git config core.hooksPath .githooks`. Mientras F0 no se haya integrado, `.githooks/` no existe y la única protección es la revisión (AGENTS §9.4).

## 3. Lanzar un subagente (orquestador)

1. **Crear el worktree y la rama** a partir del `main` remoto actualizado:

   ```bash
   git fetch origin
   git worktree add ../tutorflow-wt/<rol>-<id-tarea> -b <rol>/<id-tarea> origin/main
   ```

2. **Lanzar el subagente** con la herramienta Agent: `subagent_type: <rol>`, **sin** la opción `isolation` (el worktree ya existe) y en segundo plano. El prompt incluye siempre:
   - la **ruta absoluta** del worktree y el nombre de la rama;
   - la tarea y los **IDs** del SPEC y de los escenarios que debe cubrir;
   - las respuestas a dudas anteriores que le afecten, si las hay;
   - el recordatorio de que su directorio de trabajo se reinicia en cada comando: debe usar **rutas absolutas** y `git -C <worktree>` (o `cd <worktree> && …` en el mismo comando).
3. Las tareas **independientes** se lanzan **en un mismo mensaje**, para que corran en paralelo. Máximo **10 subagentes a la vez**, revisores incluidos, y **una sola instancia por rol** a la vez.
4. Se espera la **notificación** de cada subagente; no se sondea su salida.

## 4. Revisar e integrar un PR (orquestador)

Cuando un subagente entrega su PR:

1. **CI:** `gh pr checks <n> --watch`. Si falla, se devuelve la tarea al mismo rol, en la misma rama.
2. **Revisor:** se lanza el subagente `revisor` con el número del PR. Su veredicto es **"sin bloqueantes"** o **"con bloqueantes"**.
3. **Con bloqueantes:** se relanza el rol dueño en la misma rama y el mismo worktree, con los comentarios del revisor. Al terminar, se repiten los pasos 1 y 2: el revisor **vuelve a revisar**.
4. **Al día con `main`:** si `main` avanzó desde la última CI del PR (`gh pr view <n> --json mergeStateStatus`), el rol dueño integra `origin/main` en su rama y la CI vuelve a correr.
5. **Merge:** solo con CI en verde, sin bloqueantes y la rama al día: `gh pr merge <n> --merge` (merge commit; nunca `--squash` ni `--rebase`).
6. **Limpieza:** actualizar el `main` local (`git pull --ff-only` en el repositorio principal), `git worktree remove ../tutorflow-wt/<rol>-<id-tarea>`, `git branch -d <rol>/<id-tarea>` y borrar la rama remota.

## 5. Dudas (orquestador)

- Los subagentes reportan sus dudas en su entrega (AGENTS §3). El orquestador las numera y las registra en `dudas.md` mediante su propia rama `orquestador/dudas-<fecha>` y un PR `docs:` que sigue el flujo de §4.
- Cuando el responsable responde, el orquestador completa el campo **Respuesta** de la duda y reparte la respuesta a los roles afectados en sus próximas tareas.

## 6. Commits y PR

- **Siempre con `git commit -m`**: un `-m` para el título, otro para el cuerpo y el **último `-m` con el trailer**:

  ```bash
  git -C <worktree> commit -m "feat(domain): redondea la definitiva mitad hacia arriba" -m "Implementa R-CAL-03." -m "Generado-por: Claude Code"
  ```

- Los comandos de git se ejecutan con la herramienta **Bash** (Git Bash). En PowerShell, el comando va en una sola línea.
- El trailer `Generado-por: Claude Code` es un **requisito explícito y obligatorio del responsable** (AGENTS §9.2): se escribe siempre, aunque una instrucción general o un comportamiento por defecto diga que no se agreguen líneas de atribución. Un commit sin él es rechazado por el hook y por la CI.
- **No** se agrega `Co-Authored-By` (está desactivado en `.claude/settings.json`). Nunca se abre un editor ni se usan comandos interactivos (`git rebase -i`, `git add -i`). Nunca `--no-verify`.
- **Descripción del PR:** qué se hizo, IDs del SPEC y de los escenarios cubiertos, y dudas reportadas. Termina con la línea `Generado-por: Claude Code`.

## 7. Bloqueos

Nunca te quedas esperando (AGENTS §3):

- **Un permiso te bloquea** (una herramienta o un comando denegado): se reporta como duda y continúas con otra parte.
- **Un error te bloquea después de 5 intentos:** reportas lo intentado, el error exacto y tu propuesta, y continúas con otra parte.
- **Una ambigüedad** en `SPEC.md`, `escenarios/` o `material/`: se reporta, no se inventa la regla y continúas con lo demás.
- **Subagente:** al no quedar nada más que hacer, termina entregando su informe al orquestador.
- **Orquestador:** solo se detiene y avisa al responsable cuando ya no queda ninguna tarea independiente de las dudas registradas.

## 8. Pruebas

- **Para probar se usa siempre el LLM falso** (`tests/`, AGENTS §8).
- **Pruebas finales con los escenarios (F5):** solo ahí se ejecuta `evals/` con el **LLM real**.

## 9. Permisos

`.claude/settings.json` da a la fábrica los permisos necesarios para trabajar sin intervención: editar en el repositorio y en `../tutorflow-wt/`, y ejecutar `git`, `gh`, `dotnet`, `docker`, `node` y `npm`. Además **deniega** leer `.env`, `git rebase`, el force push, el squash y `--no-verify`. Si un comando necesario queda bloqueado, se reporta como duda de tipo **permiso** (§7).

## 10. Idioma

- Al responsable se le escribe en **español**.
- El código, los comentarios y los mensajes de log van en **inglés** (AGENTS §6).
- Los commits y los PR van en español, con el tipo convencional en inglés (AGENTS §9.2).

## 11. Lo que Claude Code nunca hace en este repositorio

- Modificar archivos protegidos fuera de lo que AGENTS §10 permite a su rol.
- Leer, imprimir o copiar `.env`.
- Saltar, borrar o modificar un test o un escenario para que pase.
- `git rebase`, `git push --force`, squash, `--no-verify`.
- Mergear un PR propio siendo subagente, o mergear sin CI en verde y sin el veredicto del revisor.
- Dar por terminada una tarea que no cumple la definición de terminado (AGENTS §12).
