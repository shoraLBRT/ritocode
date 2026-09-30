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

export type Difficulty = 'easy' | 'medium' | 'hard';

/** One row of `GET /api/v1/tasks`. `solved` is set for a signed-in caller and `null` otherwise. */
export interface TaskSummary {
  readonly slug: string;
  readonly title: string;
  readonly difficulty: Difficulty;
  readonly solved: boolean | null;
}

/** `GET /api/v1/problems`: the six classes, and every live card in full. */
export interface ProblemCatalogue {
  readonly classes: readonly ProblemClass[];
  readonly cards: readonly ProblemCard[];
}

export interface ProblemClass {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
}

export interface ProblemCard {
  readonly slug: string;
  readonly class: string;
  readonly name: string;
  readonly summary: string;
  /** Never shown; what search matches besides the name and summary. */
  readonly keywords: readonly string[];
  readonly sections: CardSections;
}

/** A card's long fields, each Markdown, in the order the page shows them. Optional ones may be null. */
export interface CardSections {
  readonly signs: string | null;
  readonly whyAiDoesIt: string | null;
  readonly cost: string | null;
  readonly acceptableWhen: string | null;
  readonly detection: string | null;
  readonly treatment: string | null;
  readonly sources: string | null;
  readonly counterArguments: string | null;
}

/** Paging inputs, 1-based. Out-of-range values are rejected by the API, never clamped. */
export interface PageQuery {
  readonly page?: number;
  readonly pageSize?: number;
}
