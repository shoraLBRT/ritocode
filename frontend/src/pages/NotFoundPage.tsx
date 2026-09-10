import { Link } from 'react-router';

/** The client-side 404: a route this application has no page for. */
export function NotFoundPage() {
  return (
    <section className="page">
      <h1>Page not found</h1>
      <p>This address does not match any page.</p>
      <Link to="/">Go home</Link>
    </section>
  );
}
