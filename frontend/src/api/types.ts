/**
 * The wire types the backend actually serves, transcribed from the C# records behind
 * `/api/v1`. They are hand-written rather than generated: the surface is meta for
 * now, and a generator would put a running backend on the critical path of a frontend build.
 * When that stops being true, generating from the OpenAPI document the API already produces
 * (`Api:EnableOpenApi`) replaces this file without changing anything that imports it.
 *
 * Ids are `string` here even though they are `Guid` in C#, because ADR 0003 says ids are
 * opaque to clients — typing them as anything a client could parse invites code that does.
 */

/**
 * The platform-wide pagination envelope from ADR 0003. Collection endpoints never return a
 * bare array, so every list in this client is a `Page<T>`.
 */
export interface Page<T> {
  readonly items: readonly T[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalItems: number;
  readonly totalPages: number;
  readonly hasNextPage: boolean;
  readonly hasPreviousPage: boolean;
}

/** One row of `GET /api/v1/meta/modules`: which modules this host composed in. */
export interface ModuleInfo {
  readonly name: string;
  readonly routePrefix: string;
}

/**
 * `GET /api/v1/me`: the signed-in caller. The development identity until
 * [#6](https://github.com/shoraLBRT/ritocode/issues/6) brings real sessions; a 401 means signed out.
 */
export interface Me {
  readonly id: string;
  readonly username: string;
}

/** Paging inputs, 1-based. Out-of-range values are rejected by the API, never clamped. */
export interface PageQuery {
  readonly page?: number;
  readonly pageSize?: number;
}
