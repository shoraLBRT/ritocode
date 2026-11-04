// A stand-in for GitHub's OAuth app (#39), run beside the API by playwright.config.ts. The API is
// pointed at it by configuration (Auth:GitHub:*Endpoint); the browser and the API's OAuth handler
// talk to it as they would to GitHub:
//
//   GET  /login/oauth/authorize     the browser arrives; it is sent straight back with a code, as if
//                                   the person had approved the app
//   POST /login/oauth/access_token  the API trades the code for a token — refused unless the PKCE
//                                   verifier matches the challenge the authorisation carried
//   GET  /user, /user/emails        the profile, and one primary, verified address
//
// Every authorisation is a new person, so a run never meets a learner of an earlier one.
import { createHash, randomUUID } from 'node:crypto';
import { createServer } from 'node:http';

const port = Number(process.env.FAKE_PROVIDER_PORT ?? 5299);
const codes = new Map(); // code -> { challenge, person }
const tokens = new Map(); // access token -> person
let people = 0;

function person() {
  people += 1;
  const id = Date.now() * 100 + people;
  return { id, login: `e2e-learner-${id}`, email: `e2e-learner-${id}@example.test` };
}

function json(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'application/json' });
  response.end(JSON.stringify(body));
}

async function form(request) {
  let body = '';
  for await (const chunk of request) body += chunk;
  return new URLSearchParams(body);
}

function bearer(request) {
  return tokens.get((request.headers.authorization ?? '').replace(/^Bearer /i, ''));
}

createServer(async (request, response) => {
  const url = new URL(request.url, `http://localhost:${port}`);

  if (request.method === 'GET' && url.pathname === '/health') {
    return json(response, 200, { status: 'ok' });
  }

  if (request.method === 'GET' && url.pathname === '/login/oauth/authorize') {
    const redirect = new URL(url.searchParams.get('redirect_uri'));
    const code = randomUUID();
    codes.set(code, { challenge: url.searchParams.get('code_challenge'), person: person() });
    redirect.searchParams.set('code', code);
    redirect.searchParams.set('state', url.searchParams.get('state'));
    response.writeHead(302, { Location: redirect.href });
    return response.end();
  }

  if (request.method === 'POST' && url.pathname === '/login/oauth/access_token') {
    const params = await form(request);
    const grant = codes.get(params.get('code'));
    codes.delete(params.get('code'));
    const verifier = params.get('code_verifier') ?? '';
    const challenge = createHash('sha256').update(verifier).digest('base64url');

    if (!grant || grant.challenge !== challenge) {
      return json(response, 400, { error: 'bad_verification_code' });
    }

    const token = randomUUID();
    tokens.set(token, grant.person);
    return json(response, 200, { access_token: token, token_type: 'bearer', scope: 'read:user,user:email' });
  }

  const who = bearer(request);
  if (request.method === 'GET' && url.pathname === '/user' && who) {
    return json(response, 200, { id: who.id, login: who.login, email: null });
  }
  if (request.method === 'GET' && url.pathname === '/user/emails' && who) {
    return json(response, 200, [{ email: who.email, primary: true, verified: true }]);
  }

  return json(response, 404, { message: 'Not Found' });
}).listen(port, 'localhost', () => {
  console.log(`Fake GitHub on http://localhost:${port}`);
});
