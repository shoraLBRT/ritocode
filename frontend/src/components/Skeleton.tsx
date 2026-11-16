/** A placeholder block while content loads (docs/DESIGN.md › Motion); hidden from assistive tech. */
export function Skeleton({
  width,
  height,
  className,
}: {
  width?: number | string;
  height?: number | string;
  className?: string;
}) {
  return (
    <span
      className={className ? `rc-skeleton ${className}` : 'rc-skeleton'}
      style={{ width, height }}
      aria-hidden="true"
    />
  );
}
