import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import { RouterProvider } from "./state/RouterContext";
import { SessionProvider } from "./state/SessionContext";
import "./styles/app.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <RouterProvider basename="/app">
      <SessionProvider>
        <App />
      </SessionProvider>
    </RouterProvider>
  </StrictMode>
);
