import { Link, NavLink, Outlet } from 'react-router'

export function PublicLayout() {
  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `rounded-md px-3 py-1.5 text-sm font-medium ${isActive ? 'bg-rose-50 text-rose-700' : 'text-slate-600 hover:text-slate-900'}`

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center justify-between gap-4 px-4 py-3">
          <Link to="/" className="flex items-center gap-2 text-lg font-bold text-slate-900">
            <span aria-hidden>✂️</span> BeautyAgenda
          </Link>
          <nav className="flex gap-1">
            <NavLink to="/" end className={linkClass}>
              Agendar
            </NavLink>
            <NavLink to="/agendamento" className={linkClass}>
              Meu agendamento
            </NavLink>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-slate-200 bg-white py-4 text-center text-xs text-slate-500">
        BeautyAgenda · <Link to="/admin" className="hover:text-slate-700">Área do salão</Link>
      </footer>
    </div>
  )
}
