"use client";

import { useMemo, useReducer } from "react";
import type { BurstConfig } from "./BurstMachineType";
import { BurstMachine } from "./BurstMachine";
import { BurstController } from "./BurstController";
import { BurstApiAdapter } from "./BurstApiAdapter";
import { ConsoleContextAccessor } from "../ConsoleType";

export function useBurstController(
  consoleContextAccessor: ConsoleContextAccessor,
  defaultConfig?: BurstConfig
) {
  const initialCfg = defaultConfig ?? BurstMachine.defaultConfig();

  const [model, dispatch] = useReducer(
    BurstMachine.reduce.bind(BurstMachine),
    initialCfg,
    (cfg: BurstConfig) => BurstMachine.initial(cfg)
  );

  const adapter = useMemo(
    () => new BurstApiAdapter(consoleContextAccessor.api),
    [consoleContextAccessor.api]
  );

  const controller = useMemo(
    () => new BurstController(dispatch, consoleContextAccessor),
    [dispatch, consoleContextAccessor]
  );

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
      async start(configOverride?: BurstConfig) {
        await controller.start(adapter, model, configOverride);
      },
    };
  }, [controller, adapter, model]);

  return { model, actions };
}