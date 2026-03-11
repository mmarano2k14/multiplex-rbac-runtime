"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useConsoleContext } from "@/lib/console/contextProvider/useConsoleContext";

export default function LoginPage() {
  const router = useRouter();
  const controller = useConsoleContext();

  const [username, setUsername] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function login(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    try {
      const can = await controller.actions.login(username);

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
    <div
      style={{
        height: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <form
        onSubmit={login}
        style={{
          width: 360,
          padding: 24,
          borderRadius: 12,
          background: "white",
          boxShadow: "0 10px 30px rgba(0,0,0,0.1)",
          display: "grid",
          gap: 14,
        }}
      >
        <h2>Multiplexed RBAC Demo</h2>

        <input
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          placeholder="username"
          style={{
            padding: 10,
            borderRadius: 8,
            border: "1px solid #ddd",
          }}
        />

        {error && <div style={{ color: "red", fontSize: 13 }}>{error}</div>}

        <button
          type="submit"
          disabled={controller.state.busy}
          style={{
            padding: 12,
            borderRadius: 8,
            border: "none",
            background: "#2962ff",
            color: "white",
            fontWeight: 600,
          }}
        >
          {controller.state.busy ? "Logging in..." : "Login"}
        </button>
      </form>
    </div>
  );
}