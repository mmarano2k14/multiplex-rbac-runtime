export type Rotation = { from: string; to: string };

export class ClientSession {
  private _contextKey = "";
  private _demoUserId = "demo-user-1";

  public get contextKey(): string {
    return this._contextKey;
  }
  public set contextKey(v: string) {
    this._contextKey = (v ?? "").trim();
  }

  public get demoUserId(): string {
    return this._demoUserId;
  }
  public set demoUserId(v: string) {
    this._demoUserId = (v ?? "").trim();
  }

  public buildHeaders(): Record<string, string> {
    const h: Record<string, string> = {};
    if (this._contextKey) h["X-Access-Context"] = this._contextKey;
    if (this._demoUserId) h["X-Demo-UserId"] = this._demoUserId;
    return h;
  }

  public applyRotation(headers?: Record<string, string | string[]>): Rotation | undefined {
    const rotated = headers?.["x-access-context"];
    const value =
      typeof rotated === "string" ? rotated :
      Array.isArray(rotated) ? rotated[0] :
      "";

    if (value && value !== this._contextKey) {
      const rotation = { from: this._contextKey || "(empty)", to: value };
      this._contextKey = value;
      return rotation;
    }
    return undefined;
  }
}