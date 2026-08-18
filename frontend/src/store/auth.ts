import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { AuthResponse } from '../types'

// JWT token lives only in memory (never persisted) to prevent XSS token theft.
// Only non-sensitive session metadata is persisted to survive page refresh.
interface AuthState {
  token: string | null           // memory only — intentionally excluded from persist
  refreshToken: string | null   // stored in sessionStorage for page-refresh resilience
  username: string | null
  role: string | null
  isAuthenticated: boolean
  login: (data: AuthResponse) => void
  logout: () => void
  setToken: (token: string) => void
}

// Persisted shape: everything except the in-memory token and the actions
type PersistedAuth = Pick<AuthState, 'refreshToken' | 'username' | 'role' | 'isAuthenticated'>

export const useAuthStore = create<AuthState>()(
  persist<AuthState, [], [], PersistedAuth>(
    (set) => ({
      token: null,
      refreshToken: null,
      username: null,
      role: null,
      isAuthenticated: false,
      login: (data) => {
        set({
          token: data.token,
          refreshToken: data.refreshToken,
          username: data.username,
          role: data.role,
          isAuthenticated: true,
        })
      },
      logout: () => {
        set({ token: null, refreshToken: null, username: null, role: null, isAuthenticated: false })
      },
      setToken: (token) => set({ token }),
    }),
    {
      name: 'archlab-auth',
      storage: createJSONStorage(() => sessionStorage),
      // Exclude JWT access token from persistence — keep only session metadata
      partialize: (state) => ({
        refreshToken: state.refreshToken,
        username: state.username,
        role: state.role,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
)
