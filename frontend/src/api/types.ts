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

/**
 * `GET /api/v1/tasks/{slug}`: everything a learner needs to solve a task, and nothing that gives the
 * answer away — the cards carry a name, a summary and search keywords only.
 */
export interface TaskDetail {
  readonly slug: string;
  readonly title: string;
  readonly difficulty: Difficulty;
  readonly context: string;
  readonly brief: string;
  readonly material: Material;
  readonly classes: readonly ProblemClass[];
  readonly cards: readonly CandidateCard[];
  readonly sameMaterial: readonly TaskSummary[];
}

export interface Material {
  readonly files: readonly MaterialFile[];
  readonly overview: MaterialOverview;
}

export interface MaterialFile {
  readonly path: string;
  readonly content: string;
}

export interface MaterialOverview {
  readonly files: readonly { readonly path: string; readonly lines: number }[];
  readonly totalLines: number;
  readonly fileCount: number;
  readonly dependencies: readonly string[];
}

/** A card as step 1 offers it: name and summary to read, keywords to search by. */
export interface CandidateCard {
  readonly slug: string;
  readonly class: string;
  readonly name: string;
  readonly summary: string;
  readonly keywords: readonly string[];
}

/** `GET /api/v1/treatments`: the whole tree, shown for every picked card. */
export interface TreatmentTree {
  readonly branches: readonly TreatmentBranch[];
}

export interface TreatmentBranch {
  readonly id: string;
  readonly name: string;
  readonly leaves: readonly TreatmentLeaf[];
}

/** A leaf, addressed as `branch.leaf` — the identifier an answer names. */
export interface TreatmentLeaf {
  readonly id: string;
  readonly label: string;
}

export type AttemptStep = 'diagnosis' | 'treatment';

/** One picked card and its leaves: the answer's unit. */
export interface Pick {
  readonly card: string;
  readonly leaves: readonly string[];
}

/** `/api/v1/attempts/{id}`: an attempt, and once submitted its answer and result. */
export interface Attempt {
  readonly id: string;
  readonly task: string;
  readonly startedAt: string;
  readonly step: AttemptStep;
  readonly submittedAt: string | null;
  readonly practice: boolean;
  readonly contentRevision: string | null;
  readonly answer: { readonly picks: readonly Pick[] } | null;
  readonly result: AttemptResult | null;
}

export interface AttemptResult {
  readonly total: number;
  readonly maximum: number;
  readonly isCorrect: boolean;
  readonly cards: readonly {
    readonly card: string;
    readonly outcome: 'found' | 'missed' | 'extra';
    readonly points: number;
    readonly keyLeaves: readonly string[] | null;
  }[];
}

/** One row of `GET /api/v1/attempts`. */
export interface AttemptSummary {
  readonly id: string;
  readonly task: string;
  readonly startedAt: string;
  readonly step: AttemptStep;
  readonly submittedAt: string | null;
  readonly practice: boolean;
  readonly score: number | null;
  readonly maxScore: number | null;
}

/** Paging inputs, 1-based. Out-of-range values are rejected by the API, never clamped. */
export interface PageQuery {
  readonly page?: number;
  readonly pageSize?: number;
}
