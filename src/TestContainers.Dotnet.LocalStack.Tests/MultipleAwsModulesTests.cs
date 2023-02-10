using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace TestContainers.Dotnet.LocalStack.Tests;

public class MultipleAwsModulesTests : IAsyncLifetime
{
    private readonly LocalStackContainer localStackContainer =
        new LocalStackBuilder().WithServices(AwsService.DynamoDb, AwsService.S3).Build();

    public Task InitializeAsync()
    {
        return this.localStackContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this.localStackContainer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task CreateTableReturnsCorrectTableDescription()
    {
        // Given
        const string tableName = "TestDynamoDbTable";
        var clientConfig = new AmazonDynamoDBConfig();
        clientConfig.ServiceURL = localStackContainer.GetEndpoint();
        clientConfig.UseHttp = true;
        var client = new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"), clientConfig);

        // When
        _ = await client.CreateTableAsync(new CreateTableRequest()
            {
                TableName = tableName,
                AttributeDefinitions = new List<AttributeDefinition>()
                {
                    new AttributeDefinition("Id", ScalarAttributeType.S),
                    new AttributeDefinition("Name", ScalarAttributeType.S),
                },
                KeySchema = new List<KeySchemaElement>()
                    { new KeySchemaElement("Id", KeyType.HASH), new KeySchemaElement("Name", KeyType.RANGE), },
                ProvisionedThroughput = new ProvisionedThroughput(1, 1),
                TableClass = TableClass.STANDARD,
            })
            .ConfigureAwait(false);

        var tableDescription = await client.DescribeTableAsync(tableName).ConfigureAwait(false);

        // Then
        Assert.NotNull(tableDescription);
        Assert.Equal(HttpStatusCode.OK, tableDescription.HttpStatusCode);
        Assert.Equal(tableName, tableDescription.Table.TableName);
        Assert.Equal("Id", tableDescription.Table.KeySchema[0].AttributeName);
    }

    [Fact]
    public async Task InsertElementToTableReturnsHttpStatusCodeOk()
    {
        // Given
        var tableName = $"TestDynamoDbTable-{Guid.NewGuid():D}";
        var itemId = Guid.NewGuid().ToString("D");
        var itemName = Guid.NewGuid().ToString("D");

        var clientConfig = new AmazonDynamoDBConfig();
        clientConfig.ServiceURL = localStackContainer.GetEndpoint();
        clientConfig.UseHttp = true;
        var client = new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"), clientConfig);

        // When
        _ = await client.CreateTableAsync(new CreateTableRequest()
            {
                TableName = tableName,
                AttributeDefinitions = new List<AttributeDefinition>()
                {
                    new AttributeDefinition("Id", ScalarAttributeType.S),
                    new AttributeDefinition("Name", ScalarAttributeType.S),
                },
                KeySchema = new List<KeySchemaElement>()
                    { new KeySchemaElement("Id", KeyType.HASH), new KeySchemaElement("Name", KeyType.RANGE), },
                ProvisionedThroughput = new ProvisionedThroughput(1, 1),
                TableClass = TableClass.STANDARD,
            })
            .ConfigureAwait(false);

        _ = await client.PutItemAsync(new PutItemRequest(tableName,
                new Dictionary<string, AttributeValue>()
                {
                    { "Id", new AttributeValue() { S = itemId } }, { "Name", new AttributeValue() { S = itemName } }
                }))
            .ConfigureAwait(false);

        var getItemResponse = await client.GetItemAsync(new GetItemRequest(tableName,
                new Dictionary<string, AttributeValue>()
                {
                    { "Id", new AttributeValue() { S = itemId } }, { "Name", new AttributeValue() { S = itemName } }
                }))
            .ConfigureAwait(false);

        // Then
        Assert.Equal(HttpStatusCode.OK, getItemResponse.HttpStatusCode);
    }


    [Fact]
    public async Task ListBucketsReturnsHttpStatusCodeOk()
    {
        // Given
        var config = new AmazonS3Config();
        config.ServiceURL = localStackContainer.GetEndpoint();

        var client = new AmazonS3Client(localStackContainer.GetAccessKeyId(), localStackContainer.GetAccessSecretKey(), config);

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
        config.ServiceURL = localStackContainer.GetEndpoint();

        var client = new AmazonS3Client(localStackContainer.GetAccessKeyId(), localStackContainer.GetAccessSecretKey(), config);

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