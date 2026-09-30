export { ApiClient, REQUEST_ID_HEADER } from './client';
export type { ApiClientOptions, RequestOptions } from './client';
export { ApiError, isApiError, parseProblemBody } from './errors';
export type { ApiErrorKind, ApiProblemBody } from './errors';
export {
  getAttempt,
  getMe,
  getProblemCatalogue,
  getTask,
  getTreatments,
  listAttempts,
  listModules,
  listTasks,
  recordStep,
  startAttempt,
  submitAttempt,
} from './endpoints';
export { DEFAULT_API_BASE_URL, resolveApiBaseUrl } from './config';
export { ApiClientContext, useApiClient } from './ApiClientContext';
export { ApiClientProvider } from './ApiClientProvider';
export type {
  Attempt,
  AttemptResult,
  AttemptStep,
  AttemptSummary,
  CandidateCard,
  CardSections,
  Difficulty,
  Me,
  ModuleInfo,
  Page,
  PageQuery,
  Pick,
  Material,
  MaterialFile,
  MaterialOverview,
  ProblemCard,
  ProblemCatalogue,
  ProblemClass,
  TaskDetail,
  TaskSummary,
  TreatmentBranch,
  TreatmentLeaf,
  TreatmentTree,
} from './types';
