import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert, Button, Card, Field, Input } from '../../components/ui'
import { ApiError, applyServerErrors, errorMessage } from '../../lib/api'
import type { PublicAppointment } from '../../types/api'
import { publicApi } from './publicApi'

const digits = (v: string) => v.replace(/\D/g, '')

const schema = z.object({
  customerName: z.string().trim().min(2, 'Informe seu nome.').max(120, 'Nome muito longo.'),
  customerPhone: z
    .string()
    .refine((v) => digits(v).length >= 10 && digits(v).length <= 13, 'Informe o celular com DDD, por exemplo (11) 98765-4321.'),
  customerEmail: z.union([z.literal(''), z.email('E-mail inválido.').max(254)]),
})

type FormValues = z.infer<typeof schema>

export function CustomerStep({
  summary,
  request,
  onBack,
  onConflict,
  onBooked,
}: {
  summary: string
  request: { professionalId: number; serviceIds: number[]; startAt: string }
  onBack: () => void
  onConflict: () => void
  onBooked: (appointment: PublicAppointment) => void
}) {
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { customerName: '', customerPhone: '', customerEmail: '' } })

  const book = useMutation({
    mutationFn: (values: FormValues) =>
      publicApi.book({
        ...request,
        customerName: values.customerName.trim(),
        customerPhone: values.customerPhone,
        customerEmail: values.customerEmail || null,
      }),
    onSuccess: (appointment) => {
      void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
      onBooked(appointment)
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
        onConflict()
        return
      }
      applyServerErrors(error, setError, ['customerName', 'customerPhone', 'customerEmail'])
    },
  })

  const generalError = book.isError && !(book.error instanceof ApiError && book.error.status === 409) ? errorMessage(book.error) : null

  return (
    <Card>
      <h2 className="font-semibold">Seus dados</h2>
      <p className="mb-4 mt-1 text-sm first-letter:uppercase text-slate-600">{summary}</p>

      <form className="space-y-4" noValidate onSubmit={handleSubmit((values) => book.mutate(values))}>
        <Field label="Nome" error={errors.customerName?.message}>
          {(id, invalid) => <Input id={id} autoComplete="name" aria-invalid={invalid} {...register('customerName')} />}
        </Field>
        <Field label="Celular (com DDD)" error={errors.customerPhone?.message} hint="Usado pelo salão para contato sobre o agendamento.">
          {(id, invalid) => (
            <Input id={id} type="tel" inputMode="tel" autoComplete="tel" placeholder="(11) 98765-4321" aria-invalid={invalid} {...register('customerPhone')} />
          )}
        </Field>
        <Field label="E-mail (opcional)" error={errors.customerEmail?.message}>
          {(id, invalid) => <Input id={id} type="email" autoComplete="email" aria-invalid={invalid} {...register('customerEmail')} />}
        </Field>

        {generalError && <Alert tone="error">{generalError}</Alert>}

        <div className="flex justify-between pt-1">
          <Button variant="ghost" onClick={onBack} disabled={book.isPending}>
            Voltar
          </Button>
          <Button type="submit" loading={book.isPending}>
            Confirmar agendamento
          </Button>
        </div>
      </form>
    </Card>
  )
}
