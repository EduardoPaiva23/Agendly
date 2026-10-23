import { useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { useAuth } from '../lib/useAuth'

const items = [
  { to: '/admin', label: 'Painel', end: true },
  { to: '/admin/agenda', label: 'Agenda' },
  { to: '/admin/agendamentos', label: 'Agendamentos' },
  { to: '/admin/clientes', label: 'Clientes' },
  { to: '/admin/profissionais', label: 'Profissionais' },
  { to: '/admin/servicos', label: 'Serviços' },
  { to: '/admin/expediente', label: 'Expediente' },
]

export function AdminLayout() {
  const { user, isAdmin, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  const [lastPath, setLastPath] = useState(location.pathname)

  // Close the mobile menu after navigating.
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname)
    setMenuOpen(false)
  }

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `block rounded-lg px-3 py-2 text-sm font-medium ${isActive ? 'bg-rose-50 text-rose-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'}`

  const nav = (
    <nav className="space-y-1">
      {items.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.end} className={linkClass}>
          {item.label}
        </NavLink>
      ))}
    </nav>
  )

  return (
    <div className="min-h-dvh lg:flex">
      <aside className="hidden w-60 shrink-0 flex-col border-r border-slate-200 bg-white p-4 lg:flex">
        <Link to="/admin" className="mb-6 flex items-center gap-2 px-2 text-lg font-bold">
          <span aria-hidden>✂️</span> BeautyAgenda
        </Link>
        {nav}
        <UserBox name={user?.fullName} role={isAdmin ? 'Administrador' : 'Recepção'} onLogout={logout} />
      </aside>

      <header className="sticky top-0 z-30 flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3 lg:hidden">
        <Link to="/admin" className="font-bold">
          ✂️ BeautyAgenda
        </Link>
        <button
          type="button"
          className="rounded-md px-3 py-1.5 text-sm font-medium ring-1 ring-slate-300"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((v) => !v)}
        >
          Menu
        </button>
      </header>
      {menuOpen && (
        <div className="border-b border-slate-200 bg-white p-4 lg:hidden">
          {nav}
          <UserBox name={user?.fullName} role={isAdmin ? 'Administrador' : 'Recepção'} onLogout={logout} />
        </div>
      )}

      <main className="min-w-0 flex-1 px-4 py-6 sm:px-8">
        <Outlet />
      </main>
    </div>
  )
}

function UserBox({ name, role, onLogout }: { name?: string; role: string; onLogout: () => void }) {
  return (
    <div className="mt-auto border-t border-slate-200 pt-4 text-sm lg:mt-6">
      <p className="font-medium text-slate-800">{name}</p>
      <p className="text-xs text-slate-500">{role}</p>
      <button type="button" onClick={onLogout} className="mt-2 text-xs font-medium text-rose-700 hover:underline">
        Sair
      </button>
    </div>
  )
}
