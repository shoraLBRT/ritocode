using Ritocode.TestSupport;

// One MinIO container for the whole assembly, started on first use and removed when the run ends.
// Each test class still gets a bucket per role of its own — see MinioTestServer.
[assembly: AssemblyFixture(typeof(MinioTestServer))]
