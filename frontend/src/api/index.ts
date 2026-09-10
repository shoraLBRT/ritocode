export { ApiClient, REQUEST_ID_HEADER } from './client';
export type { ApiClientOptions, RequestOptions } from './client';
export { ApiError, isApiError, parseProblemBody } from './errors';
export type { ApiErrorKind, ApiProblemBody } from './errors';
export { getProblem, listModules, listProblems } from './endpoints';
export { DEFAULT_API_BASE_URL, resolveApiBaseUrl } from './config';
export { ApiClientContext, useApiClient } from './ApiClientContext';
export { ApiClientProvider } from './ApiClientProvider';
export type {
  CatalogProblem,
  CatalogProblemDetail,
  Difficulty,
  ModuleInfo,
  Page,
  PageQuery,
} from './types';
