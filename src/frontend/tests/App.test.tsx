import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "../src/App";
import { httpAssistant } from "../src/api/httpAssistant";
import { isSendable, limitQuestion, MAX_QUESTION_LENGTH } from "../src/domain/question";
import type { AskResult, AssistantPort, AssistantResponse } from "../src/domain/types";

function body(over: Partial<AssistantResponse> = {}): AssistantResponse {
  return { respuesta: "Respuesta de prueba", citas: [], usoDatosEstudiante: false, tokens: 12, costoUsd: 0.000375, ...over };
}

function fake(result: AskResult): AssistantPort & { ask: ReturnType<typeof vi.fn> } {
  return { ask: vi.fn().mockResolvedValue(result) };
}

async function ask(text: string) {
  await userEvent.type(screen.getByLabelText("Pregunta"), text);
  await userEvent.click(screen.getByRole("button", { name: "Enviar" }));
}

describe("R-FRO-02 question field", () => {
  it("rejects empty and whitespace-only questions", () => {
    expect(isSendable("")).toBe(false);
    expect(isSendable("   \n ")).toBe(false);
    expect(isSendable(" hola ")).toBe(true);
  });

  it("limits to 1000 code points, counting after trimming", () => {
    expect(Array.from(limitQuestion("a".repeat(1500)))).toHaveLength(MAX_QUESTION_LENGTH);
    expect(Array.from(limitQuestion("😀".repeat(1200)))).toHaveLength(MAX_QUESTION_LENGTH);
    expect(isSendable(" " + "a".repeat(1000))).toBe(true);
  });

  it("keeps the send button disabled while the field is blank", async () => {
    render(<App assistant={fake({ kind: "ok", body: body() })} />);
    const button = screen.getByRole("button", { name: "Enviar" });
    expect(button).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Pregunta"), "   ");
    expect(button).toBeDisabled();
  });
});

describe("R-FRO-04 and R-FRO-05 answer", () => {
  it("shows citations, tokens and cost with 6 decimals", async () => {
    const assistant = fake({
      kind: "ok",
      body: body({ citas: [{ documento: "reglamento_academico_2025.md", seccion: "Artículo 5" }], costoUsd: 0.000375 }),
    });
    render(<App assistant={assistant} />);
    await ask("¿Cuál es la nota mínima?");
    expect(await screen.findByText("reglamento_academico_2025.md · Artículo 5")).toBeInTheDocument();
    expect(screen.getByTestId("tokens")).toHaveTextContent("12");
    expect(screen.getByTestId("cost")).toHaveTextContent("0.000375");
  });

  it("shows zero cost with 6 decimals and no citations list when citas is empty", async () => {
    render(<App assistant={fake({ kind: "ok", body: body({ costoUsd: 0, tokens: 0 }) })} />);
    await ask("hola");
    expect(await screen.findByTestId("cost")).toHaveTextContent("0.000000");
    expect(screen.queryByLabelText("Citas")).not.toBeInTheDocument();
  });

  it("sends only the question and the selected user, without history", async () => {
    const assistant = fake({ kind: "ok", body: body() });
    render(<App assistant={assistant} />);
    await ask("primera");
    await screen.findByText("Respuesta de prueba");
    await ask("segunda");
    await waitFor(() => expect(assistant.ask).toHaveBeenCalledTimes(2));
    const [question, user] = assistant.ask.mock.calls[1]!;
    expect(question).toBe("segunda");
    expect(user).toMatchObject({ id: "EST-001", role: "estudiante" });
  });
});

describe("R-FRO-06 errors", () => {
  it.each([
    [400, "La solicitud no es válida."],
    [403, "No estás autorizado a obtener esa información."],
    [503, "Los datos no están disponibles en este momento. Intenta más tarde."],
  ] as const)("shows backend text for %i as an alert", async (status, text) => {
    render(<App assistant={fake({ kind: "backend-error", status, body: body({ respuesta: text }) })} />);
    await ask("pregunta");
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(text);
    expect(alert).toHaveClass(`error-${status}`);
  });

  it("maps statuses and network failures in the HTTP adapter", async () => {
    const user = { id: "EST-001", name: "x", role: "estudiante" } as const;
    const reply = (status: number, payload: unknown) =>
      vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ status, json: async () => payload }));
    reply(403, body({ respuesta: "No" }));
    expect(await httpAssistant.ask("p", user)).toMatchObject({ kind: "backend-error", status: 403 });
    reply(500, { title: "boom" });
    expect(await httpAssistant.ask("p", user)).toEqual({ kind: "network-error" });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("fail")));
    expect(await httpAssistant.ask("p", user)).toEqual({ kind: "network-error" });
    vi.unstubAllGlobals();
  });
});

describe("R-FRO-07 user change", () => {
  it("clears the chat and sends the new user's role and id", async () => {
    const assistant = fake({ kind: "ok", body: body() });
    render(<App assistant={assistant} />);
    await ask("pregunta de Laura");
    expect(await screen.findByText("pregunta de Laura")).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText("Usuario de prueba"), "COORD-01");
    expect(screen.queryByText("pregunta de Laura")).not.toBeInTheDocument();

    await ask("otra");
    await screen.findByText("otra");
    expect(assistant.ask.mock.calls[1]![1]).toMatchObject({ id: "COORD-01", role: "coordinador" });
  });

  it("lists the 8 users with id, name and role (R-FRO-01)", () => {
    render(<App assistant={fake({ kind: "ok", body: body() })} />);
    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(8);
    expect(options[0]).toHaveTextContent("EST-001 · Laura Gómez · Estudiante");
    expect(options[7]).toHaveTextContent("COORD-02 · Julián Pardo · Coordinador");
  });
});
