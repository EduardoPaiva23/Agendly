# Banco de dados

SQL Server 2022, acessado via EF Core 10 (code-first). O schema é definido em `backend/src/BeautyAgenda.Infrastructure/Persistence/Configurations/EntityConfigurations.cs` e versionado em `Persistence/Migrations`.

## Tabelas

| Tabela | Finalidade | Observações |
|---|---|---|
| `Customers` | Clientes | `Phone` só com dígitos e **único**: é por ele que um cliente recorrente é reconhecido. |
| `Professionals` | Profissionais | `IsActive` em vez de exclusão. |
| `Services` | Catálogo | `Name` único; `Price decimal(10,2)`; `IsActive`. |
| `ProfessionalServices` | Quem realiza o quê (N:N) | PK composta `(ProfessionalId, ServiceId)`. |
| `BusinessHours` | Expediente semanal do salão | `TimeOnly` no horário **local**; vários intervalos por dia; único em `(DayOfWeek, StartTime)`. |
| `ProfessionalTimeOffs` | Folgas e bloqueios | Instantes em UTC. |
| `Appointments` | Agendamentos | `PublicReference` único e aleatório; `Status` como texto; `RowVersion`. |
| `AppointmentServices` | Serviços do agendamento | `PriceAtBooking` e `DurationMinutes` **congelados** na reserva. |
| `AdminAuditLogs` | Auditoria administrativa | `Details` em JSON (até 4000 caracteres). |
| `AspNet*` | ASP.NET Core Identity | `AspNetUsers` acrescenta `FullName` e `IsActive`. |

## Índices e por que existem

| Índice | Consulta atendida |
|---|---|
| `Appointments (ProfessionalId, StartAt, EndAt)` | Sobreposição: `WHERE ProfessionalId = @p AND StartAt < @fim AND EndAt > @inicio`. É a consulta mais crítica do sistema. |
| `Appointments (StartAt, Status)` | Agenda e dashboard por período. |
| `Appointments (CustomerId)` | Histórico do cliente e FK. |
| `Appointments (PublicReference)` **único** | Consulta e cancelamento pelo link público. |
| `ProfessionalTimeOffs (ProfessionalId, StartAt, EndAt)` | Mesma consulta de sobreposição, para os bloqueios. |
| `Customers (Phone)` **único** | Reconhecer cliente recorrente; impedir duplicidade. |
| `Customers (Name)` | Busca no painel. |
| `Services (Name)` **único** | Evitar serviços duplicados. |
| `AdminAuditLogs (EntityName, EntityId)`, `(CreatedAt)` | Histórico de um registro; consultas por período. |

## Integridade no banco

As regras críticas são garantidas também pelo banco, não só pela aplicação:

- **Check constraints:** `StartAt < EndAt` (agendamentos e bloqueios), `StartTime < EndTime`, `DayOfWeek BETWEEN 0 AND 6`, `DurationMinutes > 0`, `Price >= 0`.
- **Chaves estrangeiras:**
  - `Appointments → Customers/Professionals`: `Restrict`. Não é possível apagar quem tem histórico.
  - `AppointmentServices → Services`: `Restrict`. Serviços já vendidos não podem ser apagados, só desativados.
  - `AppointmentServices → Appointments`, `ProfessionalServices`, `ProfessionalTimeOffs`: `Cascade`, pois são partes do registro pai.
- **`rowversion` em `Appointments`:** dois administradores alterando o mesmo agendamento ao mesmo tempo resultam em 409 para o segundo, sem sobrescrita silenciosa.
- **Violação de índice único** (erros SQL 2601/2627) é convertida em 409 pelo `AppDbContext`. Isso cobre a corrida rara entre a checagem da aplicação e o `INSERT`.

## Datas e fuso horário

| Dado | Tipo no banco | Referência |
|---|---|---|
| `StartAt`, `EndAt`, `CreatedAt`, `CancelledAt`… | `datetime2` | **UTC** |
| `BusinessHours.StartTime/EndTime` | `time` | horário **local do salão** |

O motivo da diferença: um instante ("a cliente chega às 14h do dia 3") é um ponto absoluto e vai em UTC. O expediente ("abrimos às 9h") é uma regra de calendário local e deve continuar significando 9h locais mesmo que o offset mude (horário de verão). A conversão é feita em `SalonClock`, usando o fuso IANA configurado (`Salon:TimeZone`).

O EF Core não guarda o `DateTimeKind`. Um `ValueConverter` global (`AppDbContext.ConfigureConventions`) marca todo `DateTime` lido como `Utc`, para que conversões posteriores não tratem o valor como hora local da máquina.

## Migrations

Gerar uma nova migration (a fábrica de design-time dispensa rodar a API):

```bash
cd backend
dotnet ef migrations add <Nome> \
  --project src/BeautyAgenda.Infrastructure \
  --startup-project src/BeautyAgenda.Infrastructure \
  --output-dir Persistence/Migrations
```

Aplicar:

- **Desenvolvimento e Docker:** automático na inicialização da API (`Database:ApplyMigrationsOnStartup=true`). O EF Core 10 usa uma trava de migração, então várias instâncias subindo juntas não aplicam a mesma migration duas vezes.
- **Manual:** `dotnet ef database update` com a variável `BEAUTYAGENDA_CONNECTION` apontando para o banco.
- **Produção (recomendado):** gerar um script idempotente revisável com `dotnet ef migrations script --idempotent` e aplicá-lo no pipeline de deploy, mantendo `ApplyMigrationsOnStartup=false`.

## Dados de demonstração

Com `Seed:DemoData=true` e o catálogo vazio, `DatabaseInitializer` cria:
- 7 serviços e 3 profissionais, cada um com sua especialidade;
- expediente de terça a sexta (09–12 e 13–19) e aos sábados (08–14);
- 3 clientes e 6 agendamentos nos próximos dias úteis.

O seed é idempotente: não roda de novo se já houver serviços cadastrados.
