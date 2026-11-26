import type { AskResult, AssistantPort, AssistantResponse, TestUser } from "../domain/types";

/** Same-origin endpoint. The frontend never calls the simulated API (API-03, D-14). */
const ENDPOINT = "/api/asistente/preguntar";

function isAssistantResponse(value: unknown): value is AssistantResponse {
  if (typeof value !== "object" || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.respuesta === "string" &&
    Array.isArray(v.citas) &&
    typeof v.tokens === "number" &&
    typeof v.costoUsd === "number"
  );
}

/** HTTP adapter of {@link AssistantPort}. Each request is independent: no history (R-RES-07). */
export const httpAssistant: AssistantPort = {
  async ask(question: string, user: TestUser): Promise<AskResult> {
    try {
      const response = await fetch(ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pregunta: question, usuario: { rol: user.role, id: user.id } }),
      });
      const body: unknown = await response.json().catch(() => null);
      if (!isAssistantResponse(body)) return { kind: "network-error" };
      if (response.status === 200) return { kind: "ok", body };
      if (response.status === 400 || response.status === 403 || response.status === 503) {
        return { kind: "backend-error", status: response.status, body };
      }
      return { kind: "network-error" };
    } catch {
      return { kind: "network-error" };
    }
  },
};
