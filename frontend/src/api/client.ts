import axios from 'axios'
import { useAuthStore } from '../store/auth'

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

let refreshPromise: Promise<string> | null = null

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      const { refreshToken, login, logout } = useAuthStore.getState()

      if (refreshToken) {
        if (!refreshPromise) {
          refreshPromise = axios
            .post('/api/auth/refresh', { refreshToken })
            .then(({ data }) => {
              login(data)
              return data.token as string
            })
            .finally(() => {
              refreshPromise = null
            })
        }

        try {
          const newToken = await refreshPromise
          original.headers.Authorization = `Bearer ${newToken}`
          return api(original)
        } catch {
          logout()
          window.location.href = '/login'
        }
      } else {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)
