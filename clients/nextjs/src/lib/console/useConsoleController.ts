"use client";

import { useEffect, useMemo, useReducer, useRef, useState } from "react";

import { ConsoleMachine } from "./ConsoleMachine";
import { InMemoryLogSink } from "@/lib/logs/InMemoryLogSink";
import type { LogEntry } from "@/lib/logs/contracts";
import { MultiplexedRbacApi } from "@/lib/rbac/MultiplexedRbacApi";
import type { TargetPreset } from "../http/HttpClientType";
import type { ConsoleController } from "./ConsoleController";

type RunResult = {
  rotatedContextKey?: string;
  status?: number;
  error?: string;
};

export function useConsoleController(presets: TargetPreset[]): ConsoleController {
  const initialState = ConsoleMachine.initial(presets);

  const [state, dispatch] = useReducer(ConsoleMachine.reduce, initialState);

  const [logSink] = useState(
    () =>
      new InMemoryLogSink((items: LogEntry[]) =>
        dispatch({ type: "LogsChanged", logs: items })
      )
  );

  const [initialApi] = useState(() =>
    MultiplexedRbacApi.createInstanceRef(initialState.baseUrl, logSink)
  );

  const apiRef = useRef<MultiplexedRbacApi>(initialApi);
  const stateRef = useRef(state);
  const listenersRef = useRef(new Set<() => void>());

  useEffect(() => {
    stateRef.current = state;
    for (const listener of listenersRef.current) {
        listener();
    }
  }, [state]);

  useEffect(() => {
    apiRef.current.baseUrl = state.baseUrl;
    apiRef.current.demoUserId = state.demoUserId;
    apiRef.current.contextKey = state.contextKey;
  }, [state.baseUrl, state.demoUserId, state.contextKey]);

  const actions = useMemo(() => {
    async function run(call: () => Promise<RunResult>) {
      dispatch({ type: "StartCall" });

      try {
        const r = await call();

        if (typeof r.status === "number" && (r.status === 401 || r.status === 403)) {
          dispatch({ type: "CallForbiddenOrUnauthorized", httpStatus: r.status });
          return;
        }

        if (r.error) {
          dispatch({ type: "CallFailed", message: r.error });
          return;
        }

        dispatch({ type: "CallSucceeded", rotatedContextKey: r.rotatedContextKey });
      } catch (e: unknown) {
        const msg = e instanceof Error ? e.message : String(e);
        dispatch({ type: "CallFailed", message: msg });
      }
    }

    return {
      async bootstrap() {
        dispatch({ type: "StartCall" });

        try {
          const res = await apiRef.current.bootstrap();

          if (res.kind === "error") {
            dispatch({ type: "CallFailed", message: res.error.error });
            return;
          }

          dispatch({ type: "CallSucceeded", rotatedContextKey: apiRef.current.contextKey || undefined });
          dispatch({ type: "BootStrapSucceeded", demoUserId: apiRef.current.demoUserId, contextKey: apiRef.current.contextKey });
        } catch (e: unknown) {
          const msg = e instanceof Error ? e.message : String(e);
          dispatch({ type: "CallFailed", message: msg });
        }
      },

      async login(username: string) {
        dispatch({ type: "StartCall" });

        try {
          const res = await apiRef.current.login(username);

          if (res.kind === "error") {
            dispatch({ type: "CallFailed", message: res.error.error });
            return;
          }

          dispatch({ type: "CallSucceeded", rotatedContextKey: apiRef.current.contextKey || undefined });
          dispatch({ type: "LoginSucceeded", demoUserId: username, contextKey: apiRef.current.contextKey });
          return true;
        } catch (e: unknown) {
          const msg = e instanceof Error ? e.message : String(e);
          dispatch({ type: "CallFailed", message: msg });
          return false;
        }
      },

      async getContextKey() {
        await run(async () => {
          const res = await apiRef.current.getContextKey();

          return {
            status: res.kind === "ok" ? res.response.status : undefined,
            rotatedContextKey: apiRef.current.contextKey || undefined,
            error: res.kind === "error" ? res.error.error : undefined,
          };
        });
      },

      async readInvoice() {
        const current = stateRef.current;

        await run(async () => {
          const res = await apiRef.current.readInvoice(current.invoiceId);

          return {
            status: res.kind === "ok" ? res.response.status : undefined,
            rotatedContextKey: apiRef.current.contextKey || undefined,
            error: res.kind === "error" ? res.error.error : undefined,
          };
        });
      },

      async refundInvoice() {
        const current = stateRef.current;

        await run(async () => {
          const res = await apiRef.current.refundInvoice(current.invoiceId, current.amount);

          return {
            status: res.kind === "ok" ? res.response.status : undefined,
            rotatedContextKey: apiRef.current.contextKey || undefined,
            error: res.kind === "error" ? res.error.error : undefined,
          };
        });
      },

      clearLogs() {
        logSink.clear();
        dispatch({ type: "ClearLogs" });
      },

      resetError() {
        dispatch({ type: "ResetError" });
      },
    };
  }, [logSink]);

  const actionsRef = useRef(actions);

  useEffect(() => {
    actionsRef.current = actions;
  }, [actions]);

  const controllerRef = useRef<ConsoleController | null>(null);

  if (!controllerRef.current) {
    controllerRef.current = {
      get state() {
        return stateRef.current;
      },
      dispatch,
      get actions() {
        return actionsRef.current;
      },
      api: initialApi,
      subscribe(listener: () => void) {
        listenersRef.current.add(listener);
        return () => {
          listenersRef.current.delete(listener);
        };
      },
    };
  }

  return controllerRef.current;
}