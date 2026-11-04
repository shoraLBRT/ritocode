/** The class name that gives an element its problem class's colour (styles: `.k-{class}`). */
export function classTint(problemClass: string | undefined): string {
  return problemClass === undefined ? '' : ` k-${problemClass}`;
}

/** The list with the value added, or taken out if it was there. */
export function toggled<T>(list: readonly T[], value: T): T[] {
  return list.includes(value) ? list.filter((each) => each !== value) : [...list, value];
}
