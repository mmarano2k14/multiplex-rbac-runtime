import {
  BurstPlanKey,
  MaintainedConcurrencyConfig,
  SingleBurstConfig,
  WaveBatchesConfig,
  WaveBatchesStaggeredConfig,
} from "@/lib/console/burst/runtime/BurstMachineType";
import { BurstScenarioDefinition, BurstScenarioKey } from "./BurstScenarioPresetType";

export class BurstScenarioFactory {
  static buildAll(
    planKey: BurstPlanKey = "read"
  ): Record<BurstScenarioKey, BurstScenarioDefinition> {
    return {
      "single-burst": this.singleBurst(planKey),
      "maintained-concurrency": this.maintainedConcurrency(planKey),
      "wave-batches": this.waveBatches(planKey),
      "wave-batches-staggered": this.waveBatchesStaggered(planKey),
    };
  }

  static singleBurst(planKey: BurstPlanKey): BurstScenarioDefinition {
    const burstConfig: SingleBurstConfig = {
      dispatchMode: "single-burst",
      planKey,
      total: 100,
      delayMs: 0,
    };

    return {
      key: "single-burst",
      title: "Single burst",
      maxInFlight: "1",
      rotationOverlapMs: "0",
      burstConfig,
      idea: "All requests are sent at the same time.",
      recommendedParameters: [
        { label: "Dispatch mode", value: "single-burst" },
        { label: "Total requests", value: 100 },
        { label: "Delay per request", value: 0 },
        { label: "Max In-Flight", value: 1 },
        { label: "Rotation overlap", value: 0 },
      ],
      whatItTests: [
        "raw contention",
        "immediate middleware reaction",
        "ability to massively reject when too many requests hit the same key",
      ],
      expectedReading: [
        "only one or very few requests succeed",
        "many 429 responses",
        "almost no progression",
      ],
      simpleExplanation:
        "This mode sends all the load at once. It is used to test the immediate resistance of the runtime and the rejection policy when multiple requests use the same context handle at the same time.",
    };
  }

  static maintainedConcurrency(planKey: BurstPlanKey): BurstScenarioDefinition {
    const burstConfig: MaintainedConcurrencyConfig = {
      dispatchMode: "maintained-concurrency",
      planKey,
      total: 500,
      concurrency: 50,
      delayMs: 10,
    };

    return {
      key: "maintained-concurrency",
      title: "Maintained concurrency",
      maxInFlight: "5",
      rotationOverlapMs: "1000",
      burstConfig,
      idea:
        "The client keeps X requests in-flight continuously until reaching the total.",
      recommendedParameters: [
        { label: "Dispatch mode", value: "maintained-concurrency" },
        { label: "Total requests", value: 500 },
        { label: "Concurrency", value: 50 },
        { label: "Delay per request", value: 10 },
        { label: "Max In-Flight", value: 5 },
        { label: "Rotation overlap", value: 1000 },
      ],
      whatItTests: [
        "realistic continuous load",
        "average system behavior over time",
        "stability of p50 / p95 metrics",
        "effect of rotating context under sustained traffic",
      ],
      expectedReading: [
        "relatively stable throughput",
        "usable histogram",
        "some rejections if pressure exceeds limits",
        "smoother context timeline",
      ],
      simpleExplanation:
        "This mode simulates a real client maintaining a certain level of concurrency. It is useful for observing runtime stability, latency, and overall behavior under sustained load.",
    };
  }

  static waveBatches(planKey: BurstPlanKey): BurstScenarioDefinition {
    const burstConfig: WaveBatchesConfig = {
      dispatchMode: "wave-batches",
      planKey,
      total: 100,
      batchSize: 5,
      wavePauseMs: 300,
      delayMs: 0,
    };

    return {
      key: "wave-batches",
      title: "Wave batches",
      maxInFlight: "1",
      rotationOverlapMs: "0",
      burstConfig,
      idea: "Requests are sent in fixed batches.",
      recommendedParameters: [
        { label: "Dispatch mode", value: "wave-batches" },
        { label: "Total requests", value: 100 },
        { label: "Batch size", value: 5 },
        { label: "Wave pause", value: 300 },
        { label: "Delay per request", value: 0 },
        { label: "Max In-Flight", value: 1 },
        { label: "Rotation overlap", value: 0 },
      ],
      whatItTests: [
        "wave-based behavior",
        "batch effects",
        "relationship between batch size and max in-flight",
        "near-deterministic case: 1 success / 4 rejected",
      ],
      expectedReading: [
        "for batch size 5 and max in-flight 1, around 20% succeed",
        "more readable behavior than single burst",
        "rejection patterns become easy to explain",
      ],
      simpleExplanation:
        "This mode sends groups of requests separated by pauses. It is used to test how the runtime reacts to periodic pressure and provides results that are easier to interpret than a full burst.",
    };
  }

  static waveBatchesStaggered(planKey: BurstPlanKey): BurstScenarioDefinition {
    const burstConfig: WaveBatchesStaggeredConfig = {
      dispatchMode: "wave-batches-staggered",
      planKey,
      total: 100,
      batchSize: 5,
      wavePauseMs: 300,
      delayMs: 100,
    };

    return {
      key: "wave-batches-staggered",
      title: "Wave batches (staggered)",
      maxInFlight: "1",
      rotationOverlapMs: "100",
      burstConfig,
      idea:
        "Same as wave batches, but with a delay between requests within the same wave.",
      recommendedParameters: [
        { label: "Dispatch mode", value: "wave-batches-staggered" },
        { label: "Total requests", value: 100 },
        { label: "Batch size", value: 5 },
        { label: "Wave pause", value: 300 },
        { label: "Delay between requests", value: 100 },
        { label: "Max In-Flight", value: 1 },
        { label: "Rotation overlap", value: 100 },
      ],
      whatItTests: [
        "fine interaction between rotation, overlap, and request timing",
        "less deterministic scenarios",
        "realistic client behavior (not perfectly simultaneous)",
      ],
      expectedReading: [
        "more variability",
        "some additional requests may succeed due to overlap",
        "timeline becomes very useful to understand behavior",
      ],
      simpleExplanation:
        "This mode introduces a delay between requests within the same wave. It helps explore how the overlap window and rotation affect results when requests arrive almost simultaneously, but not exactly at the same time.",
    };
  }

  static simpleRequest(planKey: BurstPlanKey): BurstScenarioDefinition {
    const scenario = this.maintainedConcurrency(planKey);

    const burstConfig: MaintainedConcurrencyConfig = {
      dispatchMode: "maintained-concurrency",
      planKey,
      total: 1,
      concurrency: 1,
      delayMs: 10,
    };

    scenario.burstConfig = burstConfig;
    return scenario;
  }
}