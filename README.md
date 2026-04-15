<div align="center">

# 🏪 PDV System — ERP / Ponto de Venda

**Sistema de gestão de vendas completo, construído sobre .NET 10 Minimal APIs + React + SQLite → PostgreSQL**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-9-purple?style=for-the-badge)](https://learn.microsoft.com/ef/core/)
[![SQLite](https://img.shields.io/badge/SQLite-dev-003B57?style=for-the-badge&logo=sqlite)](https://www.sqlite.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-prod-336791?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?style=for-the-badge&logo=typescript)](https://www.typescriptlang.org/)

</div>

---

## 🗺️ Roadmap do Projeto

> Acesse o **[Roadmap Interativo](./roadmap.html)** — visão completa das 2 sprints, com tarefas por dia, diagnóstico do projeto e checklist de entrega.

---

## 👥 Time

| Membro | GitHub | Função |
|--------|--------|--------|
| **Kauã** | [@Kaua-KGzin](https://github.com/Kaua-KGzin) | Tech Lead · Architect · Backend · Security · QA · DevOps |
| **Kerlon** | — | Frontend Lead · UX/UI Designer |
| **Pedro** | — | Database Engineer · Backend |

---

## 🧱 Stack Técnica

| Camada | Tecnologias |
|--------|-------------|
| **Backend** | C# · ASP.NET Core 10 (Minimal APIs) · EF Core · BCrypt.Net |
| **Banco (dev)** | SQLite via EF Core |
| **Banco (prod)** | PostgreSQL via Npgsql |
| **Frontend** | React 19 · TypeScript · Vite · Axios · React Router |
| **Auth** | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| **API Docs** | OpenAPI (Scalar) |
| **Infra** | Rate Limiting · Health Checks · CORS · ProblemDetails |

---

## 📊 Status Atual

| Módulo | Status |
|--------|--------|
| Domain Model (8 entidades) | ✅ Concluído |
| `ServiceResult<T>` pattern | ✅ Concluído |
| Transações DB + SaleService | ✅ Concluído |
| Estoque + InventoryMovement | ✅ Concluído |
| Multi-pagamento + troco | ✅ Concluído |
| Desconto em item e venda | ✅ Concluído |
| NFC-e simulado | ✅ Concluído |
| CORS · OpenAPI · /health | ✅ Concluído |
| Cancelamento com estorno | ✅ Concluído |
| JWT Auth / User entity | 🔴 Pendente (Sprint 1) |
| EF Migrations (vs EnsureCreated) | 🔴 Pendente (Sprint 1) |
| Category · Customer · Supplier | 🔴 Pendente (Sprint 1) |
| Paginação nas listas | 🔴 Pendente (Sprint 1) |
| Relatórios · Dashboard stats | 🟡 Planejado (Sprint 2) |
| Rate Limiting · Logging | 🟡 Planejado (Sprint 2) |

---

## 🚀 Como Rodar (Backend)

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Execução

```powershell
# Clonar o repositório
git clone https://github.com/Kaua-KGzin/System-PVD.git
cd System-PVD

# Restaurar dependências e rodar
dotnet restore
dotnet run --project .\Pdv.Backend\Pdv.Backend.csproj
```

A API sobe em `https://localhost:5235`. O banco SQLite é criado automaticamente e já é populado com dados seed.

**Docs da API (OpenAPI/Scalar):**
```
http://localhost:5235/openapi/v1.json
```

**Health check:**
```
http://localhost:5235/health
```

---

## 🚀 Como Rodar (Frontend)

> **Nota:** O frontend definitivo (React + TypeScript + Vite) está planejado para a Sprint 1, Dia 1 (Kerlon). O diretório `frontend-test/` contém um protótipo HTML/JS para validação de integração com a API.

```bash
# Quando o frontend React estiver pronto:
cd frontend
npm install
npm run dev
```

---

## 📡 Endpoints Disponíveis

### Produtos
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/products` | Lista produtos (paginado) |
| `POST` | `/api/products` | Criar produto |
| `PUT` | `/api/products/{id}` | Atualizar produto |
| `GET` | `/api/products/barcode/{barcode}` | Buscar por código de barras |
| `POST` | `/api/products/{id}/stock-adjustments` | Ajuste manual de estoque |

### Caixa
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/cash-sessions` | Abrir caixa |
| `GET` | `/api/cash-sessions/open/{terminalId}` | Caixa aberto do terminal |
| `POST` | `/api/cash-sessions/{id}/close` | Fechar caixa |

### Vendas
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/sales` | Registrar venda (itens + pagamentos) |
| `GET` | `/api/sales/{id}` | Consultar venda |
| `POST` | `/api/sales/{id}/cancel` | Cancelar venda (estorna estoque) |

### Documentos fiscais (NFC-e simulado)
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/fiscal-documents/{accessKey}` | Consultar documento |
| `GET` | `/api/fiscal-documents/sale/{saleId}` | Documento por venda |

---

## ⚠️ Observação Fiscal

> A emissão fiscal atual é **apenas uma simulação técnica** para sustentar o fluxo do PDV. Para uso real no Brasil, é necessário integrar SAT, NFC-e, certificado digital, regras tributárias, contingência e autorização da SEFAZ conforme o estado e regime da empresa.

---

## 📁 Estrutura do Projeto

```
System-PVD/
├── Pdv.Backend/
│   ├── Domain/          # Entidades de domínio
│   ├── Data/            # DbContext + Migrations + Seeder
│   ├── Services/        # Regras de negócio
│   ├── Endpoints/       # Minimal API endpoints
│   ├── Contracts/       # DTOs de request/response
│   ├── Common/          # ServiceResult<T>, helpers
│   └── Program.cs       # Configuração da aplicação
├── frontend-test/       # Protótipo HTML/JS (integração)
├── docs/
│   └── roadmaps/        # Planejamento semanal detalhado
├── roadmap.html         # 🗺️ Roadmap interativo unificado
└── README.md
```

---

<div align="center">

**PDV System · Sprint 1–2 · Kauã · Pedro · Kerlon**

</div>
