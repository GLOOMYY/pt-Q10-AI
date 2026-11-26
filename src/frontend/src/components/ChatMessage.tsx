import type { ChatEntry } from "../domain/types";
import { formatCost } from "../domain/question";

/** Spanish title of each error kind, so errors differ visually and textually (R-FRO-06). */
const ERROR_TITLES = {
  400: "Solicitud no válida (400)",
  403: "Acceso denegado (403)",
  503: "Servicio no disponible (503)",
} as const;

/** One question with its answer, citations and consumption (R-FRO-03..06). */
export function ChatMessage({ entry }: { entry: ChatEntry }) {
  const { result } = entry;
  return (
    <li className="entry">
      <p className="question">
        <span className="label">Pregunta</span>
        {entry.question}
      </p>
      {result.kind === "network-error" ? (
        <div className="answer error error-network" role="alert">
          <strong>No se pudo conectar con el servidor</strong>
          <p>Revisa tu conexión e inténtalo de nuevo.</p>
        </div>
      ) : (
        <div
          className={result.kind === "ok" ? "answer" : `answer error error-${result.status}`}
          role={result.kind === "ok" ? undefined : "alert"}
        >
          {result.kind === "backend-error" && <strong>{ERROR_TITLES[result.status]}</strong>}
          <p>{result.body.respuesta}</p>
          {result.body.citas.length > 0 && (
            <ul className="citations" aria-label="Citas">
              {result.body.citas.map((c, i) => (
                <li key={`${c.documento}|${c.seccion}|${i}`}>
                  {c.documento} · {c.seccion}
                </li>
              ))}
            </ul>
          )}
          <p className="usage">
            Tokens: <span data-testid="tokens">{result.body.tokens}</span> · Costo: USD{" "}
            <span data-testid="cost">{formatCost(result.body.costoUsd)}</span>
          </p>
        </div>
      )}
    </li>
  );
}
