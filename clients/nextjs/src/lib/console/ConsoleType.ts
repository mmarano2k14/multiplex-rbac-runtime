import { LogEntry } from "../logs/contracts";


export type ConsoleStatus = "Idle" | "Running" | "Expired" | "Error";

export type ConsoleState = {
  status: ConsoleStatus;

  // inputs
  baseUrl: string;
  demoUserId: string;
  contextKey: string;

  invoiceId: string;
  amount: number;

  // runtime
  logs: LogEntry[];
  lastError?: string;

  // derived flags
  busy: boolean;
};

export type ConsoleSession = {
  baseUrl?: string;
  demoUserId?: string;
  contextKey?: string;
};

export type ConsoleEvent =
  | { type: "TargetChanged"; baseUrl: string }
  | { type: "DemoUserChanged"; demoUserId: string }
  | { type: "ContextChanged"; contextKey: string }
  | { type: "InvoiceChanged"; invoiceId: string }
  | { type: "AmountChanged"; amount: number }
  | { type: "LogsChanged"; logs: LogEntry[] }
  | { type: "ClearLogs" }
  | { type: "StartCall" }
  | { type: "CallSucceeded"; rotatedContextKey?: string }
  | { type: "CallForbiddenOrUnauthorized"; httpStatus: number }
  | { type: "CallFailed"; message: string }
  | { type: "ResetError" }
  | { type: "LoginSucceeded"; demoUserId: string; contextKey: string }
  | { type: "BootStrapSucceeded"; demoUserId: string; contextKey: string };


  
