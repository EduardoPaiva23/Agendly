import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { ActiveBadge, Alert, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Modal, PageHeader } from '../../components/ui'
import { applyServerErrors, errorMessage } from '../../lib/api'
import { formatPhone } from '../../lib/datetime'
import { useAuth } from '../../lib/useAuth'
import type { Professional } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'
import { TimeOffsDialog } from './TimeOffsDialog'

const digits = (v: string) => v.replace(/\D/g, '')

const schema = z.object({
  name: z.string().trim().min(1, 'Informe o nome.').max(120),
  phone: z.string().refine((v) => v.trim() === '' || (digits(v).length >= 10 && digits(v).length <= 13), 'Telefone com DDD ou vazio.'),
  isActive: z.boolean(),
})
type FormValues = z.infer<typeof schema>

type Dialog = { kind: 'edit'; professional: Professional | null } | { kind: 'services' | 'timeoffs'; professional: Professional }

export function ProfessionalsPage() {
  const { isAdmin } = useAuth()
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const professionals = useQuery({ queryKey: adminKeys.professionals, queryFn: adminApi.professionals })
  const services = useQuery({ queryKey: adminKeys.services, queryFn: adminApi.services })
  const serviceName = (id: number) => services.data?.find((s) => s.id === id)?.name ?? `#${id}`

  return (
    <>
      <PageHeader
        title="Profissionais"
        description="Serviços que cada profissional realiza, folgas e bloqueios de agenda."
        actions={isAdmin && <Button onClick={() => setDialog({ kind: 'edit', professional: null })}>Novo profissional</Button>}
      />

      {professionals.isPending ? (
        <Loading />
      ) : professionals.isError ? (
        <Alert tone="error">{errorMessage(professionals.error)}</Alert>
      ) : professionals.data.length === 0 ? (
        <EmptyState title="Nenhum profissional cadastrado." />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {professionals.data.map((p) => (
            <Card key={p.id} className="flex flex-col gap-3">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="font-semibold text-slate-900">{p.name}</p>
                  <p className="text-sm text-slate-500">{p.phone ? formatPhone(p.phone) : 'Sem telefone'}</p>
                </div>
                <ActiveBadge active={p.isActive} />
              </div>
              <div className="flex flex-wrap gap-1.5">
                {p.serviceIds.length === 0 ? (
                  <span className="text-xs text-amber-700">Nenhum serviço associado: não aparece no agendamento.</span>
                ) : (
                  p.serviceIds.map((id) => (
                    <span key={id} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                      {serviceName(id)}
                    </span>
                  ))
                )}
              </div>
              <div className="mt-auto flex flex-wrap gap-2 border-t border-slate-100 pt-3">
                <Button variant="secondary" onClick={() => setDialog({ kind: 'timeoffs', professional: p })}>
                  Folgas e bloqueios
                </Button>
                {isAdmin && (
                  <>
                    <Button variant="ghost" onClick={() => setDialog({ kind: 'services', professional: p })}>
                      Serviços
                    </Button>
                    <Button variant="ghost" onClick={() => setDialog({ kind: 'edit', professional: p })}>
                      Editar
                    </Button>
                  </>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      {dialog?.kind === 'edit' && <ProfessionalForm professional={dialog.professional} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'services' && <ServicesDialog professional={dialog.professional} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'timeoffs' && <TimeOffsDialog professional={dialog.professional} onClose={() => setDialog(null)} />}
    </>
  )
}

function ProfessionalForm({ professional, onClose }: { professional: Professional | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: professional?.name ?? '',
      phone: professional?.phone ? formatPhone(professional.phone) : '',
      isActive: professional?.isActive ?? true,
    },
  })

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const body = { name: v.name.trim(), phone: v.phone.trim() || null, isActive: v.isActive }
      return professional ? adminApi.updateProfessional(professional.id, body) : adminApi.createProfessional(body)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminKeys.professionals })
      void queryClient.invalidateQueries({ queryKey: ['public'] })
      onClose()
    },
    onError: (error) => applyServerErrors(error, setError, ['name', 'phone']),
  })

  return (
    <Modal
      open
      title={professional ? 'Editar profissional' : 'Novo profissional'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="professional-form" loading={save.isPending}>
            Salvar
          </Button>
        </>
      }
    >
      <form id="professional-form" className="space-y-4" noValidate onSubmit={handleSubmit((v) => save.mutate(v))}>
        <Field label="Nome" error={errors.name?.message}>
          {(id, invalid) => <Input id={id} aria-invalid={invalid} {...register('name')} />}
        </Field>
        <Field label="Telefone (opcional)" error={errors.phone?.message} hint="Não é exibido na área pública.">
          {(id, invalid) => <Input id={id} type="tel" aria-invalid={invalid} {...register('phone')} />}
        </Field>
        <Checkbox label="Ativo (recebe novos agendamentos)" {...register('isActive')} />
        {save.isError && <Alert tone="error">{errorMessage(save.error)}</Alert>}
      </form>
    </Modal>
  )
}

function ServicesDialog({ professional, onClose }: { professional: Professional; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<number[]>(professional.serviceIds)
  const services = useQuery({ queryKey: adminKeys.services, queryFn: adminApi.services })

  const save = useMutation({
    mutationFn: () => adminApi.setProfessionalServices(professional.id, selected),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminKeys.professionals })
      void queryClient.invalidateQueries({ queryKey: ['public'] })
      onClose()
    },
  })

  const toggle = (id: number) => setSelected((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]))

  return (
    <Modal
      open
      title={`Serviços de ${professional.name}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Salvar
          </Button>
        </>
      }
    >
      {services.isPending ? (
        <Loading />
      ) : (
        <div className="space-y-2">
          {services.data?.map((s) => (
            <Checkbox
              key={s.id}
              checked={selected.includes(s.id)}
              onChange={() => toggle(s.id)}
              label={
                <span>
                  {s.name}
                  {!s.isActive && <span className="ml-1 text-xs text-slate-400">(inativo)</span>}
                </span>
              }
            />
          ))}
        </div>
      )}
      {save.isError && (
        <div className="mt-4">
          <Alert tone="error">{errorMessage(save.error)}</Alert>
        </div>
      )}
    </Modal>
  )
}
