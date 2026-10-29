# Regras de negócio

Catálogo das regras do sistema, com **onde cada uma é aplicada** e **qual teste a garante**. Testes com prefixo `U:` são unitários (`BeautyAgenda.UnitTests`); com `I:`, de integração (`BeautyAgenda.IntegrationTests`).

## Disponibilidade

| # | Regra | Onde | Teste |
|---|---|---|---|
| D1 | Só profissionais **ativos** aparecem e podem receber reservas. | `ScheduleReader.GetEligibleProfessionalsAsync` | I: `Inactive_or_unqualified_professionals_are_not_offered` |
| D2 | O profissional precisa realizar **todos** os serviços escolhidos. | idem | I: idem; I: `Invalid_input_returns_400_and_client_values_are_never_trusted` |
| D3 | O atendimento inteiro precisa caber em um intervalo do expediente; o último horário termina exatamente no fechamento. | `AvailabilityCalculator` | U: `Last_slot_ends_exactly_at_closing_time`, `Service_longer_than_working_interval_has_no_slots` |
| D4 | Um serviço não atravessa a pausa entre intervalos (09–12 / 13–18); intervalos encostados (09–12 / 12–14) são unidos. | `AvailabilityCalculator.Merge` | U: `Slot_cannot_span_a_gap_between_intervals`, `Touching_intervals_are_merged_so_a_service_may_cross_the_boundary` |
| D5 | Dia sem expediente não tem horários. | `AvailabilityCalculator` | U: `Day_without_business_hours_has_no_slots`; I: `Booking_respects_minimum_advance_and_sunday_closure` |
| D6 | Folgas e bloqueios removem só o período bloqueado. | `ScheduleReader.GetBusyRangesAsync` | U: `Partial_time_off_blocks_only_its_period`; I: `Time_off_removes_slots_and_deleting_it_restores_them` |
| D7 | Agendamentos `Scheduled`/`Confirmed` ocupam a agenda; `Cancelled` e `Completed` não bloqueiam novos horários. | idem | I: `Customer_can_cancel_by_reference_and_slot_is_released` |
| D8 | Sobreposição: `novoInicio < fimExistente E novoFim > inicioExistente`. Intervalos encostados não conflitam. | `TimeRange.Overlaps` | U: `Overlap_follows_NewStart_lt_ExistingEnd_and_NewEnd_gt_ExistingStart`, `Existing_appointment_blocks_overlapping_slots_but_not_adjacent_ones` |
| D9 | Antecedência mínima para reservar online (padrão 60 min) e horizonte máximo (60 dias). | `ScheduleReader.PublicWindow` | U: `Minimum_advance_and_maximum_horizon_are_respected`; I: `Booking_respects_minimum_advance_and_sunday_closure` |
| D10 | Horários na grade de 15 min a partir do início de cada intervalo; um horário fora da grade é recusado na reserva pública. | `AvailabilityCalculator` + `PublicBookingService` | I: `Times_that_are_not_offered_are_rejected` |

## Reserva

| # | Regra | Onde | Teste |
|---|---|---|---|
| R1 | A duração é a **soma** das durações dos serviços, calculada no backend. | `Appointment.Create` | U: `Create_sums_durations_and_snapshots_prices`; I: `Public_booking_creates_appointment_and_removes_the_slot` |
| R2 | Preço e duração vêm do banco; o cliente envia só ids. Ids inexistentes, inativos ou repetidos → 400. | `AvailabilityService.LoadActiveServicesAsync`, validadores | I: `Invalid_input_returns_400_and_client_values_are_never_trusted` |
| R3 | Pelo menos 1 e no máximo 5 serviços por agendamento (configurável). | validadores, `Appointment.Create` | U: `Create_requires_at_least_one_service` |
| R4 | Horário ocupado entre a consulta e a confirmação → 409. | `PublicBookingService.CreateAsync` | I: `Booking_an_occupied_slot_returns_409` |
| R5 | **Reservas simultâneas no mesmo horário confirmam exatamente uma.** | trava `LockProfessionalSchedulesAsync` + revalidação na transação | I: `Concurrent_requests_for_the_same_slot_confirm_exactly_one`, `Concurrent_overlapping_requests_with_different_starts_confirm_only_one` |
| R6 | Preço e duração de cada serviço ficam congelados; mudar o catálogo não altera agendamentos existentes. | `AppointmentService.PriceAtBooking/DurationMinutes` | U: `Create_sums_durations_and_snapshots_prices`; I: `Contracted_price_and_duration_survive_catalog_changes` |
| R7 | Cliente reconhecido pelo telefone normalizado, sem sobrescrever dados vindos de formulário anônimo. | `PublicBookingService.CreateAsync` | I: `Returning_customer_is_recognized_by_phone_without_overwriting_data` |
| R8 | Cada agendamento recebe uma referência pública aleatória de 128 bits. | `PublicReference.New` | U: `Public_references_are_random_url_safe_and_well_formed` |

## Estados

