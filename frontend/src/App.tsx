import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useAuthStore } from './store/auth'
import { useSettingsStore } from './store/settings'
import Sidebar from './components/Sidebar'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import PdvPage from './pages/PdvPage'
import CatalogPage from './pages/CatalogPage'
import SalesHistoryPage from './pages/SalesHistoryPage'
import CashPage from './pages/CashPage'
import SuppliersPage from './pages/SuppliersPage'
import ReportsPage from './pages/ReportsPage'
import CustomersPage from './pages/CustomersPage'
import CategoriesPage from './pages/CategoriesPage'
import UsersPage from './pages/UsersPage'
import PurchaseEntriesPage from './pages/PurchaseEntriesPage'
import AuditPage from './pages/AuditPage'
import LoyaltyPage from './pages/LoyaltyPage'
import CommissionPage from './pages/CommissionPage'

const qc = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: 1 } } })

function ThemeEffect() {
  const darkMode = useSettingsStore((s) => s.darkMode)
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', darkMode ? 'dark' : 'light')
  }, [darkMode])
  return null
}

function ProtectedLayout() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return (
    <div className="app-layout">
      <Sidebar />
      <main className="app-main">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/pdv" element={<PdvPage />} />
          <Route path="/catalog" element={<CatalogPage />} />
          <Route path="/sales" element={<SalesHistoryPage />} />
          <Route path="/cash" element={<CashPage />} />
          <Route path="/suppliers" element={<SuppliersPage />} />
          <Route path="/customers" element={<CustomersPage />} />
          <Route path="/categories" element={<CategoriesPage />} />
          <Route path="/users" element={<UsersPage />} />
          <Route path="/purchase-entries" element={<PurchaseEntriesPage />} />
          <Route path="/audit" element={<AuditPage />} />
          <Route path="/loyalty" element={<LoyaltyPage />} />
          <Route path="/commissions" element={<CommissionPage />} />
          <Route path="/reports" element={<ReportsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <ThemeEffect />
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/*" element={<ProtectedLayout />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
