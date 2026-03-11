export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type TargetPreset = {
  label: string;
  baseUrl: string;
};

export type RequestSpec = {
  name: string;
  method: HttpMethod;
  path: string;
  body?: unknown;
  headers?: Record<string, string> | undefined;
};

export type ProxyResponse = {
  ok: boolean;
  status: number;
  statusText: string;
  url: string;
  headers: Record<string, string>;
  body: string;
};

export type ProxyError = {
  error: string;
  details?: string;
};

export type CallResult =
  | { kind: "ok"; response: ProxyResponse; rotation?: { from: string; to: string } }
  | { kind: "error"; error: ProxyError; response?: ProxyResponse };
