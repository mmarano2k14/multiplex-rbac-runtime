import type { EntityStoreMode, IEntityStore } from "@/lib/infrastructure/storage/EntityStoreType";
import type { BurstRun } from "../BurstRun";
import { ApiEntityStoreOptions, EntityStoreOptions } from "@/lib/infrastructure/storage/EntityStoreOptions";

export type BurstRunStoreOptions = {
  mode?: EntityStoreMode;
} & Partial<
  EntityStoreOptions<BurstRun, string> &
  ApiEntityStoreOptions<BurstRun, string>
>;
// Extend generic store with domain-specific queries
export interface IBurstRunStore extends IEntityStore<BurstRun, string> {
  getLatest(): Promise<BurstRun | null>;
  getByParentRunId(parentId: string): Promise<BurstRun[]>;
}

