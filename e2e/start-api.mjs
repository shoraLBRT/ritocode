// The API for the end-to-end run (#39), started by playwright.config.ts — which starts its servers
// before any global setup, so the database is made ready here: created in the compose PostgreSQL if it
// is missing, then migrated. The API then runs in Development, which seeds content/ on start, with the
// development identity off and GitHub pointed at the fake provider.
import { execFileSync, spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { apiUrl, connectionString, database, databaseUser, pagesUrl, providerUrl, root } from './stack.mjs';

function run(command, args, env = process.env) {
  return execFileSync(command, args, { cwd: root, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
}

const psql = ['compose', 'exec', '-T', 'postgres', 'psql', '-U', databaseUser, '-d', 'postgres', '-tAc'];
if (run('docker', [...psql, `SELECT 1 FROM pg_database WHERE datname = '${database}'`]).trim() !== '1') {
  run('docker', [...psql, `CREATE DATABASE ${database}`]);
}

run('dotnet', ['run', '--project', 'src/Ritocode.DbMigrator'], { ...process.env, Database__ConnectionString: connectionString });

const api = spawn('dotnet', ['run', '--project', 'src/Ritocode.Api', '--no-launch-profile', '--urls', apiUrl], {
  cwd: root,
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    Database__ConnectionString: connectionString,
    Content__Seed__Directory: resolve(root, 'content'),
    Authentication__DevelopmentIdentity__Enabled: 'false',
    Auth__SignIn__AppOrigin: pagesUrl,
    Auth__GitHub__ClientId: 'e2e-client',
    Auth__GitHub__ClientSecret: 'e2e-secret',
    Auth__GitHub__AuthorizationEndpoint: `${providerUrl}/login/oauth/authorize`,
    Auth__GitHub__TokenEndpoint: `${providerUrl}/login/oauth/access_token`,
    Auth__GitHub__UserInformationEndpoint: `${providerUrl}/user`,
    Logging__LogLevel__Default: 'Warning',
  },
});

// Playwright stops this process when the run ends; the API goes with it.
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => api.kill(signal));
}
api.on('exit', (code) => process.exit(code ?? 0));
