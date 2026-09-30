using Ritocode.TestSupport;

// One PostgreSQL container for the whole assembly, started on first use and removed when the run
// ends. Each test class still gets a database of its own — see PostgresTestServer.
[assembly: AssemblyFixture(typeof(PostgresTestServer))]
