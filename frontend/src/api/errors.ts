import axios from 'axios'

/**
 * The API reports failures as ProblemDetails. Anything else that reaches an error handler — a
 * dropped connection, a bug of our own — carries no detail worth showing at the till, so the
 * caller's message stands in.
 */
export function errorDetail(error: unknown, fallback: string): string {
  if (axios.isAxiosError<{ detail?: string }>(error)) {
    return error.response?.data?.detail ?? fallback
  }
  return fallback
}
