import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import "./shopping.css";
import "./payments.css";
import "./staff.css";
import "./premium-menu.css";
import App from "./App.tsx";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
