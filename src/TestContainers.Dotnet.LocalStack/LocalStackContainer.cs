using System;
using DotNet.Testcontainers.Containers;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace TestContainers.Dotnet.LocalStack;

/// <inheritdoc cref="DockerContainer" />
[PublicAPI]
public sealed class LocalStackContainer: DockerContainer
{
    private readonly LocalstackConfiguration _configuration;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackContainer" /> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <param name="logger">The logger.</param>
    public LocalStackContainer(LocalstackConfiguration configuration, ILogger logger) : base(configuration, logger)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the Minio Url.
    /// </summary>
    /// <returns>The Minio Url.</returns>
    public string GetEndpoint()
    {
        return new UriBuilder(Uri.UriSchemeHttp, Hostname, GetMappedPublicPort(LocalStackBuilder.LocalStackPort)).ToString();
    }
    
    /// <summary>
    /// Gets the Minio Url.
    /// </summary>
    /// <returns>The Minio Url.</returns>
    public string GetDefaultRegion()
    {
        return _configuration.DefaultRegion!;
    }
    
    /// <summary>
    /// Gets the Minio Url.
    /// </summary>
    /// <returns>The Minio Url.</returns>
    public string GetAccessKeyId() => "dummy";

    /// <summary>
    /// Gets the Minio Url.
    /// </summary>
    /// <returns>The Minio Url.</returns>
    public string GetAccessSecretKey() => "dummy";
}