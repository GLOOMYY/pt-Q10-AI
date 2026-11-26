import { useRef, useState, type FormEvent } from "react";
import { ChatMessage } from "./components/ChatMessage";
import { countChars, isSendable, limitQuestion, MAX_QUESTION_LENGTH } from "./domain/question";
import { roleLabel, TEST_USERS } from "./domain/users";
import type { AssistantPort, ChatEntry, TestUser } from "./domain/types";

/** Props of {@link App}. */
export interface AppProps {
  /** Backend port; injected so tests can use a test double. */
  assistant: AssistantPort;
}

/**
 * TutorFlow chat. The chat is visual only: each request carries just the question and
 * the selected user (R-FRO-03). Changing user clears the chat (R-FRO-07).
 */
export function App({ assistant }: AppProps) {
  const [user, setUser] = useState<TestUser>(TEST_USERS[0]!);
  const [text, setText] = useState("");
  const [entries, setEntries] = useState<ChatEntry[]>([]);
  const [loading, setLoading] = useState(false);
  /** Bumped on every user change so late answers of the previous user are discarded. */
  const session = useRef(0);
  const nextId = useRef(1);

  function changeUser(id: string) {
    const selected = TEST_USERS.find((u) => u.id === id);
    if (!selected) return;
    session.current += 1;
    setUser(selected);
    setEntries([]);
    setText("");
    setLoading(false);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (loading || !isSendable(text)) return;
    const question = text.trim();
    const current = session.current;
    setLoading(true);
    setText("");
    const result = await assistant.ask(question, user);
    if (current !== session.current) return;
    setEntries((prev) => [...prev, { id: nextId.current++, question, result }]);
    setLoading(false);
  }

  const count = countChars(text);
  return (
    <main>
      <h1>TutorFlow · Asistente académico</h1>
      <div className="field">
        <label htmlFor="user">Usuario de prueba</label>
        <select id="user" value={user.id} onChange={(e) => changeUser(e.target.value)}>
          {TEST_USERS.map((u) => (
            <option key={u.id} value={u.id}>
              {u.id} · {u.name} · {roleLabel(u.role)}
            </option>
          ))}
        </select>
      </div>

      <ol className="chat" aria-live="polite" aria-label="Conversación">
        {entries.map((e) => (
          <ChatMessage key={e.id} entry={e} />
        ))}
      </ol>
      {loading && <p role="status">Consultando al asistente…</p>}

      <form onSubmit={submit}>
        <label htmlFor="question">Pregunta</label>
        <textarea
          id="question"
          rows={3}
          value={text}
          onChange={(e) => setText(limitQuestion(e.target.value))}
          aria-describedby="counter"
        />
        <div className="row">
          <span id="counter">
            {count} / {MAX_QUESTION_LENGTH}
          </span>
          <button type="submit" disabled={loading || !isSendable(text)}>
            Enviar
          </button>
        </div>
      </form>
    </main>
  );
}
