"use client";

import React, { JSX, useState } from "react";
import { useRouter } from "next/navigation";
import { useConsoleContext } from "@/lib/console/contextProvider/useConsoleContext";

export default function LoginPage(): JSX.Element {
  const router = useRouter();
  const controller = useConsoleContext();

  const [username, setUsername] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function login(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    setError(null);

    try {
      const can = await controller.actions.login(username.trim());

      if (!can) {
        setError("Login failed.");
        return;
      }

      router.push("/dashboard");
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Login failed.");
    }
  }

  return (
    <div className="login-page">
      <div className="login-page__background" />

      <div className="login-shell">
        <div className="login-shell__brand">
          <div className="login-shell__eyebrow">Runtime Console</div>

          <h1 className="login-shell__title">
            Multiplexed RBAC Runtime Console
          </h1>

          <p className="login-shell__subtitle">
            Deterministic Access Context Rotation, live observability, burst
            testing, and runtime context inspection.
          </p>

          <div className="login-shell__chips">
            <span className="login-chip">Deterministic</span>
            <span className="login-chip">Realtime</span>
            <span className="login-chip">Context Rotation</span>
          </div>
        </div>

        <form className="login-card" onSubmit={login}>
          <div className="login-card__header">
            <div className="login-card__badge">Console Access</div>
            <h2 className="login-card__title">Sign in</h2>
            <p className="login-card__text">
              Enter a demo username to initialize the runtime session.
            </p>
          </div>

          <div className="login-form">
            <div className="login-field">
              <label htmlFor="username">Username</label>
              <input
                id="username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="marco"
                autoComplete="username"
                disabled={controller.state.busy}
              />
            </div>

            {error && <div className="login-error">{error}</div>}

            <button
              type="submit"
              className="login-submit"
              disabled={controller.state.busy || username.trim().length === 0}
            >
              {controller.state.busy ? "Logging in..." : "Login"}
            </button>
          </div>

          <div className="login-card__footer">
            <span className="login-card__hint">
              Session bootstraps target, user context, and runtime access key.
            </span>
          </div>
        </form>
      </div>
    </div>
  );
}