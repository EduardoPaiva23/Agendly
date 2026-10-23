import { zodResolver } from '@hookform/resolvers/zod'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert, Button, Card, EmptyState, Field, Input, Loading, Modal, PageHeader, Pagination, Table, Td, Th } from '../../components/ui'
import { applyServerErrors, errorMessage } from '../../lib/api'
import { formatDate, formatPhone } from '../../lib/datetime'
import type { Customer } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'

const digits = (v: string) => v.replace(/\D/g, '')

const schema = z.object({
  name: z.string().trim().min(1, 'Informe o nome.').max(120),
  phone: z.string().refine((v) => digits(v).length >= 10 && digits(v).length <= 13, 'Informe o telefone com DDD.'),
  email: z.union([z.literal(''), z.email('E-mail inválido.').max(254)]),
})
type FormValues = z.infer<typeof schema>

export function CustomersPage() {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Customer | 'new' | null>(null)

  useEffect(() => {
    const handle = setTimeout(() => {
      setSearch(searchInput.trim())
      setPage(1)
    }, 300)
    return () => clearTimeout(handle)
  }, [searchInput])

  const customers = useQuery({
    queryKey: adminKeys.customers(search, page),
    queryFn: () => adminApi.customers(search, page),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader title="Clientes" description="Cadastros feitos pelo salão ou pelo agendamento online." actions={<Button onClick={() => setEditing('new')}>Novo cliente</Button>} />

      <Card>
        <label htmlFor="customer-search" className="sr-only">
          Buscar
        </label>
        <Input
          id="customer-search"
          type="search"
          placeholder="Buscar por nome, telefone ou e-mail"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          className="mb-4 sm:max-w-sm"
        />

        {customers.isPending ? (
          <Loading />
        ) : customers.isError ? (
          <Alert tone="error">{errorMessage(customers.error)}</Alert>
        ) : customers.data.items.length === 0 ? (
          <EmptyState title={search ? 'Nenhum cliente encontrado.' : 'Nenhum cliente cadastrado ainda.'} />
        ) : (
          <>
            <Table
              head={
                <tr>
                  <Th>Nome</Th>
                  <Th>Telefone</Th>
                  <Th className="hidden sm:table-cell">E-mail</Th>
                  <Th className="hidden md:table-cell">Desde</Th>
                  <Th />
                </tr>
              }
            >
              {customers.data.items.map((c) => (
                <tr key={c.id}>
                  <Td className="font-medium text-slate-900">{c.name}</Td>
                  <Td className="whitespace-nowrap">{formatPhone(c.phone)}</Td>
                  <Td className="hidden sm:table-cell">{c.email ?? '–'}</Td>
                  <Td className="hidden md:table-cell">{formatDate(c.createdAt)}</Td>
                  <Td className="text-right">
                    <Button variant="ghost" onClick={() => setEditing(c)}>
                      Editar
                    </Button>
                  </Td>
                </tr>
              ))}
            </Table>
            <Pagination page={customers.data.page} totalPages={customers.data.totalPages} onChange={setPage} />
          </>
        )}
      </Card>

      {editing && <CustomerForm customer={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function CustomerForm({ customer, onClose }: { customer: Customer | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: customer?.name ?? '', phone: customer ? formatPhone(customer.phone) : '', email: customer?.email ?? '' },
  })

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const body = { name: values.name.trim(), phone: values.phone, email: values.email || null }
      return customer ? adminApi.updateCustomer(customer.id, body) : adminApi.createCustomer(body)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'customers'] })
      onClose()
    },
    onError: (error) => applyServerErrors(error, setError, ['name', 'phone', 'email']),
  })

  return (
    <Modal
      open
      title={customer ? 'Editar cliente' : 'Novo cliente'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="customer-form" loading={save.isPending}>
            Salvar
          </Button>
        </>
      }
    >
      <form id="customer-form" className="space-y-4" noValidate onSubmit={handleSubmit((v) => save.mutate(v))}>
        <Field label="Nome" error={errors.name?.message}>
          {(id, invalid) => <Input id={id} aria-invalid={invalid} {...register('name')} />}
        </Field>
        <Field label="Telefone" error={errors.phone?.message}>
          {(id, invalid) => <Input id={id} type="tel" aria-invalid={invalid} {...register('phone')} />}
        </Field>
        <Field label="E-mail (opcional)" error={errors.email?.message}>
          {(id, invalid) => <Input id={id} type="email" aria-invalid={invalid} {...register('email')} />}
        </Field>
        {save.isError && <Alert tone="error">{errorMessage(save.error)}</Alert>}
      </form>
    </Modal>
  )
}
