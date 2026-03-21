import type { HttpClient } from "@/lib/infrastructure/transport/http/HttpClient";
import { EntityStoreMode } from "./EntityStoreType";

export type EntityStoreOptions<T, TId = string> = {
  mode?:EntityStoreMode;
  storageKey: string;
  getId: (entity: T) => TId;
  compare?: (left: T, right: T) => number;
};

export type ApiEntityStoreOptions<T, TId = string> = {
  baseUrl: string;
  resourcePath: string;
  getId: (entity: T) => TId;
  compare?: (left: T, right: T) => number;
  client?: HttpClient;
  serialize?: (entity: T) => unknown;
  deserialize?: (payload: unknown) => T;
};