| # | Regra | Onde | Teste |
|---|---|---|---|
| E1 | Transições permitidas: Agendado → Confirmado/Concluído/Cancelado; Confirmado → Concluído/Cancelado. | `Appointment.AllowedTransitions` | U: `Allowed_transitions` |
| E2 | Concluído e Cancelado são finais: qualquer outra transição → 409. | `Appointment.ChangeStatus` | U: `Forbidden_transitions_throw`; I: `Status_transitions_are_enforced` |
| E3 | Cancelar grava data e motivo e mantém o registro e os serviços. | `Appointment.Cancel` | U: `Cancel_keeps_history_and_records_reason`; I: `Admin_cancel_preserves_history_and_lists_with_filters` |
| E4 | Mudanças simultâneas no mesmo agendamento → 409 (sem sobrescrita silenciosa). | `RowVersion` + `AppDbContext.SaveChangesAsync` | — (garantido pelo EF; sem teste dedicado) |

## Reagendamento

| # | Regra | Onde | Teste |
|---|---|---|---|
| G1 | Só agendamentos ativos podem ser reagendados. | `Appointment.Reschedule` | U: `Reschedule_of_finished_appointment_is_rejected` |
| G2 | O novo horário passa pelas mesmas regras de disponibilidade, ignorando o próprio agendamento (pode deslocar sobre o horário atual). | `AppointmentsService.RescheduleAsync` | I: `Reschedule_moves_appointment_keeps_price_and_frees_old_slot` |
| G3 | Em caso de conflito, nada muda (operação atômica). | transação | I: `Reschedule_into_conflict_fails_and_leaves_appointment_unchanged` |
| G4 | Trocar de profissional exige que o novo realize todos os serviços. | idem | I: `Reschedule_to_unqualified_professional_is_rejected` |
| G5 | Os valores contratados são mantidos. | `Appointment.Reschedule` | U: `Reschedule_moves_interval_and_keeps_contracted_values` |
| G6 | A equipe pode reagendar sem a antecedência mínima da área pública, mas nunca para o passado. | `ScheduleReader.StaffWindow` | — |

## Bloqueios e expediente

| # | Regra | Onde | Teste |
|---|---|---|---|
| B1 | Não é possível bloquear um período com agendamentos ativos: é preciso reagendar ou cancelar antes. | `TimeOffsService.CreateAsync` | I: `Time_off_over_an_active_appointment_is_rejected` |
| B2 | Bloqueio com fim antes do início ou com mais de 90 dias → 400. | validador | I: idem |
| B3 | Intervalos do expediente no mesmo dia não podem se sobrepor; início < fim; formato `HH:mm`. | `UpdateBusinessHoursRequestValidator` | U: `Rejects_overlapping_intervals_on_same_day`, `Rejects_invalid_times`, `Accepts_split_day_and_empty_week` |
| B4 | Alterar o expediente não altera agendamentos já marcados. | `BusinessHoursService.ReplaceAsync` | — |

## Cancelamento pelo cliente

| # | Regra | Onde | Teste |
|---|---|---|---|
| C1 | Feito pela referência secreta; referência malformada e inexistente respondem igual (404). | `PublicBookingService.FindAsync` | I: `Unknown_or_malformed_reference_returns_404` |
| C2 | Só até 2 h antes do início (configurável); depois disso, só pelo salão. | `PublicBookingService.CanCancelOnline` | I: `Online_cancellation_too_close_to_start_is_rejected` |
| C3 | Cancelar duas vezes → 409. | idem | I: `Customer_can_cancel_by_reference_and_slot_is_released` |

## Acesso e privacidade

| # | Regra | Onde | Teste |
|---|---|---|---|
| S1 | Todo endpoint exige autenticação, exceto os marcados como públicos. | *fallback policy* em `ApiSetup` | I: `Admin_endpoints_require_authentication` |
| S2 | Staff opera a agenda; só Admin altera catálogo, profissionais e expediente. | políticas `AdminOnly` / `StaffOrAdmin` | I: `Staff_cannot_manage_catalog_but_can_operate_agenda` |
| S3 | Endpoints públicos não expõem dados de clientes nem telefone de profissionais. | DTOs `Public*` | I: `Public_endpoints_are_anonymous_and_do_not_expose_contact_data` |
| S4 | Login inválido, usuário inexistente ou inativo recebem a mesma resposta 401. | `AuthService.LoginAsync` | I: `Login_with_wrong_password_returns_401`, `Login_with_unknown_user_returns_same_401` |
| S5 | Conta desativada perde o acesso imediatamente, mesmo com token válido. | `JwtBearerEvents.OnTokenValidated` | — |

## Fuso horário

| # | Regra | Onde | Teste |
|---|---|---|---|
| F1 | O expediente é interpretado no fuso do salão e convertido para UTC. | `SalonClock.ToUtc` | U: `Local_business_time_is_converted_to_utc` |
| F2 | "Hoje" é o dia no salão, não o dia em UTC. | `SalonClock.Today` | U: `Today_uses_salon_time_zone_not_utc` |
| F3 | Respostas trazem o offset do salão. | `SalonClock.ToLocal` | U: `ToLocal_returns_offset_of_salon` |

**Lacunas conhecidas:** E4, G6, B4 e S5 não têm teste dedicado. São candidatos naturais aos próximos testes.
