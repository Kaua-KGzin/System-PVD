import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthResponse } from '../types'

interface AuthState {
  token: string | null
  refreshToken: string | null
  username: string | null
  role: string | null
  isAuthenticated: boolean
  login: (data: AuthResponse) => void
  logout: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      refreshToken: null,
      username: null,
      role: null,
      isAuthenticated: false,
      login: (data) => {
        localStorage.setItem('token', data.token)
        localStorage.setItem('refreshToken', data.refreshToken)
        set({
          token: data.token,
          refreshToken: data.refreshToken,
          username: data.username,
          role: data.role,
          isAuthenticated: true,
        })
      },
      logout: () => {
        localStorage.removeItem('token')
        localStorage.removeItem('refreshToken')
        set({ token: null, refreshToken: null, username: null, role: null, isAuthenticated: false })
      },
    }),
    { name: 'archlab-auth' }
  )
)
