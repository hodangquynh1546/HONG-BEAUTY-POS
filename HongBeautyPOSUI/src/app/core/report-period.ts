import { vietnamDay } from './format';

export type ReportPeriod = 'today' | 'yesterday' | 'last7' | 'thisMonth' | 'lastMonth' | 'custom';
const asDay = (value: Date): string => value.toISOString().slice(0, 10);

/** Inclusive `from`/`to` days (Vietnam calendar) for a preset period. */
export function periodRange(period: Exclude<ReportPeriod, 'custom'>, now = new Date()): { from: string; to: string } {
  const today = vietnamDay(now);
  const date = new Date(`${today}T00:00:00Z`);
  switch (period) {
    case 'today': return { from: today, to: today };
    case 'yesterday': date.setUTCDate(date.getUTCDate() - 1); return { from: asDay(date), to: asDay(date) };
    case 'last7': date.setUTCDate(date.getUTCDate() - 6); return { from: asDay(date), to: today };
    case 'thisMonth': return { from: today.slice(0, 8) + '01', to: today };
    case 'lastMonth': {
      date.setUTCDate(0);
      const to = asDay(date);
      date.setUTCDate(1);
      return { from: asDay(date), to };
    }
  }
}
