/**
 * The wire types the backend actually serves, transcribed from the C# records behind
 * `/api/v1`. They are hand-written rather than generated: the surface is the catalog, the
 * workspace and its files, plus meta, and a generator would put a running backend on the critical
 * path of a frontend build.
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

/**
 * Serialised as a camelCase name, never an ordinal — see the API conventions note in
 * `PROJECT_STATE.md`. A number would tie this union to a C# enum's member order.
 */
export type Difficulty = 'easy' | 'medium' | 'hard';

/** One catalog row: a problem plus the published version a workspace is created from. */
export interface CatalogProblem {
  readonly id: string;
  readonly slug: string;
  readonly title: string;
  readonly difficulty: Difficulty;
  readonly tags: readonly string[];
  readonly problemVersionId: string;
  readonly version: number;
  readonly publishedAt: string;
}

/** The catalog row plus the Markdown description. Flat, not nested around `CatalogProblem`. */
export interface CatalogProblemDetail extends CatalogProblem {
  readonly description: string;
}

/** One row of `GET /api/v1/meta/modules`: which modules this host composed in. */
export interface ModuleInfo {
  readonly name: string;
  readonly routePrefix: string;
}

/** Paging inputs, 1-based. Out-of-range values are rejected by the API, never clamped. */
export interface PageQuery {
  readonly page?: number;
  readonly pageSize?: number;
}

/** The caller's working copy of one problem version. There is no owner in it: the owner is the caller. */
export interface Workspace {
  readonly id: string;
  readonly problemVersionId: string;
  readonly createdAt: string;
  readonly updatedAt: string;
}

/** One file of a workspace. `path` is workspace-relative with `/` separators — what a read takes back. */
export interface WorkspaceFileEntry {
  readonly path: string;
  /** Bytes, not characters. */
  readonly sizeBytes: number;
}

/**
 * The whole tree, ordered by path — deliberately not a `Page<T>`: an editor cannot use half a file
 * list, and a package's limits bound it at 2000 entries.
 */
export interface WorkspaceFileTree {
  readonly files: readonly WorkspaceFileEntry[];
}

/** One file as text. A byte-order mark and line endings are preserved, so saving it back changes nothing. */
export interface WorkspaceFile {
  readonly path: string;
  readonly sizeBytes: number;
  readonly content: string;
}
