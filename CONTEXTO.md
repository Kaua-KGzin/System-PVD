# Contexto do Sistema

## PostgreSQL 18

- **Host:** localhost
- **Port:** 5432
- **Banco:** ArchNexus
- **Usuário:** `kgzin`
- **Senha:** `KGzin123`
- **Serviço:** postgresql-x64-18

## Usuários do Banco

| Usuário | Senha | Permissão |
|---------|-------|-----------|
| postgres | KGzin123 | Superuser |
| kgzin | KGzin123 | Superuser |

## Usuários da Aplicação (Seed)

| Usuário | Senha | Role |
|---------|-------|------|
| KGzin | KGzin123 | Admin |
| admin | admin123 | Admin |

## Arquivos de Configuração

- `appsettings.Development.json` — connection string PostgreSQL
- `Archlab.Desktop/Program.cs` — Desktop usa PostgreSQL (não SQLite)

## pg_hba.conf

- Localização: `C:\Program Files\PostgreSQL\18\data\pg_hba.conf`
- Método: `scram-sha-256`
- Para resetar senha: mudar para `trust`, reiniciar serviço, alterar senha, voltar para `scram-sha-256`

## Desktop (ArchNexus.exe)

- Localização: `dist-desktop/ArchNexus.exe`
- Build: `scripts/build-desktop.ps1`
- Usa PostgreSQL (não SQLite)
- Frontend embutido no exe via ManifestEmbeddedFileProvider

## Comandos Úteis

```powershell
# Parar/iniciar PostgreSQL (como Admin)
net stop postgresql-x64-18
net start postgresql-x64-18

# Build do Desktop
powershell -ExecutionPolicy Bypass -File scripts\build-desktop.ps1

# Conectar no PostgreSQL
$env:PGPASSWORD = 'KGzin123'; psql -h localhost -U kgzin -d ArchNexus
```
