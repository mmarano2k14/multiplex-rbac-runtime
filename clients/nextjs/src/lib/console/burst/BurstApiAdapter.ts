// lib/burst/BurstApiAdapter.ts

import { RequestSpec } from "@/lib/http/HttpClientType";

import type { MultiplexedRbacApi } from "@/lib/rbac/MultiplexedRbacApi";
import { ApiCallResult, IBurstApi } from "./BurstController";

/**
 * Adapte MultiplexedRbacApi à l'interface burst.
 *
 * Important:
 * - Le burst NE gère PAS la rotation.
 * - La rotation doit rester dans MultiplexedRbacApi / ClientSession (single source of truth).
 */
export class BurstApiAdapter implements IBurstApi {
  constructor(private readonly api: MultiplexedRbacApi) {}

  async call(spec: RequestSpec): Promise<ApiCallResult> {
    const startedAt = performance.now();

    try {
      const r = await this.api.call(spec);

      const durationMs = Math.max(0, performance.now() - startedAt);

      // Expected shape:
      // - { kind: "ok", response: { status: number, ... } }
      // - { kind: "error", error: { error: string, details?: string }, status?: number }
      if (r?.kind === "ok") {
        const status: number | undefined = r?.response?.status;
        if (typeof status !== "number") {
          return { kind: "error", durationMs, message: "Missing status in OK response" };
        }

        return { kind: "ok", status, durationMs };
      }

      const status: number | undefined = r?.response?.status ?? r?.response?.status;
      const msg =
        r?.error?.error
          ? `${r.error.error}${r.error.details ? `: ${r.error.details}` : ""}`
          : "Unknown error";

      return { kind: "error", status, durationMs, message: msg };
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      return { kind: "error", durationMs: Math.max(0, performance.now() - startedAt), message: msg };
    }
  }
}