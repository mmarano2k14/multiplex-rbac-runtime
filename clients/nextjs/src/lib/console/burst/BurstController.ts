// lib/burst/BurstController.ts

import type { BurstConfig, BurstEvent, BurstModel } from "./BurstMachineType";
import { BurstPlans } from "./BurstPlans";
import type { RequestSpec } from "@/lib/http/HttpClientType";

export type ApiCallResult =
  | { kind: "ok"; status: number; durationMs: number }
  | { kind: "error"; status?: number; durationMs: number; message: string };

export interface IBurstApi {
  call(spec: RequestSpec): Promise<ApiCallResult>;
}

export class BurstController {
  private _stopRequested = false;

  constructor(private readonly dispatch: (ev: BurstEvent) => void) {}

  public configure(cfg: BurstConfig): void {
    this.dispatch({ type: "Configure", config: cfg });
  }

  public reset(): void {
    this._stopRequested = false;
    this.dispatch({ type: "Reset" });
  }

  public stop(): void {
    this._stopRequested = true;
    this.dispatch({ type: "Stop" });
  }

  public async start(api: IBurstApi, model: BurstModel): Promise<void> {
    const st = model.state;
    if (st === "Running" || st === "Stopping") return;

    this._stopRequested = false;
    this.dispatch({ type: "Start" });

    const report = model.report;
    if (!report) {
      this.dispatch({ type: "Fail", message: "Burst report missing" });
      return;
    }

    const cfg = report.config;
    const plan = BurstPlans.byKey(cfg.planKey);

    try {
      await this.runBurst(api, cfg, plan.makeRequest.bind(plan));
      this.dispatch({ type: "Finish" });
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      this.dispatch({ type: "Fail", message: msg });
    }
  }

  private async runBurst(
    api: IBurstApi,
    cfg: BurstConfig,
    make: (i: number) => RequestSpec
  ): Promise<void> {
    const total = Math.max(1, Math.floor(cfg.total));
    const concurrency = Math.max(1, Math.floor(cfg.concurrency));
    const delayMs = Math.max(0, Math.floor(cfg.delayMs));

    let nextIndex = 0;

    const workers: Promise<void>[] = [];
    for (let w = 0; w < concurrency; w++) {
      workers.push(this.workerLoop(api, total, delayMs, () => nextIndex++, make));
    }

    await Promise.all(workers);
  }

  private async workerLoop(
    api: IBurstApi,
    total: number,
    delayMs: number,
    next: () => number,
    make: (i: number) => RequestSpec
  ): Promise<void> {
    while (true) {
      if (this._stopRequested) return;

      const i = next();
      if (i >= total) return;

      this.dispatch({ type: "TickStart", count: 1 });

      try {
        if (delayMs > 0) await sleep(delayMs);

        const spec = make(i);
        const r = await api.call(spec);

        if (r.kind === "ok") {
          if (r.status >= 200 && r.status < 300) {
            this.dispatch({ type: "ResultOk", durationMs: r.durationMs });
          } else {
            this.dispatch({ type: "ResultHttp", status: r.status, durationMs: r.durationMs });
          }
        } else {
          if (typeof r.status === "number") {
            this.dispatch({ type: "ResultHttp", status: r.status, durationMs: r.durationMs });
          } else {
            this.dispatch({ type: "ResultError", message: r.message });
          }
        }
      } catch (e: unknown) {
        const msg = e instanceof Error ? e.message : String(e);
        this.dispatch({ type: "ResultError", message: msg });
      } finally {
        this.dispatch({ type: "TickComplete", count: 1 });
      }
    }
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}