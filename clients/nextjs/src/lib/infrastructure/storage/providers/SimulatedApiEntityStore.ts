import { EntityStoreBase } from "../EntityStoreBase";
import { LocalStorageEntityStore } from "./LocalStorageEntityStore";
import type { EntityFindQuery, EntityQuery } from "../EntityStoreType";
import type { EntityStoreOptions } from "../EntityStoreOptions";

// Simulated API store backed by localStorage
export class SimulatedApiEntityStore<T, TId = string>
  extends EntityStoreBase<T, TId>
{
  private readonly store: LocalStorageEntityStore<T, TId>;

  constructor(options: EntityStoreOptions<T, TId>) {
    super(options);
    this.store = new LocalStorageEntityStore<T, TId>(options);
  }

  public async getAll(query?: EntityQuery): Promise<T[]> {
    return this.simulate(() => this.store.getAll(query));
  }

  public async getById(id: TId): Promise<T | null> {
    return this.simulate(() => this.store.getById(id));
  }

  public async save(entity: T): Promise<void> {
    return this.simulate(() => this.store.save(entity));
  }

  public async delete(id: TId): Promise<void> {
    return this.simulate(() => this.store.delete(id));
  }

  public async clear(): Promise<void> {
    return this.simulate(() => this.store.clear());
  }

  public async find(query: EntityFindQuery): Promise<T[]> {
    return this.simulate(() => this.store.find(query));
  }

  // Add small random latency to mimic an HTTP call
  private async simulate<TResult>(fn: () => Promise<TResult>): Promise<TResult> {
    await new Promise((resolve) => setTimeout(resolve, this.getRandomDelayMs()));
    return fn();
  }

  // Biased distribution (more values concentrated around lower/mid range ~150–300ms)
  private getRandomDelayMs(): number {
    const min = 80;
    const max = 600;

    // Squaring the random value biases it toward smaller numbers
    const random = Math.random() * Math.random();

    return Math.floor(random * (max - min + 1)) + min;
  }
}