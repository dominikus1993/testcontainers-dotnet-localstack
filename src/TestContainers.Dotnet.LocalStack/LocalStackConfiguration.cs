using System.Collections.Generic;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using JetBrains.Annotations;

namespace TestContainers.Dotnet.LocalStack;

/// <inheritdoc cref="ContainerConfiguration" />
[PublicAPI]
public sealed class LocalstackConfiguration : ContainerConfiguration
{
    
    public IEnumerable<IAwsService>? Services { get; }
    public string? DefaultRegion { get; }
    public string? ExternalServicePortStart { get; }
    public string? ExternalServicePortEnd { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalstackConfiguration" /> class.
    /// </summary>
    /// <param name="services">The LocalStack services list.</param>
    /// <param name="defaultRegion">The LocalStack services list.</param>
    /// <param name="externalServicePortStart">The LocalStack services list.</param>
    /// <param name="externalServicePortEnd">The LocalStack services list.</param>
    public LocalstackConfiguration(IEnumerable<IAwsService>? services = null, string? defaultRegion = null, string? externalServicePortStart = null, string? externalServicePortEnd = null) : base()
    {
        Services = services;
        DefaultRegion = defaultRegion;
        ExternalServicePortStart = externalServicePortStart;
        ExternalServicePortEnd = externalServicePortEnd;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalstackConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public LocalstackConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalstackConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public LocalstackConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalstackConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public LocalstackConfiguration(LocalstackConfiguration resourceConfiguration)
        : this(new LocalstackConfiguration(), resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalstackConfiguration" /> class.
    /// </summary>
    /// <param name="oldValue">The old Docker resource configuration.</param>
    /// <param name="newValue">The new Docker resource configuration.</param>
    public LocalstackConfiguration(LocalstackConfiguration oldValue, LocalstackConfiguration newValue)
        : base(oldValue, newValue)
    {
        Services = BuildConfiguration.Combine(oldValue.Services, newValue.Services);
        DefaultRegion = BuildConfiguration.Combine(oldValue.DefaultRegion, newValue.DefaultRegion);
        ExternalServicePortStart = BuildConfiguration.Combine(oldValue.ExternalServicePortStart, newValue.ExternalServicePortStart);
        ExternalServicePortEnd = BuildConfiguration.Combine(oldValue.ExternalServicePortEnd, newValue.ExternalServicePortEnd);
    }
}