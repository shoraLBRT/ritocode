using Ritocode.TestSupport;

// One PostgreSQL container for the whole assembly, started on first use and removed when the run
// ends. Each test class still gets a database of its own — see PostgresTestServer.
[assembly: AssemblyFixture(typeof(PostgresTestServer))]

// MinIO the same way, and started only by the fixtures that ask for buckets: a host that never reads
// or writes an object still starts without one.
[assembly: AssemblyFixture(typeof(MinioTestServer))]
