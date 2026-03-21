import type { IEntityStore, EntityStoreMode } from "./EntityStoreType";
import type { EntityStoreOptions, ApiEntityStoreOptions } from "./EntityStoreOptions";
import { LocalStorageEntityStore } from "./providers/LocalStorageEntityStore";
import { SimulatedApiEntityStore } from "./providers/SimulatedApiEntityStore";
import { ProxyApiEntityStore } from "./providers/ProxyApiEntityStore";
import { SimpleApiEntityStore } from "./providers/SimpleApiEntityStore";

// Factory for generic entity stores
export class EntityStoreFactory{
  // Factory for generic entity stores
  public static createEntityStore<T, TId = string>(
    options:
      | ({ mode?: EntityStoreMode } & EntityStoreOptions<T, TId>)
      | ({ mode?: EntityStoreMode } & ApiEntityStoreOptions<T, TId>)
  ): IEntityStore<T, TId> {
    switch (options.mode) {
      case "api-proxy":
        return new ProxyApiEntityStore<T, TId>(
          options as ApiEntityStoreOptions<T, TId>
        );

      case "api-simple":
        return new SimpleApiEntityStore<T, TId>(
          options as ApiEntityStoreOptions<T, TId>
        );

      case "simulated-api":
        return new SimulatedApiEntityStore<T, TId>(
          options as EntityStoreOptions<T, TId>
        );

      case "local-storage":
      default:
        return new LocalStorageEntityStore<T, TId>(
          options as EntityStoreOptions<T, TId>
        );
    }
  }
}

