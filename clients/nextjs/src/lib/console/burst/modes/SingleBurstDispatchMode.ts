import type { ApiCallResult } from "../BurstController";
import type { SingleBurstConfig } from "../BurstMachineType";
import type { BurstDispatchExecutionArgs, IBurstDispatchMode } from "./BurstDispatchModeType";
import { sleep } from "./BurstDispatchModeType";

/**
 * Sends all requests immediately in a single burst.
 * No maintained concurrency, no wave logic.
 */
export class SingleBurstDispatchMode implements IBurstDispatchMode<SingleBurstConfig> {
  public async execute(args: BurstDispatchExecutionArgs<SingleBurstConfig>): Promise<void> {
    const { api, config, stopRequested, dispatch, makeRequest } = args;

    const total = Math.max(1, Math.floor(config.total));
    const delayMs = Math.max(0, Math.floor(config.delayMs));

    const tasks: Promise<void>[] = [];

    for (let i = 0; i < total; i++) {
      if (stopRequested()) break;
      tasks.push(this.executeOne(api, i, delayMs, dispatch, makeRequest));
    }

    await Promise.all(tasks);
  }

  private async executeOne(
    api: BurstDispatchExecutionArgs<SingleBurstConfig>["api"],
    index: number,
    delayMs: number,
    dispatch: BurstDispatchExecutionArgs<SingleBurstConfig>["dispatch"],
    makeRequest: BurstDispatchExecutionArgs<SingleBurstConfig>["makeRequest"]
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
    dispatch: BurstDispatchExecutionArgs<SingleBurstConfig>["dispatch"]
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