import axios, { AxiosInstance } from "axios";
import { ProxyError, ProxyResponse, RequestSpec } from "./HttpClientType";


export type ProxyRequest = {
  baseUrl: string;
  path: string;
  method: string;
  headers?: Record<string, string>;
  body?: unknown;
};

export class HttpClient {
  private readonly ax: AxiosInstance;

  public constructor() {
    this.ax = axios.create({
      baseURL: "", // same origin (Next.js)
      timeout: 30_000,
      withCredentials: true,
      headers: { "content-type": "application/json" },
    });
  }

  public buildProxyRequest(baseUrl: string, spec: RequestSpec, headers: Record<string, string>): ProxyRequest {
    return {
      baseUrl,
      path: spec.path,
      method: spec.method,
      headers,
      body: spec.body,
    };
  }

  public async sendViaProxy(req: ProxyRequest, signal?: AbortSignal): Promise<ProxyResponse | ProxyError> {
    try {
      const r = await this.ax.post<ProxyResponse | ProxyError>("/api/proxy", req, { signal });
      return r.data;
    } catch (e: unknown) {
      const msg = axios.isAxiosError(e)
        ? e.message
        : e instanceof Error
          ? e.message
          : String(e);

      const err: ProxyError = {
        error: signal?.aborted ? "Aborted" : "Network failure",
        details: msg,
      };

      return err;
    }
  }
}