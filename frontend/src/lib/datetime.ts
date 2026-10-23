/** All dates shown and typed in the UI are in the salon's time zone, regardless of the visitor's machine. */
export const SALON_TZ = 'America/Sao_Paulo'

const fmt = (options: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat('pt-BR', { timeZone: SALON_TZ, ...options })

const timeFmt = fmt({ hour: '2-digit', minute: '2-digit' })
const dateFmt = fmt({ day: '2-digit', month: '2-digit', year: 'numeric' })
const longDateFmt = fmt({ weekday: 'long', day: 'numeric', month: 'long' })
const shortDayFmt = fmt({ weekday: 'short', day: '2-digit', month: '2-digit' })
const isoPartsFmt = new Intl.DateTimeFormat('en-CA', {
  timeZone: SALON_TZ,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

export const formatTime = (iso: string) => timeFmt.format(new Date(iso))
export const formatDate = (iso: string) => dateFmt.format(new Date(iso))
export const formatDateTime = (iso: string) => `${formatDate(iso)} às ${formatTime(iso)}`
export const formatRange = (start: string, end: string) => `${formatDateTime(start)} – ${formatTime(end)}`

/** "yyyy-MM-dd" date as a calendar day (noon UTC avoids day shifts). */
const dayOnly = (date: string) => new Date(`${date}T12:00:00Z`)
const dayFmtUtc = (options: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat('pt-BR', { timeZone: 'UTC', ...options })
const longDayFmtUtc = dayFmtUtc({ weekday: 'long', day: 'numeric', month: 'long' })
const shortDayFmtUtc = dayFmtUtc({ weekday: 'short', day: '2-digit', month: '2-digit' })

export const formatDay = (date: string) => longDayFmtUtc.format(dayOnly(date))
export const formatShortDay = (date: string) => shortDayFmtUtc.format(dayOnly(date))
export const formatLongDate = (iso: string) => longDateFmt.format(new Date(iso))
export const formatShortDate = (iso: string) => shortDayFmt.format(new Date(iso))

/** Salon-local date and time parts of an instant. */
export function salonParts(iso: string | Date): { date: string; time: string } {
  const parts = Object.fromEntries(isoPartsFmt.formatToParts(new Date(iso)).map((p) => [p.type, p.value]))
  return { date: `${parts.year}-${parts.month}-${parts.day}`, time: `${parts.hour}:${parts.minute}` }
}

export const salonToday = () => salonParts(new Date()).date

export function addDays(date: string, days: number): string {
  const d = dayOnly(date)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

function offsetMinutesAt(instant: Date): number {
  const name =
    new Intl.DateTimeFormat('en-US', { timeZone: SALON_TZ, timeZoneName: 'longOffset' })
      .formatToParts(instant)
      .find((p) => p.type === 'timeZoneName')?.value ?? 'GMT'
  const match = /GMT([+-])(\d{2}):(\d{2})/.exec(name)
  if (!match) return 0
  return (match[1] === '-' ? -1 : 1) * (Number(match[2]) * 60 + Number(match[3]))
}

/** Salon-local date + "HH:mm" -> ISO 8601 with the salon offset, e.g. 2026-11-03T14:00:00-03:00. */
export function toSalonIso(date: string, time: string): string {
  const guess = new Date(`${date}T${time}:00Z`)
  const offset = offsetMinutesAt(new Date(guess.getTime() - offsetMinutesAt(guess) * 60_000))
  const sign = offset < 0 ? '-' : '+'
  const abs = Math.abs(offset)
  const hh = String(Math.floor(abs / 60)).padStart(2, '0')
  const mm = String(abs % 60).padStart(2, '0')
  return `${date}T${time}:00${sign}${hh}:${mm}`
}

export const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h === 0) return `${m} min`
  return m === 0 ? `${h} h` : `${h} h ${m} min`
}

export function formatPhone(digits: string): string {
  const d = digits.replace(/\D/g, '')
  if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`
  if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`
  return digits
}
