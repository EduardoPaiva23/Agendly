# Roteiro para apresentar o projeto

Material para entrevistas técnicas (vagas C#/.NET). A ideia é conduzir a conversa para as partes mais fortes: regras de disponibilidade, concorrência e testes.

## Pitch de 30 segundos

> "O BeautyAgenda é um sistema de agendamento para salão de beleza, em ASP.NET Core 10 com SQL Server e um frontend em React. O cliente agenda sem criar conta e a equipe gerencia tudo num painel. O foco técnico foi a parte difícil de qualquer agenda: calcular horários livres corretamente e garantir que duas pessoas nunca reservem o mesmo horário, mesmo clicando ao mesmo tempo. Isso está provado por um teste de integração que dispara 10 reservas simultâneas contra um SQL Server real e verifica que só uma é confirmada."

## Roteiro de 5 minutos

1. **Problema** (30 s): agenda parece CRUD, mas tem três armadilhas: sobreposição de intervalos, concorrência e fuso horário.
2. **Arquitetura** (1 min): quatro camadas (Domain, Application, Infrastructure, Api). O cálculo de disponibilidade é uma função pura no Domain, testada isoladamente, e é a **mesma** função usada para listar horários e para revalidar dentro da transação.
3. **Concorrência** (1 min 30): checar e depois inserir tem uma janela de corrida. Abro uma transação, travo a linha do profissional com `UPDLOCK, HOLDLOCK`, revalido e insiro. A segunda requisição espera, revalida, encontra o horário ocupado e recebe 409. Explicar por que um índice único não resolve: ele só pega inícios idênticos, não sobreposições.
4. **Histórico** (30 s): preço e duração congelados por serviço na reserva; cancelamento sem exclusão física; auditoria na mesma transação.
5. **Testes** (1 min): 57 unitários para as regras e 41 de integração com Testcontainers, que sobem um SQL Server real. Explicar a diferença: o unitário prova a regra; o de integração prova o SQL gerado pelo EF, a transação, a trava e o HTTP.
6. **Segurança** (30 s): JWT com perfis Admin/Staff, *fallback policy* que exige login por padrão, referência pública de 128 bits, nenhum dado pessoal nos endpoints públicos.

## Perguntas prováveis e respostas

**Por que não usou o padrão Repository?**
O `DbContext` já é Unit of Work e Repository. Um repositório genérico esconderia `Include`, projeções e `AsNoTracking`. Como os testes de integração usam banco real, não preciso de mock. Abstraí só o que é específico do SQL Server (a trava), atrás de `IApplicationDbContext`.

**E se o sistema tivesse vários servidores da API?**
A trava está no banco, não em memória, então continua funcionando com N instâncias. Um `lock` em C# ou um `SemaphoreSlim` só protegeria uma instância.

**Por que não `SERIALIZABLE`?**
Também resolveria, mas gera *range locks* e deadlocks sob carga e exige repetir a operação. A trava explícita na linha do profissional é mais previsível e só serializa as escritas daquele profissional.

**Como evita deadlock no reagendamento, que envolve dois profissionais?**
As travas são sempre adquiridas em ordem crescente de id.

**Como trata fuso horário?**
Instantes em UTC no banco; o expediente em `TimeOnly` local, porque "abrimos às 9h" é regra de calendário, não um instante. A conversão fica num único serviço (`SalonClock`), com `TimeProvider` injetado. Um teste cobre o caso em que já é o dia seguinte em UTC, mas ainda é hoje em São Paulo.

**Por que guardar o preço no agendamento se ele já está no serviço?**
Porque o preço muda. Um relatório de faturamento de março não pode mudar quando o salão reajusta os preços em abril.

**Como garante que o frontend não burla as regras?**
O frontend só manda ids e o horário; duração e preço são recalculados no backend. A disponibilidade é revalidada na reserva. A validação no React é só conveniência de UX.

**O que faria diferente em produção?**
- Token em cookie `HttpOnly` em vez de `localStorage`.
- Migrations aplicadas por script no pipeline, e não na inicialização da API.
- Logs estruturados com correlação (Serilog + OpenTelemetry).
- Notificações com o padrão *outbox*.
- Expediente por profissional.

**Qual foi a maior dificuldade?**
Sugestões honestas:
- perceber que a checagem de disponibilidade fora da transação deixava uma janela de corrida, e escrever um teste que a reproduzisse;
- ou os casos de borda de intervalos: adjacência, pausa de almoço e o último horário do dia.

## O que mostrar ao vivo

1. Swagger: `GET /api/public/availability` com dois serviços, mostrando a duração somada.
2. Frontend: fazer uma reserva e mostrar o horário sumindo da lista.
3. Código: `AvailabilityCalculator.cs` (curto e puro) e `PublicBookingService.CreateAsync` (transação, trava e revalidação).
4. Teste: `Concurrent_requests_for_the_same_slot_confirm_exactly_one` e a execução da suíte.
