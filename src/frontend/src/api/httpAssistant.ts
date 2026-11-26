import type { AskResult, AssistantPort, AssistantResponse, TestUser } from "../domain/types";

/** Same-origin endpoint. The frontend never calls the simulated API (API-03, D-14). */
const ENDPOINT = "/api/asistente/preguntar";

function isCitation(value: unknown): boolean {
  if (typeof value !== "object" || value === null) return false;
  const c = value as Record<string, unknown>;
  return typeof c.documento === "string" && typeof c.seccion === "string";
}

function isAssistantResponse(value: unknown): value is AssistantResponse {
  if (typeof value !== "object" || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.respuesta === "string" &&
    Array.isArray(v.citas) &&
    v.citas.every(isCitation) &&
    typeof v.tokens === "number" &&
    typeof v.costoUsd === "number"
  );
}

/**
 * HTTP adapter of {@link AssistantPort}. Each request is independent: no history (R-RES-07).
 * Connection failures are `network-error`; any response that cannot be used (unforeseen
 * status or invalid body) is `unexpected-response`.
 */
export const httpAssistant: AssistantPort = {
  async ask(question: string, user: TestUser): Promise<AskResult> {
    let response: Response;
    try {
      response = await fetch(ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pregunta: question, usuario: { rol: user.role, id: user.id } }),
      });
    } catch {
      return { kind: "network-error" };
    }
    const body: unknown = await response.json().catch(() => null);
    if (!isAssistantResponse(body)) return { kind: "unexpected-response" };
    if (response.status === 200) return { kind: "ok", body };
    if (response.status === 400 || response.status === 403 || response.status === 503) {
      return { kind: "backend-error", status: response.status, body };
    }
    return { kind: "unexpected-response" };
  },
};
