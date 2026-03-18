import type { ApiCallResult } from "../BurstController";
import type { MaintainedConcurrencyConfig } from "../BurstMachineType";
import type { BurstDispatchExecutionArgs, IBurstDispatchMode } from "./BurstDispatchModeType";
import { sleep } from "./BurstDispatchModeType";

/**
 * Keeps N workers active until the configured total is reached.
 */
export class MaintainedConcurrencyDispatchMode
  implements IBurstDispatchMode<MaintainedConcurrencyConfig>
{
  public async execute(
    args: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>
  ): Promise<void> {
    const { api, config, stopRequested, dispatch, makeRequest } = args;

    const total = Math.max(1, Math.floor(config.total));
    const concurrency = Math.max(1, Math.floor(config.concurrency));
    const delayMs = Math.max(0, Math.floor(config.delayMs));

    let nextIndex = 0;

    const workers: Promise<void>[] = [];
    for (let w = 0; w < concurrency; w++) {
      workers.push(
        this.workerLoop(
          api,
          total,
          delayMs,
          stopRequested,
          () => nextIndex++,
          dispatch,
          makeRequest
        )
      );
    }

    await Promise.all(workers);
  }

  private async workerLoop(
    api: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>["api"],
    total: number,
    delayMs: number,
    stopRequested: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>["stopRequested"],
    next: () => number,
    dispatch: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>["dispatch"],
    makeRequest: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>["makeRequest"]
  ): Promise<void> {
    while (true) {
      if (stopRequested()) return;

      const i = next();
      if (i >= total) return;

      dispatch({ type: "TickStart", count: 1 });

      try {
        if (delayMs > 0) {
          await sleep(delayMs);
        }

        const spec = makeRequest(i);
        const result = await api.call(spec);

        this.dispatchResult(result, dispatch);
      } catch (e: unknown) {
        const msg = e instanceof Error ? e.message : String(e);
        dispatch({ type: "ResultError", message: msg });
      } finally {
        dispatch({ type: "TickComplete", count: 1 });
      }
    }
  }

  private dispatchResult(
    result: ApiCallResult,
    dispatch: BurstDispatchExecutionArgs<MaintainedConcurrencyConfig>["dispatch"]
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