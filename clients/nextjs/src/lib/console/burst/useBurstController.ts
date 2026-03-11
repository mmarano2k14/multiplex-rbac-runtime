"use client";

import { useMemo, useReducer } from "react";

import type { MultiplexedRbacApi } from "@/lib/rbac/MultiplexedRbacApi";
import type { BurstConfig } from "./BurstMachineType";
import { BurstMachine } from "./BurstMachine";
import { BurstController } from "./BurstController";
import { BurstApiAdapter } from "./BurstApiAdapter";

export function useBurstController(api: MultiplexedRbacApi, defaultConfig?: BurstConfig) {
  const initialCfg = defaultConfig ?? BurstMachine.defaultConfig();

  const [model, dispatch] = useReducer(
    BurstMachine.reduce.bind(BurstMachine),
    initialCfg,
    (cfg: BurstConfig) => BurstMachine.initial(cfg)
  );

  const adapter = useMemo(() => new BurstApiAdapter(api), [api]);

  const controller = useMemo(() => new BurstController(dispatch), [dispatch]);

  const actions = useMemo(() => {
    return {
      configure(cfg: BurstConfig) {
        controller.configure(cfg);
      },
      reset() {
        controller.reset();
      },
      stop() {
        controller.stop();
      },
      async start() {
        await controller.start(adapter, model);
      },
    };
  }, [controller, adapter, model]);

  return { model, actions };
}