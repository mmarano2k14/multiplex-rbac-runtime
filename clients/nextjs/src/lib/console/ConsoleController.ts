import { MultiplexedRbacApi } from "../rbac/MultiplexedRbacApi";
import { ConsoleState, ConsoleEvent } from "./ConsoleType";

export type ConsoleController = {
  state: ConsoleState;
  dispatch: (e: ConsoleEvent) => void;
  actions: {
    getContextKey(): Promise<void>;
    readInvoice(): Promise<void>;
    refundInvoice(): Promise<void>;
    login(username: string): Promise<boolean | undefined>;
    bootstrap(): Promise<void>;
    clearLogs(): void;
    resetError(): void;
  };
  api: MultiplexedRbacApi;
  readonly subscribe: (listener: () => void) => () => void;
};