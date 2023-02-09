using System.Linq;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using JetBrains.Annotations;

namespace TestContainers.Dotnet.LocalStack;

/// <inheritdoc cref="ContainerBuilder" />
[PublicAPI]
public sealed class LocalStackBuilder : ContainerBuilder<LocalStackBuilder, LocalStackContainer, LocalstackConfiguration>
{
    public const ushort LocalStackPort = 4566;
    public const string LocalStackImage = "localstack/localstack:1.2.0";

    protected override LocalstackConfiguration DockerResourceConfiguration { get; }


    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackBuilder" /> class.
    /// </summary>
    public LocalStackBuilder()
        : this(new LocalstackConfiguration())
    {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackBuilder" /> class.
    /// </summary>
    /// <param name="dockerResourceConfiguration">The Docker resource configuration.</param>
    private LocalStackBuilder(LocalstackConfiguration dockerResourceConfiguration) : base(dockerResourceConfiguration)
    {
        DockerResourceConfiguration = dockerResourceConfiguration;
    }

    /// <summary>
    /// Sets the Minio username.
    /// </summary>
    /// <param name="services">The LocalStack services.</param>
    /// <returns>A configured instance of <see cref="LocalStackBuilder" />.</returns>
    public LocalStackBuilder WithServices(params IAwsService[] services)
    {
        if (services is null or { Length: 0})
        {
            return this;
        }

        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(services: services))
            .WithEnvironment("SERVICES", string.Join(',', services.Select(service => service.Name)))
            .WithExposedPorts(services);
    }
    
    /// <summary>
    /// Sets the Minio username.
    /// </summary>
    /// <param name="services">The LocalStack services.</param>
    /// <returns>A configured instance of <see cref="LocalStackBuilder" />.</returns>
    private LocalStackBuilder WithExposedPorts(IAwsService[] services)
    {
        return services.Aggregate(this, (builder, service) => builder.WithExposedPort(service.Port));
    } 
    
    /// <summary>
    /// Sets the Minio username.
    /// </summary>
    /// <param name="defaultRegion">The LocalStack Default Region.</param>
    /// <returns>A configured instance of <see cref="LocalStackBuilder" />.</returns>
    public LocalStackBuilder WithDefaultRegion(string defaultRegion)
    {
        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(defaultRegion: defaultRegion))
            .WithEnvironment("DEFAULT_REGION", defaultRegion);
    }
    
    /// <summary>
    /// Sets the Minio username.
    /// </summary>
    /// <param name="port">The LocalStack Default Region.</param>
    /// <returns>A configured instance of <see cref="LocalStackBuilder" />.</returns>
    public LocalStackBuilder WithExternalServicePortStart(string port)
    {
        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(externalServicePortStart: port))
            .WithEnvironment("EXTERNAL_SERVICE_PORTS_START", port);
    }
    
    /// <summary>
    /// Sets the Minio username.
    /// </summary>
    /// <param name="port">The LocalStack Default Region.</param>
    /// <returns>A configured instance of <see cref="LocalStackBuilder" />.</returns>
    public LocalStackBuilder WithExternalServicePortEnd(string port)
    {
        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(externalServicePortEnd: port))
            .WithEnvironment("EXTERNAL_SERVICE_PORTS_END", port);
    }

    protected override LocalStackBuilder Init()
    {
        return base.Init()
            .WithImage(LocalStackImage)
            .WithExternalServicePortStart("4510")
            .WithExternalServicePortEnd("4559")
            .WithEnvironment("USE_SSL", "false")
            .WithDefaultRegion("eu-west-1")
            .WithServices()
            .WithPortBinding(LocalStackPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new UntilReady()));
    }


    public override LocalStackContainer Build()
    {
        Validate();
        return new LocalStackContainer(DockerResourceConfiguration, TestcontainersSettings.Logger);
    }


    protected override void Validate()
    {
        base.Validate();

        _ = Guard.Argument(DockerResourceConfiguration.Services, nameof(DockerResourceConfiguration.Services))
            .NotNull();
    }

    protected override LocalStackBuilder Merge(LocalstackConfiguration oldValue, LocalstackConfiguration newValue)
    {
        return new LocalStackBuilder(new LocalstackConfiguration(oldValue, newValue));
    }

    protected override LocalStackBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(resourceConfiguration));
    }

    protected override LocalStackBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new LocalstackConfiguration(resourceConfiguration));
    }
    
    private sealed class UntilReady : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            var (stdout, _) = await container.GetLogs()
                .ConfigureAwait(false);
            return stdout != null && stdout.Contains("Ready.\n");
        }
    }
}
