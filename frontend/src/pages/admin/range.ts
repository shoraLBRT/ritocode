/** The ranges the overview's pickers offer, in days (#304, #306); usage over 90 is read by week so the bars stay readable. */
export const RANGES = ['7', '30', '90'] as const;
export type Range = (typeof RANGES)[number];

/** A UTC date as `yyyy-MM-dd`, `days` before today. */
export function utcDate(daysAgo: number, now = new Date()): string {
  const date = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - daysAgo));
  return date.toISOString().slice(0, 10);
}
