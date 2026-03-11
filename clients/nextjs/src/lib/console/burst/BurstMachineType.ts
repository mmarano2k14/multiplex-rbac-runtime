import { RequestSpec } from "@/lib/http/HttpClientType";

export type BurstState = "Idle" | "Running" | "Stopping" | "Completed" | "Error";

export type BurstPlanKey = "read" | "refund";

export type BurstConfig = {
  total: number;
  concurrency: number;
  delayMs: number;
  planKey: BurstPlanKey;
};

export type BurstCounters = {
  ok: number;
  unauthorized: number; // 401
  forbidden: number; // 403
  other: number; // other HTTP
  errors: number; // network/proxy/throw
};

export type BurstProgress = {
  started: number;
  completed: number;
  inFlight: number;
};

export type BurstTiming = {
  startedAt?: number;
  finishedAt?: number;
};

export type BurstStats = {
  durationsMs: number[];
  p50ms?: number;
  p95ms?: number;
};

export type BurstReport = {
  config: BurstConfig;
  progress: BurstProgress;
  counters: BurstCounters;
  timing: BurstTiming;
  stats: BurstStats;
  error?: string;
};

export type BurstModel = {
  state: BurstState;
  report: BurstReport | null;
  // "stop requested" signal (also mirrored by controller abort token)
  stopRequested: boolean;
};

export type BurstPlan = {
  key: BurstPlanKey;
  displayName: string;
  makeRequest(i: number): RequestSpec;
};

export type BurstEvent =
  | { type: "Configure"; config: BurstConfig } // updates config even if not running
  | { type: "Start" } // transitions -> Running, initializes report
  | { type: "Stop" } // transitions -> Stopping (soft)
  | { type: "TickStart"; count: number } // when N new requests start
  | { type: "TickComplete"; count: number } // when N requests complete (any outcome)
  | { type: "ResultOk"; durationMs: number }
  | { type: "ResultHttp"; status: number; durationMs: number }
  | { type: "ResultError"; message: string }
  | { type: "Finish" } // transitions -> Completed (or Idle)
  | { type: "Fail"; message: string }
  | { type: "Reset" };