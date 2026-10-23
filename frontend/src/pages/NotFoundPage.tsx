import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-3 px-4 text-center">
      <p className="text-5xl font-bold text-rose-600">404</p>
      <h1 className="text-xl font-semibold">Página não encontrada</h1>
      <Link to="/" className="text-sm font-medium text-rose-700 hover:underline">
        Voltar para o agendamento
      </Link>
    </div>
  )
}
