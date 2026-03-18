import type { ApiCallResult } from "../BurstController";
import type { WaveBatchesStaggeredConfig } from "../BurstMachineType";
import type {
  BurstDispatchExecutionArgs,
  IBurstDispatchMode,
} from "./BurstDispatchModeType";
import { sleep } from "./BurstDispatchModeType";

/**
 * Sends fixed-size waves, but staggers the requests inside each wave.
 *
 * Important:
 * - The context key must be captured once per wave.
 * - All requests in the same wave must reuse that same key.
 */
export class WaveBatchesStaggeredDispatchMode
  implements IBurstDispatchMode<WaveBatchesStaggeredConfig>
{
  public async execute(
    args: BurstDispatchExecutionArgs<WaveBatchesStaggeredConfig>
  ): Promise<void> {
    const {
      api,
      config,
      stopRequested,
      dispatch,
      makeRequest,
      getCurrentContextKey,
    } = args;

    const total = Math.max(1, Math.floor(config.total));
    const batchSize = Math.max(1, Math.floor(config.batchSize));
    const delayMs = Math.max(0, Math.floor(config.delayMs));
    const wavePauseMs = Math.max(0, Math.floor(config.wavePauseMs));

    for (let start = 0; start < total; start += batchSize) {
      if (stopRequested()) return;

      const end = Math.min(start + batchSize, total);

      /**
       * Capture the context key ONCE for the whole wave.
       * Allow tetsting maximum inflight
       */
      const waveContextKey = getCurrentContextKey?.();

      const tasks: Promise<void>[] = [];

      for (let i = start; i < end; i++) {
        if (stopRequested()) break;

        const indexInWave = i - start;
        const staggerDelayMs = indexInWave * delayMs;

        tasks.push(
          this.executeOne(
            api,
            i,
            staggerDelayMs,
            waveContextKey,
            dispatch,
            makeRequest
          )
        );
      }

      await Promise.all(tasks);

      const hasMore = end < total;
      if (hasMore && wavePauseMs > 0 && !stopRequested()) {
        await sleep(wavePauseMs);
      }
    }
  }

  private async executeOne(
    api: BurstDispatchExecutionArgs<WaveBatchesStaggeredConfig>["api"],
    index: number,
    delayBeforeSendMs: number,
    waveContextKey: string | undefined,
    dispatch: BurstDispatchExecutionArgs<WaveBatchesStaggeredConfig>["dispatch"],
    makeRequest: BurstDispatchExecutionArgs<WaveBatchesStaggeredConfig>["makeRequest"]
  ): Promise<void> {
    dispatch({ type: "TickStart", count: 1 });

    try {
      if (delayBeforeSendMs > 0) {
        await sleep(delayBeforeSendMs);
      }

      const spec = makeRequest(index);

      const result = await api.call(spec, {
        contextKeyOverride: waveContextKey,
      });

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
    dispatch: BurstDispatchExecutionArgs<WaveBatchesStaggeredConfig>["dispatch"]
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