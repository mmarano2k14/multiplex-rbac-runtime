import type { ApiCallResult } from "../BurstController";
import type { WaveBatchesConfig } from "../BurstMachineType";
import type { BurstDispatchExecutionArgs, IBurstDispatchMode } from "./BurstDispatchModeType";
import { sleep } from "./BurstDispatchModeType";

/**
 * Sends fixed-size batches.
 * Each wave is fully awaited before the next one starts.
 */
export class WaveBatchesDispatchMode implements IBurstDispatchMode<WaveBatchesConfig> {
  public async execute(args: BurstDispatchExecutionArgs<WaveBatchesConfig>): Promise<void> {
    const { api, config, stopRequested, dispatch, makeRequest } = args;

    const total = Math.max(1, Math.floor(config.total));
    const batchSize = Math.max(1, Math.floor(config.batchSize));
    const delayMs = Math.max(0, Math.floor(config.delayMs));
    const wavePauseMs = Math.max(0, Math.floor(config.wavePauseMs));

    for (let start = 0; start < total; start += batchSize) {
      if (stopRequested()) return;

      const end = Math.min(start + batchSize, total);
      const tasks: Promise<void>[] = [];

      for (let i = start; i < end; i++) {
        if (stopRequested()) break;
        tasks.push(this.executeOne(api, i, delayMs, dispatch, makeRequest));
      }

      await Promise.all(tasks);

      const hasMore = end < total;
      if (hasMore && wavePauseMs > 0 && !stopRequested()) {
        await sleep(wavePauseMs);
      }
    }
  }

  private async executeOne(
    api: BurstDispatchExecutionArgs<WaveBatchesConfig>["api"],
    index: number,
    delayMs: number,
    dispatch: BurstDispatchExecutionArgs<WaveBatchesConfig>["dispatch"],
    makeRequest: BurstDispatchExecutionArgs<WaveBatchesConfig>["makeRequest"]
  ): Promise<void> {
    dispatch({ type: "TickStart", count: 1 });

    try {
      if (delayMs > 0) {
        await sleep(delayMs);
      }

      const spec = makeRequest(index);
      const result = await api.call(spec);

      this.dispatchResult(result, dispatch);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      dispatch({ type: "ResultError", message: msg });
    } finally {
      dispatch({ type: "TickComplete", count: 1 });
    }
  }

  private dispatchResult(
    result: ApiCallResult,
    dispatch: BurstDispatchExecutionArgs<WaveBatchesConfig>["dispatch"]
  ): void {
    if (result.kind === "ok") {
      if (result.status >= 200 && result.status < 300) {
        dispatch({ type: "ResultOk", durationMs: result.durationMs });
      } else {
        dispatch({
          type: "ResultHttp",
          status: result.status,
          durationMs: result.durationMs,
        });
      }

      return;
    }

    if (typeof result.status === "number") {
      dispatch({
        type: "ResultHttp",
        status: result.status,
        durationMs: result.durationMs,
      });
      return;
    }

    dispatch({ type: "ResultError", message: result.message });
  }
}