import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { Alert, Button, Card, Field, Input } from '../../components/ui'
import { ApiError, errorMessage } from '../../lib/api'
import { useAuth } from '../../lib/useAuth'

const schema = z.object({
  email: z.email('Informe um e-mail válido.'),
  password: z.string().min(1, 'Informe a senha.'),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const { login, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const from = (location.state as { from?: string } | null)?.from ?? '/admin'

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } })

  if (isAuthenticated) return <Navigate to={from} replace />

  const onSubmit = async (values: FormValues) => {
    setError(null)
    try {
      await login(values.email, values.password)
      navigate(from, { replace: true })
    } catch (e) {
      setError(e instanceof ApiError && e.status === 401 ? 'E-mail ou senha incorretos.' : errorMessage(e))
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center px-4">
      <Card className="w-full max-w-sm">
        <h1 className="text-xl font-bold">✂️ BeautyAgenda</h1>
        <p className="mb-5 mt-1 text-sm text-slate-500">Acesso da equipe do salão</p>
        <form className="space-y-4" noValidate onSubmit={handleSubmit(onSubmit)}>
          <Field label="E-mail" error={errors.email?.message}>
            {(id, invalid) => <Input id={id} type="email" autoComplete="username" aria-invalid={invalid} {...register('email')} />}
          </Field>
          <Field label="Senha" error={errors.password?.message}>
            {(id, invalid) => <Input id={id} type="password" autoComplete="current-password" aria-invalid={invalid} {...register('password')} />}
          </Field>
          {error && <Alert tone="error">{error}</Alert>}
          <Button type="submit" className="w-full" loading={isSubmitting}>
            Entrar
          </Button>
        </form>
        <Link to="/" className="mt-4 block text-center text-xs text-slate-500 hover:text-slate-700">
          Ir para o agendamento público
        </Link>
      </Card>
    </div>
  )
}
