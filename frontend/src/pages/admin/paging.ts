/** A page number from the address: a whole number from 1, else the first page. */
export function pageParam(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

/** The admin lists' page size: as many rows as a screen can usefully hold. */
export const ADMIN_PAGE_SIZE = 50;
