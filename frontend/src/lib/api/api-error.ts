// Tipo único de error de las llamadas a la API: HTTP (ProblemDetails), red o inesperado
export type ApiErrorKind = 'http' | 'network' | 'unknown'

// Elemento de la extensión ValidationErrors de los ProblemDetails del backend (FluentValidation)
export interface ValidationError {
  propertyName: string
  errorMessage: string
}

export interface ApiErrorInit {
  kind: ApiErrorKind
  status: number
  title: string
  detail?: string | null
  validationErrors?: ValidationError[]
  traceId?: string | null
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  // Código HTTP; 0 si no hubo respuesta (network) o es un error inesperado (unknown)
  readonly status: number
  readonly title: string
  readonly detail: string | null
  readonly validationErrors: ValidationError[]
  readonly traceId: string | null

  constructor(init: ApiErrorInit) {
    const detail = init.detail ?? null
    super(detail ?? init.title)
    this.name = 'ApiError'
    this.kind = init.kind
    this.status = init.status
    this.title = init.title
    this.detail = detail
    this.validationErrors = init.validationErrors ?? []
    this.traceId = init.traceId ?? null
  }
}

// Convierte una respuesta no exitosa en ApiError, leyendo sus ProblemDetails si los trae
export async function createHttpError(response: Response): Promise<ApiError> {
  const fallbackTitle = response.statusText || `HTTP ${response.status}`
  const problem = await readProblemDetails(response)

  return new ApiError({
    kind: 'http',
    status: response.status,
    title: asString(problem?.title) ?? fallbackTitle,
    detail: asString(problem?.detail),
    validationErrors: readValidationErrors(problem?.ValidationErrors),
    traceId: asString(problem?.traceId),
  })
}

// Devuelve el mismo ApiError o envuelve cualquier otro valor lanzado
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error
  }
  return new ApiError({
    kind: 'unknown',
    status: 0,
    title: error instanceof Error ? error.message : 'Unexpected error.',
  })
}

async function readProblemDetails(
  response: Response,
): Promise<Record<string, unknown> | null> {
  if (!response.headers.get('Content-Type')?.includes('json')) {
    return null
  }
  try {
    const body: unknown = await response.json()
    return isRecord(body) ? body : null
  } catch {
    // Cuerpo vacío o JSON inválido: el error se crea solo con el status
    return null
  }
}

function readValidationErrors(value: unknown): ValidationError[] {
  if (!Array.isArray(value)) {
    return []
  }
  return value.filter(isRecord).map((item) => ({
    propertyName: asString(item.propertyName) ?? '',
    errorMessage: asString(item.errorMessage) ?? '',
  }))
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function asString(value: unknown): string | null {
  return typeof value === 'string' && value !== '' ? value : null
}
