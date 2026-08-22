import { ApiException } from '../api-client/api-client.generated';

/**
 * Typed error surfaced by feature `data/` services to their callers (components). It is always
 * the result of translating whatever the NSwag-generated client threw — network failures
 * included — never swallowed.
 *
 * Shared across features (originally introduced by `auth.service.ts`, now also used by
 * `mural.service.ts`): genuinely reusable per AGENTS.md ("lo genuinamente reutilizable entre
 * features" belongs in `core/`/`shared/`), not feature-specific logic.
 */
export interface ApiError {
  /** HTTP status code, or `0` for a request that never reached the server (network/CORS failure). */
  status: number;
  /** Message ready to show to the user as-is — already the backend's own generic message when one
   * exists (FR-02/FR-05: same text regardless of which field caused the failure). */
  message: string;
}

const GENERIC_NETWORK_ERROR_MESSAGE = 'No se pudo conectar con el servidor. Intentá nuevamente.';
const GENERIC_UNEXPECTED_ERROR_MESSAGE = 'Ocurrió un error inesperado. Intentá nuevamente.';

/** Translates whatever a feature service caught (`ApiException`, an already-`ApiError`, or an
 * arbitrary network failure) into a typed `ApiError`. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiException) {
    return { status: error.status, message: extractMessage(error) };
  }
  if (isApiError(error)) {
    return error;
  }
  return { status: 0, message: GENERIC_NETWORK_ERROR_MESSAGE };
}

export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    'message' in error &&
    typeof (error as ApiError).status === 'number' &&
    typeof (error as ApiError).message === 'string'
  );
}

function extractMessage(error: ApiException): string {
  try {
    const body = JSON.parse(error.response) as { title?: string };
    if (body?.title) {
      return body.title;
    }
  } catch {
    // error.response was not parseable JSON — fall through to the generic message below.
  }
  return GENERIC_UNEXPECTED_ERROR_MESSAGE;
}
