# Arquitetura

Visão geral para quem vai manter ou avaliar o código. O README traz o resumo; aqui ficam o fluxo de uma requisição e o **porquê** das decisões, com as alternativas descartadas.

## Camadas

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure
```

| Camada | Pode depender de | Não pode conhecer |
|---|---|---|
| **Domain** | nada | EF Core, HTTP, configuração |
| **Application** | Domain, abstrações do EF Core (`DbSet`, LINQ) | SQL Server, Identity, ASP.NET |
| **Infrastructure** | Application | controllers, HTTP |
| **Api** | todas (composição) | — |

A Application referencia o pacote `Microsoft.EntityFrameworkCore`, mas não o provider do SQL Server. É uma concessão consciente: escrever consultas LINQ diretamente sobre `DbSet` é mais simples e mais legível do que criar repositórios para um único banco. O que é específico do SQL Server (a trava `UPDLOCK`, a tradução de erros de índice único) fica atrás de `IApplicationDbContext`, na Infrastructure.

## Fluxo de uma requisição: reserva pública

```
POST /api/public/appointments
│
├─ RateLimiter ("public-write", por IP)
├─ PublicController.Book                         Api
│    └─ PublicBookingService.CreateAsync          Application
│         ├─ CreatePublicAppointmentRequestValidator   → 400 se inválido
│         ├─ LoadActiveServicesAsync (preço/duração do banco, nunca do cliente)
│         ├─ GetEligibleProfessionalsAsync               → 400 se não atende os serviços
│         └─ ExecuteInTransactionAsync                   Infrastructure
│              ├─ LockProfessionalSchedulesAsync  (UPDLOCK na linha do profissional)
│              ├─ ScheduleReader: expediente + ocupados do dia
│              ├─ AvailabilityCalculator.GetAvailableSlots   Domain (função pura)
│              │     └─ horário não está na lista      → ConflictException → 409
│              ├─ cliente existente (telefone) ou novo
│              ├─ Appointment.Create (snapshot de preço/duração)
│              └─ SaveChanges + COMMIT
└─ 201 Created + Location: /api/public/appointments/{reference}
```

Erros viajam como exceções tipadas e são convertidos em `ProblemDetails` por um único `GlobalExceptionHandler`. Os controllers não têm `try/catch`.

## Decisões

### 1. Concorrência pessimista por profissional, e não índice único nem `SERIALIZABLE`

**Problema:** duas reservas simultâneas podem passar pela checagem de disponibilidade antes de qualquer uma gravar.

| Alternativa | Por que não |
|---|---|
| Índice único em `(ProfessionalId, StartAt)` | Só impede inícios idênticos. 10:00–11:00 e 10:30–11:30 se sobrepõem e passariam. |
| Constraint de exclusão de intervalos | Existe no PostgreSQL (`EXCLUDE USING gist`), não no SQL Server. |
| Transação `SERIALIZABLE` sem trava explícita | Funciona, mas gera deadlocks nas *range locks* sob carga e exige repetir a operação. |
| Concorrência otimista (`rowversion`) no profissional | Exigiria atualizar a linha do profissional a cada reserva só para mudar a versão, e repetir a operação no cliente. |
| **`UPDLOCK, HOLDLOCK` na linha do profissional** ✔ | Serializa só as escritas na agenda daquele profissional, é simples de entender, sem deadlock (ordem fixa de ids) e provado por teste. |

O custo é que reservas para o **mesmo** profissional passam a ser sequenciais. Num salão, isso significa poucas por minuto, irrelevante.

### 2. Cálculo de disponibilidade como função pura no Domain

`AvailabilityCalculator` não sabe o que é banco, fuso ou HTTP: recebe intervalos e devolve intervalos. Isso permite:
- testes unitários rápidos e exaustivos de casos de borda;
- reutilização idêntica em dois pontos: na **consulta** (lista de horários) e na **revalidação dentro da transação** (reserva e reagendamento). Não existem duas versões da regra que possam divergir.

### 3. Sem padrão Repository

O `DbContext` já implementa Unit of Work e Repository. Envolvê-lo em `IRepository<T>` genérico esconderia `Include`, projeções e `AsNoTracking`, sem trazer benefício real com um único banco. Os testes de integração usam SQL Server real, então não há necessidade de *mockar* repositórios.

### 4. Serviços de aplicação em vez de MediatR/CQRS

Uma classe de serviço por agregado (`AppointmentsService`, `CatalogService`…), com métodos assíncronos explícitos. Para o tamanho do projeto, *handlers* por comando adicionariam muitos arquivos e indireção sem ganho.

### 5. Entidade `Appointment` com comportamento

`Appointment` tem setters privados e métodos (`Create`, `ChangeStatus`, `Cancel`, `Reschedule`). A máquina de estados e o cálculo de `EndAt` não podem ser contornados por quem usa a entidade. As demais entidades (cadastros simples) são anêmicas de propósito.

### 6. UTC no banco, fuso do salão na borda

Detalhes em [database.md](database.md#datas-e-fuso-horário). A conversão acontece em um único lugar (`SalonClock`), e o tempo é injetado (`TimeProvider`) para os testes poderem fixar o "agora".

### 7. JWT stateless + revalidação de `IsActive`

Tokens JWT dispensam sessão no servidor. A desvantagem clássica, um token continuar válido depois que a conta é desativada, é resolvida em `OnTokenValidated`, que consulta o usuário a cada requisição. É uma consulta por chave primária, barata para o volume de um painel administrativo.

### 8. Frontend sem biblioteca de componentes

Tailwind e um conjunto pequeno de componentes próprios (`components/ui.tsx`). O pacote fica menor e a interface não depende de uma biblioteca externa. TanStack Query concentra o estado vindo do servidor, então não há Redux nem Context para dados da API.

## Pontos de extensão conhecidos

- **Notificações (e-mail/WhatsApp):** publicar um evento depois do `SaveChanges` da reserva, de preferência com o padrão *outbox*, para não perder mensagens se o envio falhar.
- **Expediente por profissional:** hoje o expediente é do salão e as exceções são bloqueios. Uma tabela `ProfessionalWorkingHours` entraria como mais uma fonte de intervalos para o `AvailabilityCalculator`, sem mudar a função.
- **Múltiplas unidades:** acrescentar `SalonId` às entidades e um filtro global no EF (*query filter*).
