using Ritocode.TestSupport;

// One PostgreSQL container and one MinIO container for the whole assembly, each started on first
// use and removed when the run ends. Every test class still gets a database and a bucket set of its
// own — see PostgresTestServer and MinioTestServer.
//
// The manifest and package tests need neither and start neither: a container is created the first
// time something asks for a database or a bucket, so the format suite still runs without Docker.
[assembly: AssemblyFixture(typeof(PostgresTestServer))]
[assembly: AssemblyFixture(typeof(MinioTestServer))]
