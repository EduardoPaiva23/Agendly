import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Alert, Button, Card, EmptyState, Loading } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { currency, formatDay, formatDuration, formatTime } from '../../lib/datetime'
import type { PublicAppointment } from '../../types/api'
import { CustomerStep } from './CustomerStep'
import { publicApi, publicKeys } from './publicApi'
import { BookingSuccess } from './BookingSuccess'
import { SlotStep, type SelectedSlot } from './SlotStep'

type Step = 'services' | 'professional' | 'slot' | 'customer' | 'done'

const steps: { id: Exclude<Step, 'done'>; label: string }[] = [
  { id: 'services', label: 'Serviços' },
  { id: 'professional', label: 'Profissional' },
  { id: 'slot', label: 'Data e horário' },
  { id: 'customer', label: 'Seus dados' },
]

export function BookingPage() {
  const [step, setStep] = useState<Step>('services')
  const [serviceIds, setServiceIds] = useState<number[]>([])
  const [professionalId, setProfessionalId] = useState<number | undefined>(undefined)
  const [slot, setSlot] = useState<SelectedSlot | null>(null)
  const [slotConflict, setSlotConflict] = useState(false)
  const [booked, setBooked] = useState<PublicAppointment | null>(null)

  const services = useQuery({ queryKey: publicKeys.services, queryFn: publicApi.services })
  const professionals = useQuery({ queryKey: publicKeys.professionals, queryFn: publicApi.professionals })

  const selectedServices = useMemo(
    () => (services.data ?? []).filter((s) => serviceIds.includes(s.id)),
    [services.data, serviceIds],
  )
  const totalDuration = selectedServices.reduce((sum, s) => sum + s.durationMinutes, 0)
  const totalPrice = selectedServices.reduce((sum, s) => sum + s.price, 0)

  const eligibleProfessionals = useMemo(
    () => (professionals.data ?? []).filter((p) => serviceIds.every((id) => p.serviceIds.includes(id))),
    [professionals.data, serviceIds],
  )

  const toggleService = (id: number) => {
    setServiceIds((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))
    setProfessionalId(undefined)
    setSlot(null)
  }

  const restart = () => {
    setStep('services')
    setServiceIds([])
    setProfessionalId(undefined)
    setSlot(null)
    setBooked(null)
  }

  if (step === 'done' && booked) return <BookingSuccess appointment={booked} onNew={restart} />

  const currentIndex = steps.findIndex((s) => s.id === step)

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Agende seu horário</h1>
        <p className="mt-1 text-sm text-slate-500">Escolha os serviços, o profissional e um horário livre. Não é preciso criar conta.</p>
      </div>

      <ol className="grid grid-cols-4 gap-2 text-xs sm:text-sm" aria-label="Etapas do agendamento">
        {steps.map((s, i) => (
          <li
            key={s.id}
            aria-current={s.id === step ? 'step' : undefined}
            className={`border-t-4 pt-2 font-medium ${i <= currentIndex ? 'border-rose-500 text-rose-700' : 'border-slate-200 text-slate-400'}`}
          >
            <span className="hidden sm:inline">{i + 1}. </span>
            {s.label}
          </li>
        ))}
      </ol>

      {selectedServices.length > 0 && step !== 'services' && (
        <Card className="flex flex-wrap items-center justify-between gap-2 py-3 text-sm">
          <span className="text-slate-700">{selectedServices.map((s) => s.name).join(' + ')}</span>
          <span className="font-medium text-slate-900">
            {formatDuration(totalDuration)} · {currency.format(totalPrice)}
          </span>
        </Card>
      )}

      {step === 'services' && (
        <Card>
          <h2 className="mb-4 font-semibold">Quais serviços você deseja?</h2>
          {services.isPending ? (
            <Loading />
          ) : services.isError ? (
            <Alert tone="error">{errorMessage(services.error)}</Alert>
          ) : services.data.length === 0 ? (
            <EmptyState title="Nenhum serviço disponível no momento." />
          ) : (
            <ul className="grid gap-3 sm:grid-cols-2">
              {services.data.map((service) => {
                const checked = serviceIds.includes(service.id)
                return (
                  <li key={service.id}>
                    <label
                      className={`flex h-full cursor-pointer gap-3 rounded-lg p-3 ring-1 transition ${checked ? 'bg-rose-50 ring-2 ring-rose-500' : 'ring-slate-200 hover:ring-slate-300'}`}
                    >
                      <input type="checkbox" className="mt-1 size-4 accent-rose-600" checked={checked} onChange={() => toggleService(service.id)} />
                      <span className="flex-1">
                        <span className="flex justify-between gap-2 font-medium text-slate-900">
                          {service.name}
                          <span>{currency.format(service.price)}</span>
                        </span>
                        <span className="block text-xs text-slate-500">{formatDuration(service.durationMinutes)}</span>
                        {service.description && <span className="mt-1 block text-sm text-slate-600">{service.description}</span>}
                      </span>
                    </label>
                  </li>
                )
              })}
            </ul>
          )}
          <div className="mt-5 flex justify-end">
            <Button disabled={serviceIds.length === 0} onClick={() => setStep('professional')}>
              Continuar
            </Button>
          </div>
        </Card>
      )}

      {step === 'professional' && (
        <Card>
          <h2 className="mb-4 font-semibold">Com quem você prefere?</h2>
          {professionals.isPending ? (
            <Loading />
          ) : eligibleProfessionals.length === 0 ? (
            <EmptyState
              title="Nenhum profissional realiza todos esses serviços juntos."
              description="Tente agendar os serviços separadamente."
            />
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              <ChoiceButton selected={professionalId === undefined} onClick={() => setProfessionalId(undefined)}>
                <span className="font-medium">Qualquer profissional</span>
                <span className="block text-xs text-slate-500">Mostra todos os horários livres</span>
              </ChoiceButton>
              {eligibleProfessionals.map((p) => (
                <ChoiceButton key={p.id} selected={professionalId === p.id} onClick={() => setProfessionalId(p.id)}>
                  <span className="font-medium">{p.name}</span>
                </ChoiceButton>
              ))}
            </div>
          )}
          <div className="mt-5 flex justify-between">
            <Button variant="ghost" onClick={() => setStep('services')}>
              Voltar
            </Button>
            <Button disabled={eligibleProfessionals.length === 0} onClick={() => setStep('slot')}>
              Continuar
            </Button>
          </div>
        </Card>
      )}

      {step === 'slot' && (
        <SlotStep
          serviceIds={serviceIds}
          professionalId={professionalId}
          selected={slot}
          conflict={slotConflict}
          onSelect={(s) => {
            setSlot(s)
            setSlotConflict(false)
          }}
          onBack={() => setStep('professional')}
          onNext={() => setStep('customer')}
        />
      )}

      {step === 'customer' && slot && (
        <CustomerStep
          summary={`${formatDay(slot.date)}, às ${formatTime(slot.startAt)} com ${slot.professionalName}`}
          request={{ professionalId: slot.professionalId, serviceIds, startAt: slot.startAt }}
          onBack={() => setStep('slot')}
          onConflict={() => {
            setSlot(null)
            setSlotConflict(true)
            setStep('slot')
          }}
          onBooked={(appointment) => {
            setBooked(appointment)
            setStep('done')
          }}
        />
      )}
    </div>
  )
}

function ChoiceButton({ selected, onClick, children }: { selected: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={`rounded-lg p-3 text-left ring-1 transition ${selected ? 'bg-rose-50 ring-2 ring-rose-500' : 'ring-slate-200 hover:ring-slate-300'}`}
    >
      {children}
    </button>
  )
}
