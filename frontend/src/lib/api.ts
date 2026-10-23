import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import type { ProblemDetails } from '../types/api'

const TOKEN_KEY = 'beautyagenda.token'

const defaultMessages: Record<number, string> = {
  0: 'Não foi possível conectar ao servidor. Verifique sua conexão.',
  400: 'Alguns dados estão inválidos. Revise o formulário.',
  401: 'Sessão expirada ou credenciais inválidas.',
  403: 'Você não tem permissão para esta ação.',
  404: 'Registro não encontrado.',
  409: 'A operação conflita com o estado atual. Atualize e tente novamente.',
  429: 'Muitas tentativas em pouco tempo. Aguarde um minuto.',
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? defaultMessages[status] ?? 'Erro inesperado. Tente novamente.')
    this.status = status
    this.problem = problem
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {}
  }
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const first = Object.values(error.fieldErrors)[0]?.[0]
    return error.status === 400 && first ? first : error.message
  }
  return 'Erro inesperado. Tente novamente.'
}

/** Copies server validation errors ("CustomerPhone" -> "customerPhone") into react-hook-form. Returns true if any matched. */
export function applyServerErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>, fields: string[]): boolean {
  if (!(error instanceof ApiError) || error.status !== 400) return false
  let matched = false
  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const field = fields.find((f) => f.toLowerCase() === key.toLowerCase())
    if (field && messages[0]) {
      setError(field as Path<T>, { type: 'server', message: messages[0] })
      matched = true
    }
  }
  return matched
}

function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

let token = readToken()
let unauthorizedHandler: (() => void) | null = null

export function getToken() {
  return token
}

export function setToken(value: string | null) {
  token = value
  try {
    if (value) localStorage.setItem(TOKEN_KEY, value)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Storage unavailable (private mode): the token lives only in memory.
  }
}

export function onUnauthorized(handler: (() => void) | null) {
  unauthorizedHandler = handler
}

type QueryValue = string | number | boolean | null | undefined | Array<string | number>

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Record<string, QueryValue>
}

export async function apiRequest<T>(path: string, { method = 'GET', body, query }: RequestOptions = {}): Promise<T> {
  const url = new URL(path, window.location.origin)
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue
    if (Array.isArray(value)) value.forEach((v) => url.searchParams.append(key, String(v)))
    else url.searchParams.set(key, String(value))
  }

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  } catch {
    throw new ApiError(0)
  }

  if (response.status === 401 && token && path !== '/api/auth/login') unauthorizedHandler?.()

  if (!response.ok) {
    let problem: ProblemDetails | undefined
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem = undefined
    }
    throw new ApiError(response.status, problem)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, query?: RequestOptions['query']) => apiRequest<T>(path, { query }),
  post: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'POST', body: body ?? {} }),
  put: <T>(path: string, body: unknown) => apiRequest<T>(path, { method: 'PUT', body }),
  patch: <T>(path: string, body: unknown) => apiRequest<T>(path, { method: 'PATCH', body }),
  delete: (path: string) => apiRequest<void>(path, { method: 'DELETE' }),
}
