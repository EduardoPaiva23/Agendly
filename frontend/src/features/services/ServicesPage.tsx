import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { ActiveBadge, Alert, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Modal, PageHeader, Table, Td, Textarea, Th } from '../../components/ui'
import { applyServerErrors, errorMessage } from '../../lib/api'
import { currency, formatDuration } from '../../lib/datetime'
import { useAuth } from '../../lib/useAuth'
import type { Service } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'

const schema = z.object({
  name: z.string().trim().min(1, 'Informe o nome.').max(100),
  description: z.string().max(500, 'Máximo de 500 caracteres.'),
  durationMinutes: z
    .number({ message: 'Informe a duração.' })
    .int()
    .min(5, 'Mínimo de 5 minutos.')
    .max(480, 'Máximo de 8 horas.')
    .refine((v) => v % 5 === 0, 'Use múltiplos de 5 minutos.'),
  price: z.number({ message: 'Informe o preço.' }).min(0, 'O preço não pode ser negativo.').max(99_999.99),
  isActive: z.boolean(),
})
type FormValues = z.infer<typeof schema>

export function ServicesPage() {
  const { isAdmin } = useAuth()
  const [editing, setEditing] = useState<Service | 'new' | null>(null)
  const services = useQuery({ queryKey: adminKeys.services, queryFn: adminApi.services })

  return (
    <>
      <PageHeader
        title="Serviços"
        description={isAdmin ? 'Alterar preço ou duração não muda agendamentos já feitos.' : 'Somente administradores podem alterar serviços.'}
        actions={isAdmin && <Button onClick={() => setEditing('new')}>Novo serviço</Button>}
      />
      <Card>
        {services.isPending ? (
          <Loading />
        ) : services.isError ? (
          <Alert tone="error">{errorMessage(services.error)}</Alert>
        ) : services.data.length === 0 ? (
          <EmptyState title="Nenhum serviço cadastrado." />
        ) : (
          <Table
            head={
              <tr>
                <Th>Serviço</Th>
                <Th>Duração</Th>
                <Th>Preço</Th>
                <Th>Situação</Th>
                {isAdmin && <Th />}
              </tr>
            }
          >
            {services.data.map((s) => (
              <tr key={s.id}>
                <Td>
                  <span className="font-medium text-slate-900">{s.name}</span>
                  {s.description && <span className="block text-xs text-slate-500">{s.description}</span>}
                </Td>
                <Td className="whitespace-nowrap">{formatDuration(s.durationMinutes)}</Td>
                <Td className="whitespace-nowrap">{currency.format(s.price)}</Td>
                <Td>
                  <ActiveBadge active={s.isActive} />
                </Td>
                {isAdmin && (
                  <Td className="text-right">
                    <Button variant="ghost" onClick={() => setEditing(s)}>
                      Editar
                    </Button>
                  </Td>
                )}
              </tr>
            ))}
          </Table>
        )}
      </Card>
      {editing && <ServiceForm service={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function ServiceForm({ service, onClose }: { service: Service | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: service?.name ?? '',
      description: service?.description ?? '',
      durationMinutes: service?.durationMinutes ?? 60,
      price: service?.price ?? 0,
      isActive: service?.isActive ?? true,
    },
  })

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const body = { ...v, name: v.name.trim(), description: v.description.trim() || null }
      return service ? adminApi.updateService(service.id, body) : adminApi.createService(body)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminKeys.services })
      void queryClient.invalidateQueries({ queryKey: ['public'] })
      onClose()
    },
    onError: (error) => applyServerErrors(error, setError, ['name', 'description', 'durationMinutes', 'price']),
  })

  return (
    <Modal
      open
      title={service ? 'Editar serviço' : 'Novo serviço'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="service-form" loading={save.isPending}>
            Salvar
          </Button>
        </>
      }
    >
      <form id="service-form" className="space-y-4" noValidate onSubmit={handleSubmit((v) => save.mutate(v))}>
        <Field label="Nome" error={errors.name?.message}>
          {(id, invalid) => <Input id={id} aria-invalid={invalid} {...register('name')} />}
        </Field>
        <Field label="Descrição" error={errors.description?.message}>
          {(id, invalid) => <Textarea id={id} aria-invalid={invalid} {...register('description')} />}
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Duração (min)" error={errors.durationMinutes?.message}>
            {(id, invalid) => <Input id={id} type="number" min={5} step={5} aria-invalid={invalid} {...register('durationMinutes', { valueAsNumber: true })} />}
          </Field>
          <Field label="Preço (R$)" error={errors.price?.message}>
            {(id, invalid) => <Input id={id} type="number" min={0} step={0.01} aria-invalid={invalid} {...register('price', { valueAsNumber: true })} />}
          </Field>
        </div>
        <Checkbox label="Disponível para agendamento" {...register('isActive')} />
        {save.isError && <Alert tone="error">{errorMessage(save.error)}</Alert>}
      </form>
    </Modal>
  )
}
