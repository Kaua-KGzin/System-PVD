import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface SettingsState {
  terminalId: string
  setTerminalId: (id: string) => void
}

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      terminalId: 'CAIXA-01',
      setTerminalId: (id) => set({ terminalId: id.trim() || 'CAIXA-01' }),
    }),
    { name: 'archlab-settings' }
  )
)
