using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using TestContainers.Dotnet.LocalStack;
using Xunit;

namespace TestContainers.Dotnet.LocalStack;

public sealed class AmazonS3ContainerTest : IAsyncLifetime
{
    private readonly LocalStackContainer _s3Container = new LocalStackBuilder().WithServices(AwsService.S3).Build();

    public Task InitializeAsync()
    {
        return _s3Container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _s3Container.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task ListBucketsReturnsHttpStatusCodeOk()
    {
        // Given
        var config = new AmazonS3Config();
        config.ServiceURL = _s3Container.GetEndpoint();

        var client = new AmazonS3Client(_s3Container.GetAccessKeyId(), _s3Container.GetAccessSecretKey(), config);

        // When
        var buckets = await client.ListBucketsAsync()
            .ConfigureAwait(false);

        // Then
        Assert.Equal(HttpStatusCode.OK, buckets.HttpStatusCode);
    }

    [Fact]
    public async Task GetObjectReturnsPutObject()
    {
        // Given
        using var inputStream = new MemoryStream(new byte[byte.MaxValue]);

        var config = new AmazonS3Config();
        config.ServiceURL = _s3Container.GetEndpoint();

        var client = new AmazonS3Client(_s3Container.GetAccessKeyId(), _s3Container.GetAccessSecretKey(), config);

        var objectRequest = new PutObjectRequest();
        objectRequest.BucketName = Guid.NewGuid().ToString("D");
        objectRequest.Key = Guid.NewGuid().ToString("D");
        objectRequest.InputStream = inputStream;

        // When
        _ = await client.PutBucketAsync(objectRequest.BucketName)
            .ConfigureAwait(false);

        _ = await client.PutObjectAsync(objectRequest)
            .ConfigureAwait(false);

        var objectResponse = await client.GetObjectAsync(objectRequest.BucketName, objectRequest.Key)
            .ConfigureAwait(false);

        // Then
        Assert.Equal(byte.MaxValue, objectResponse.ContentLength);
    }
}