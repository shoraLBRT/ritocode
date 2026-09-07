using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ritocode.Shared.Storage;

public static class ObjectStorageExtensions
{
    /// <summary>
    /// Registers the object storage client and its settings. Called once from the composition root;
    /// modules take <see cref="IObjectStore"/> as a constructor parameter and never build a client
    /// of their own.
    /// </summary>
    /// <remarks>
    /// Nothing here contacts the store. Construction is offline by design, so a host — and every
    /// test that boots one — starts without object storage running, exactly as it starts without a
    /// reachable database. What a misconfiguration costs is a failed request, and what
    /// <c>ValidateOnStart</c> catches is the part that can be known without a network.
    /// </remarks>
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;

            var config = new AmazonS3Config
            {
                ForcePathStyle = options.UsePathStyleAddressing,
                AuthenticationRegion = options.Region,
            };

            if (options.ServiceUrl.Length > 0)
            {
                // Setting ServiceURL is what points the client at MinIO rather than at AWS; the
                // region is then only what the signature is computed over.
                config.ServiceURL = options.ServiceUrl;
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
            }

            return options.HasStaticCredentials
                ? new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config)
                : new AmazonS3Client(config);
        });

        services.AddSingleton<IObjectStore, S3ObjectStore>();

        return services;
    }
}
