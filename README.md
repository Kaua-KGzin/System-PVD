<div align="center">

# 🏪 ARCH System — Sistema de Gestão Comercial

**ERP / Ponto de Venda — Backend .NET 10 · React 19 · PostgreSQL**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-10-7B68EE?style=for-the-badge)](https://learn.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?style=for-the-badge&logo=typescript)](https://www.typescriptlang.org/)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=for-the-badge&logo=docker)](https://www.docker.com/)
[![CI](https://img.shields.io/badge/CI-GitHub_Actions-2088FF?style=for-the-badge&logo=githubactions)](https://github.com/features/actions)

</div>

---

## 🚦 Status

| Área | Status |
|------|--------|
| Backend — Domain model (15 entidades) | ✅ |
| JWT Auth + Refresh Tokens + RBAC | ✅ |
| EF Core Migrations (PostgreSQL) | ✅ |
| Race condition `Sale.Number` (lock serializable) | ✅ |
| Multi-pagamento + troco + cancelamento | ✅ |
| Estoque + InventoryMovement log | ✅ |
| Category · Customer · Supplier · PurchaseEntry | ✅ |
| Dashboard · Relatórios · Alertas de estoque | ✅ |
| CashRegister (abertura/fechamento de caixa) | ✅ |
| FiscalDocument (simulação NFC-e) | ✅ |
| Testes xUnit — 65 testes (SQLite in-memory) | ✅ |
| Docker + docker-compose (PostgreSQL 17) | ✅ |
| GitHub Actions CI (build + test + docker build) | ✅ |
| Frontend React 19 — 9 páginas | ✅ |
| PostgreSQL como banco de produção | ✅ |
| Deploy em produção | 🔴 Próximo |

---

## 🧱 Stack

| Camada | Tecnologias |
|--------|-------------|
| **Backend** | C# · ASP.NET Core 10 Minimal APIs · EF Core 10 · BCrypt.Net |
| **Auth** | JWT Bearer · Refresh Tokens · Role-based Authorization |
| **Banco (dev/prod)** | PostgreSQL 17 via Npgsql · SQLite (testes in-memory) |
| **Frontend** | React 19 · TypeScript · Vite · React Query · Zustand · React Router · Axios |
| **Infra** | Docker · docker-compose · GitHub Actions CI |
| **Docs** | OpenAPI (Scalar UI) · Health Checks · Rate Limiting |

---

## 🚀 Rodar localmente

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. PostgreSQL via Docker
```bash
# Dev local (porta 5433 — evita conflito com PostgreSQL nativo no Windows)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up db -d

# Verificar que está pronto:
docker compose exec db pg_isready -U archlab
```

### 2. Backend
```bash
cd Archlab.Backend

# SQLite (mais simples, sem Docker):
dotnet run

# PostgreSQL (com Docker acima):
$env:DatabaseProvider="PostgreSQL"
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=archlab;Username=archlab;Password=archlab_dev_password"
dotnet run
```

API disponível em `http://localhost:5235`  
OpenAPI: `http://localhost:5235/openapi/v1.json`  
Health: `http://localhost:5235/health`

Login padrão (dev): `admin` / `admin123`

### 3. Frontend
```bash
cd frontend
npm install
npm run dev   # http://localhost:5173
```

O Vite faz proxy `/api → http://localhost:5235` automaticamente.

### 4. Testes
```bash
dotnet test Archlab.Backend.Tests
# → 65/65 passando (SQLite in-memory, sem dependências externas)
```

---

## 📡 Endpoints

### Auth
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/auth/login` | Login → JWT + Refresh Token |
| `POST` | `/api/auth/refresh` | Renovar JWT |
| `POST` | `/api/auth/logout` | Revogar Refresh Token |

### Produtos / Categorias
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/products` | Listar paginado (`search`, `categoryId`, `lowStock`) |
| `POST` | `/api/products` | Criar |
| `PUT` | `/api/products/{id}` | Atualizar |
| `GET` | `/api/products/barcode/{barcode}` | Buscar por código |
| `POST` | `/api/products/{id}/stock-adjustments` | Ajuste manual |
| `GET` | `/api/categories` | Listar categorias |
| `POST` | `/api/categories` | Criar categoria |
| `PUT` | `/api/categories/{id}` | Atualizar categoria |

### Clientes / Fornecedores
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/customers` | Listar paginado (`search`) |
| `POST` | `/api/customers` | Criar |
| `PUT` | `/api/customers/{id}` | Atualizar |
| `GET` | `/api/suppliers` | Listar paginado |
| `POST` | `/api/suppliers` | Criar |
| `PUT` | `/api/suppliers/{id}` | Atualizar |
| `POST` | `/api/purchase-entries` | Entrada de estoque |

### Caixa / Vendas
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/cash-sessions` | Abrir caixa |
| `GET` | `/api/cash-sessions/open/{terminalId}` | Caixa aberto |
| `POST` | `/api/cash-sessions/{id}/close` | Fechar caixa |
| `POST` | `/api/sales` | Registrar venda |
| `GET` | `/api/sales` | Histórico paginado |
| `GET` | `/api/sales/{id}` | Detalhe |
| `POST` | `/api/sales/{id}/cancel` | Cancelar (estorna estoque) |

### Relatórios / Dashboard
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/dashboard` | KPIs (vendas, estoque, caixas) |
| `GET` | `/api/reports/sales-summary` | Resumo por período |
| `GET` | `/api/reports/stock-alerts` | Produtos abaixo do mínimo |
| `GET` | `/api/reports/top-products` | Produtos mais vendidos |
| `GET` | `/api/reports/revenue-by-day` | Receita por dia |
| `GET` | `/api/reports/inventory-movements` | Movimentações de estoque |
| `GET` | `/api/reports/cash-session-summary/{id}` | Fechamento de caixa |

### Infra
| Método | Rota |
|--------|------|
| `GET` | `/health` |
| `GET` | `/openapi/v1.json` |

---

## 📁 Estrutura

```
ARCH System/
├── Archlab.Backend/             # ASP.NET Core 10 Minimal APIs
│   ├── Domain/                  # Entidades de domínio (15)
│   ├── Data/                    # DbContext · Migrations · Seeder · Factory
│   ├── Services/                # Regras de negócio
│   ├── Endpoints/               # Rotas Minimal API
│   ├── Contracts/               # DTOs request/response
│   └── Program.cs
├── Archlab.Backend.Tests/       # xUnit + SQLite in-memory (65 testes)
├── frontend/                    # React 19 + TypeScript + Vite
│   └── src/
│       ├── pages/               # 9 páginas (Dashboard, PDV, Catálogo, ...)
│       ├── components/          # Sidebar
│       ├── store/               # Zustand (auth, cart)
│       ├── api/                 # Axios client + interceptors JWT
│       └── types/               # Contratos TypeScript
├── .github/workflows/ci.yml     # Build · Test · Docker build
├── docker-compose.yml           # PostgreSQL 17 (produção)
├── docker-compose.dev.yml       # Override para dev local (porta 5433)
├── .env.example                 # Variáveis de ambiente necessárias
└── ARCHlab.slnx                 # Solution
```

---

## 🔒 Segurança

- JWT com refresh tokens rotativos (revogação por token)
- Senhas com BCrypt (work factor 11)
- Rate limiting por IP
- CORS configurável por ambiente
- Seeder recusa senha padrão `admin123` fora de Development

> **Fiscal:** A emissão NFC-e é uma **simulação técnica**. Para uso real: certificado digital A1/A3, integração com SEFAZ, regras tributárias por estado/regime.

---

<div align="center">

**ARCHlab · ARCH System · Solo project by Kauã ([@Kaua-KGzin](https://github.com/Kaua-KGzin))**

</div>
