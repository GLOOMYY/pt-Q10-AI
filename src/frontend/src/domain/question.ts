/** Maximum question length in characters (R-FRO-02), counted as Unicode code points. */
export const MAX_QUESTION_LENGTH = 1000;

/** Counts Unicode code points, like the backend does. */
export function countChars(text: string): number {
  return Array.from(text).length;
}

/** Truncates the text to the maximum length without splitting a code point. */
export function limitQuestion(text: string): string {
  const chars = Array.from(text);
  return chars.length > MAX_QUESTION_LENGTH ? chars.slice(0, MAX_QUESTION_LENGTH).join("") : text;
}

/**
 * Whether the question can be sent: not empty or whitespace-only and, once trimmed,
 * at most {@link MAX_QUESTION_LENGTH} characters (R-FRO-02, same rule as the backend).
 */
export function isSendable(text: string): boolean {
  const trimmed = text.trim();
  return trimmed.length > 0 && countChars(trimmed) <= MAX_QUESTION_LENGTH;
}

/** Formats `costoUsd` in USD with exactly 6 decimals (R-FRO-05). */
export function formatCost(costUsd: number): string {
  return costUsd.toFixed(6);
}
