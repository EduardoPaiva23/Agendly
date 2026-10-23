import { useEffect, useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { statusColors, statusLabels } from '../lib/labels'
import type { AppointmentStatus } from '../types/api'

const cx = (...classes: Array<string | false | null | undefined>) => classes.filter(Boolean).join(' ')

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'

const variants: Record<Variant, string> = {
  primary: 'bg-rose-600 text-white hover:bg-rose-700 focus-visible:outline-rose-600 disabled:bg-rose-300',
  secondary: 'bg-white text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50 disabled:text-slate-400',
  danger: 'bg-white text-rose-700 ring-1 ring-inset ring-rose-300 hover:bg-rose-50 disabled:text-rose-300',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 disabled:text-slate-300',
}

export function Button({
  variant = 'primary',
  loading = false,
  className,
  children,
  disabled,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; loading?: boolean }) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      className={cx(
        'inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition',
        'focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed',
        variants[variant],
        className,
      )}
      {...props}
    >
      {loading && <Spinner small />}
      {children}
    </button>
  )
}

const control =
  'block w-full rounded-lg border-0 bg-white px-3 py-2 text-sm text-slate-900 ring-1 ring-inset ring-slate-300 ' +
  'placeholder:text-slate-400 focus:ring-2 focus:ring-inset focus:ring-rose-500 disabled:bg-slate-50 disabled:text-slate-500 ' +
  'aria-[invalid=true]:ring-rose-400'

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cx(control, className)} {...props} />
}

export function Select({ className, children, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select className={cx(control, 'pr-8', className)} {...props}>
      {children}
    </select>
  )
}

export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea rows={3} className={cx(control, className)} {...props} />
}

/** Label + control + error message. The control receives the generated id through the render prop. */
export function Field({
  label,
  error,
  hint,
  children,
  className,
}: {
  label: string
  error?: string
  hint?: string
  children: (id: string, invalid: boolean) => ReactNode
  className?: string
}) {
  const id = useId()
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-sm font-medium text-slate-700">
        {label}
      </label>
      {children(id, Boolean(error))}
      {error ? (
        <p className="mt-1 text-xs text-rose-600" role="alert">
          {error}
        </p>
      ) : (
        hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>
      )}
    </div>
  )
}

export function Checkbox({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: ReactNode }) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
      <input type="checkbox" className="size-4 rounded border-slate-300 accent-rose-600" {...props} />
      {label}
    </label>
  )
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx('rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200', className)}>{children}</div>
}

export function PageHeader({ title, description, actions }: { title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
        {description && <div className="mt-1 text-sm text-slate-500">{description}</div>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

export function Spinner({ small = false }: { small?: boolean }) {
  return (
    <span
      role="status"
      aria-label="Carregando"
      className={cx('inline-block animate-spin rounded-full border-2 border-current border-r-transparent', small ? 'size-4' : 'size-6')}
    />
  )
}

export function Loading({ label = 'Carregando…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-12 text-slate-500">
      <Spinner />
      <span className="text-sm">{label}</span>
    </div>
  )
}

type Tone = 'error' | 'success' | 'info' | 'warning'
const tones: Record<Tone, string> = {
  error: 'bg-rose-50 text-rose-800 ring-rose-200',
  success: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  info: 'bg-sky-50 text-sky-800 ring-sky-200',
  warning: 'bg-amber-50 text-amber-900 ring-amber-200',
}

export function Alert({ tone = 'info', children, action }: { tone?: Tone; children: ReactNode; action?: ReactNode }) {
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={cx('flex items-start justify-between gap-3 rounded-lg p-3 text-sm ring-1', tones[tone])}>
      <div>{children}</div>
      {action}
    </div>
  )
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-slate-300 px-6 py-10 text-center">
      <p className="font-medium text-slate-700">{title}</p>
      {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

export function StatusBadge({ status }: { status: AppointmentStatus }) {
  return (
    <span className={cx('inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', statusColors[status].badge)}>
      {statusLabels[status]}
    </span>
  )
}

export function ActiveBadge({ active }: { active: boolean }) {
  return (
    <span
      className={cx(
        'inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        active ? 'bg-emerald-50 text-emerald-700 ring-emerald-200' : 'bg-slate-100 text-slate-500 ring-slate-200',
      )}
    >
      {active ? 'Ativo' : 'Inativo'}
    </span>
  )
}

export function Modal({
  open,
  title,
  onClose,
  children,
  footer,
  wide = false,
}: {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  wide?: boolean
}) {
  const titleId = useId()

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKey)
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = previous
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-900/40 p-0 sm:items-center sm:p-4" onMouseDown={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onMouseDown={(e) => e.stopPropagation()}
        className={cx(
          'flex max-h-[92vh] w-full flex-col rounded-t-2xl bg-white shadow-xl sm:rounded-2xl',
          wide ? 'sm:max-w-2xl' : 'sm:max-w-lg',
        )}
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 id={titleId} className="text-lg font-semibold text-slate-900">
            {title}
          </h2>
          <button type="button" onClick={onClose} className="rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600" aria-label="Fechar">
            ✕
          </button>
        </div>
        <div className="overflow-y-auto px-5 py-4">{children}</div>
        {footer && <div className="flex flex-wrap justify-end gap-2 border-t border-slate-200 px-5 py-3">{footer}</div>}
      </div>
    </div>
  )
}

export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (page: number) => void }) {
  if (totalPages <= 1) return null
  return (
    <div className="mt-4 flex items-center justify-between text-sm text-slate-600">
      <span>
        Página {page} de {totalPages}
      </span>
      <div className="flex gap-2">
        <Button variant="secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>
          Anterior
        </Button>
        <Button variant="secondary" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
          Próxima
        </Button>
      </div>
    </div>
  )
}

export function Table({ head, children }: { head: ReactNode; children: ReactNode }) {
  return (
    <div className="-mx-5 overflow-x-auto sm:mx-0">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">{head}</thead>
        <tbody className="divide-y divide-slate-100 bg-white">{children}</tbody>
      </table>
    </div>
  )
}

export const Th = ({ children, className }: { children?: ReactNode; className?: string }) => (
  <th className={cx('px-4 py-3', className)}>{children}</th>
)
export const Td = ({ children, className }: { children?: ReactNode; className?: string }) => (
  <td className={cx('px-4 py-3 align-middle text-slate-700', className)}>{children}</td>
)
