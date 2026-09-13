using Ritocode.TestSupport;

// One PostgreSQL container and one MinIO container for the whole assembly, each started on first use
// and removed when the run ends. The domain tests ask for neither, so they still run without Docker.
[assembly: AssemblyFixture(typeof(PostgresTestServer))]
[assembly: AssemblyFixture(typeof(MinioTestServer))]
