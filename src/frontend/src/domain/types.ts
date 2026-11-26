/** Role of the acting test user, as sent in `usuario.rol` (SPEC §5). */
export type Role = "estudiante" | "coordinador";

/** A fixed test user offered by the selector (R-FRO-01). */
export interface TestUser {
  /** User ID sent as `usuario.id`, e.g. `EST-001`. */
  id: string;
  /** Display name (test data from `material/mock-api/datos.json`). */
  name: string;
  /** Role sent as `usuario.rol`. */
  role: Role;
}

/** A citation: document plus section (R-FRO-04). */
export interface Citation {
  documento: string;
  seccion: string;
}

/** Response body of `POST /api/asistente/preguntar` (SPEC §5 and §5.0). */
export interface AssistantResponse {
  respuesta: string;
  citas: Citation[];
  usoDatosEstudiante: boolean;
  tokens: number;
  costoUsd: number;
}

/**
 * Outcome of asking a question.
 * - `ok`: HTTP 200.
 * - `backend-error`: 400, 403 or 503; `body.respuesta` is the backend text (R-FRO-06).
 * - `network-error`: no usable backend answer (network failure, other status, bad body).
 */
export type AskResult =
  | { kind: "ok"; body: AssistantResponse }
  | { kind: "backend-error"; status: 400 | 403 | 503; body: AssistantResponse }
  | { kind: "network-error" };

/** Port to the assistant backend. */
export interface AssistantPort {
  ask(question: string, user: TestUser): Promise<AskResult>;
}

/** One entry of the visual-only chat (R-FRO-03). */
export interface ChatEntry {
  id: number;
  question: string;
  result: AskResult;
}
