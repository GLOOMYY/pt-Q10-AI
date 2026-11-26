import type { TestUser } from "./types";

/** The 8 test users of `material/README.md` (R-FRO-01). No login (D-10). */
export const TEST_USERS: readonly TestUser[] = [
  { id: "EST-001", name: "Laura Gómez", role: "estudiante" },
  { id: "EST-002", name: "Andrés Martínez", role: "estudiante" },
  { id: "EST-003", name: "Sofía Herrera", role: "estudiante" },
  { id: "EST-004", name: "Camilo Rojas", role: "estudiante" },
  { id: "EST-005", name: "Valentina Castro", role: "estudiante" },
  { id: "EST-006", name: "Mateo Vargas", role: "estudiante" },
  { id: "COORD-01", name: "Marcela Ríos", role: "coordinador" },
  { id: "COORD-02", name: "Julián Pardo", role: "coordinador" },
];

/** Spanish label of a role. */
export function roleLabel(role: TestUser["role"]): string {
  return role === "estudiante" ? "Estudiante" : "Coordinador";
}
