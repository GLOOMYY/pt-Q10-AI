import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { httpAssistant } from "./api/httpAssistant";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App assistant={httpAssistant} />
  </StrictMode>,
);
