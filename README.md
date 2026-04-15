<div align="center">

# 🏪 PDV System — ERP / Ponto de Venda

**Sistema de gestão de vendas completo — Backend .NET 10 · React 19 · PostgreSQL**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-10-7B68EE?style=for-the-badge)](https://learn.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?style=for-the-badge&logo=typescript)](https://www.typescriptlang.org/)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=for-the-badge&logo=docker)](https://www.docker.com/)

</div>

---

## 🗺️ Roadmap

> **[→ Roadmap Interativo (8 semanas)](./roadmap.html)** — Análise técnica completa, tarefas por semana/responsável, matriz de riscos, checklist de entrega e script do demo. Abrir no browser.

**[→ Roadmaps legados (Sprint 1 e 2)](./docs/roadmaps/)** — versões anteriores de referência.

---

## 🚦 Status Atual

| Área | Status | Observação |
|------|--------|-----------|
| Domain Model (8 entidades) | ✅ Pronto | Sólido, bem modelado |
| ServiceResult&lt;T&gt; pattern | ✅ Pronto | Retorno tipado e explícito |
| SaleService + transações DB | ✅ Pronto | BeginTransactionAsync correto |
| Estoque + InventoryMovement | ✅ Pronto | Log de movimentações funcionando |
| Multi-pagamento + troco | ✅ Pronto | Validação de método de troco OK |
| CORS · OpenAPI · /health | ✅ Pronto | Configurados corretamente |
| Cancelamento com estorno | ✅ Pronto | Estoque estornado na transação |
| **JWT Auth / User entity** | 🔴 Pendente | **Sem autenticação — CRÍTICO** |
| **EF Migrations** | 🔴 Pendente | **Usando EnsureCreated — PERIGO** |
| **Race condition Sale.Number** | 🔴 Pendente | **MAX+1 sem lock — precisa fix** |
| **Paginação nas listas** | 🔴 Pendente | ListSales retorna tudo sem limite |
| Category · Customer · Supplier | 🔴 Pendente | Semana 2 |
| Relatórios · Dashboard | 🟡 Planejado | Semana 3 |
| Docker · CI/CD | 🟡 Planejado | Semana 5 |
| PostgreSQL (prod) | 🟡 Planejado | Semana 4 |
| Testes (xUnit + E2E) | 🟡 Planejado | Semanas 4–8 |

---

## 👥 Time

| Membro | GitHub | Papéis |
|--------|--------|--------|
| **Kauã** | [@Kaua-KGzin](https://github.com/Kaua-KGzin) | Tech Lead · Architect · Backend · Security Eng · QA · DevOps |
| **Kerlon** | — | Frontend Lead · UX/UI Designer · React · TypeScript |
| **Pedro** | — | Database Architect · EF Core · Migrations · Query Performance |

---

## 🧱 Stack Técnica

| Camada | Tecnologias |
|--------|-------------|
| **Backend** | C# 14 · ASP.NET Core 10 Minimal APIs · EF Core 10 · BCrypt.Net |
| **Auth** | JWT Bearer · Refresh Tokens · Role-based Authorization |
| **Banco (dev)** | SQLite 3 via EF Core |
| **Banco (prod)** | PostgreSQL 17 via Npgsql |
| **Frontend** | React 19 · TypeScript 5 · Vite · Axios · React Query · Zustand |
| **Infra** | Docker · docker-compose · GitHub Actions CI |
| **API Docs** | OpenAPI (Scalar UI) |
| **Observabilidade** | Serilog · Health Checks · Rate Limiting |

---

## 🚀 Como Rodar — Backend

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

```powershell
# Clonar o repositório
git clone https://github.com/Kaua-KGzin/System-PVD.git
cd System-PVD

# Restaurar e rodar
dotnet restore
dotnet run --project .\Pdv.Backend\Pdv.Backend.csproj

# A API sobe em https://localhost:5235
# OpenAPI: https://localhost:5235/openapi/v1.json
# Health:  https://localhost:5235/health
```

O banco SQLite é criado automaticamente em `Pdv.Backend/pdv.db` com 4 produtos seed.

---

## 🚀 Como Rodar — Frontend (Em desenvolvimento)

> O frontend definitivo (React 19 + TypeScript + Vite) está planejado para a **Semana 1 do roadmap**. O diretório `frontend-test/` contém um protótipo HTML/JS de integração com a API.

```bash
# Quando o frontend React estiver pronto:
cd frontend
npm install
npm run dev   # http://localhost:5173
```

---

## 📡 API Endpoints

### 🔐 Auth *(em desenvolvimento — Semana 1)*
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/auth/login` | Login → JWT + Refresh Token |
| `POST` | `/api/auth/refresh` | Renovar JWT |

### 📦 Produtos
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/products?page=1&pageSize=20&search=` | Listar (paginado) |
| `POST` | `/api/products` | Criar produto |
| `PUT` | `/api/products/{id}` | Atualizar produto |
| `GET` | `/api/products/barcode/{barcode}` | Buscar por barcode |
| `POST` | `/api/products/{id}/stock-adjustments` | Ajuste manual de estoque |

### 🏧 Caixa (Cash Sessions)
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/cash-sessions` | Abrir caixa |
| `GET` | `/api/cash-sessions/open/{terminalId}` | Caixa aberto do terminal |
| `POST` | `/api/cash-sessions/{id}/close` | Fechar caixa |

### 🛒 Vendas
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/sales` | Registrar venda (itens + pagamentos) |
| `GET` | `/api/sales/{id}` | Consultar venda |
| `GET` | `/api/sales?page=1&from=&to=` | Histórico paginado |
| `POST` | `/api/sales/{id}/cancel` | Cancelar (estorna estoque) |

### 📊 Relatórios *(Semana 3)*
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/dashboard` | KPIs em 1 chamada |
| `GET` | `/api/reports/sales-summary` | Resumo de vendas por período |
| `GET` | `/api/reports/stock-alerts` | Produtos com estoque abaixo do mínimo |
| `GET` | `/api/reports/cash-session-summary/{id}` | Comprovante de fechamento |
| `GET` | `/api/reports/inventory-movements` | Histórico de movimentações |

### 🩺 Infra
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/health` | HealthCheck (DB status) |
| `GET` | `/openapi/v1.json` | Spec OpenAPI |

---

## 📁 Estrutura do Projeto

```
System-PVD/
├── Pdv.Backend/
│   ├── Domain/          # Entidades: Product, Sale, CashSession, etc.
│   ├── Data/            # DbContext + Migrations + DatabaseSeeder
│   ├── Services/        # Regras de negócio (SaleService, etc.)
│   ├── Endpoints/       # Minimal API routes
│   ├── Contracts/       # DTOs de request/response
│   ├── Common/          # ServiceResult<T>, extensions
│   └── Program.cs       # Composition root
├── frontend/            # React 19 + TypeScript (Semana 1+)
├── frontend-test/       # Protótipo HTML/JS de integração
├── docs/
│   └── roadmaps/        # Roadmaps legados (semana1.html, semana2.html)
├── roadmap.html         # 🗺️ Roadmap interativo 8 semanas
├── .gitignore           # .NET + Node + secrets
└── README.md
```

---

## 🔒 Avisos de Segurança

> **Estado atual:** A API não possui autenticação. Qualquer pessoa com acesso à rede pode usar todos os endpoints. **Não expor em produção antes de implementar JWT (Semana 1).**

> **Fiscal:** A emissão NFC-e é uma **simulação técnica** apenas. Para uso real no Brasil: integração com SAT/NFC-e, certificado digital A1/A3, regras tributárias, contingência e autorização da SEFAZ conforme estado e regime da empresa.

---

<div align="center">

**PDV System · Roadmap 8 semanas · Privado**<br>
Kauã (Tech Lead) · Pedro (Database) · Kerlon (Frontend/UX)

</div>
