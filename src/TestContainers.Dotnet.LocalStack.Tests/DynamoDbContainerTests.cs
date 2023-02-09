using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using TestContainers.Dotnet.LocalStack;
using Xunit;

namespace Testcontainers.DynamoDB;

public sealed class MinioContainerTest : IAsyncLifetime
{
  private readonly LocalStackContainer dynamoDbContainer = new LocalStackBuilder().WithServices(AwsService.DynamoDB).Build();

  public Task InitializeAsync()
  {
    return this.dynamoDbContainer.StartAsync();
  }

  public Task DisposeAsync()
  {
    return this.dynamoDbContainer.DisposeAsync().AsTask();
  }

  [Fact]
  public async Task CreateTableReturnsCorrectTableDescription()
  {
    // Given
    const string tableName = "TestDynamoDbTable";
    var clientConfig = new AmazonDynamoDBConfig();
    clientConfig.ServiceURL = dynamoDbContainer.GetEndpoint();
    clientConfig.UseHttp = true;
    var client =  new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"), clientConfig);

    // When
    _ = await client.CreateTableAsync(new CreateTableRequest()
      {
        TableName = tableName,
        AttributeDefinitions = new List<AttributeDefinition>() { new AttributeDefinition("Id", ScalarAttributeType.S), new AttributeDefinition("Name", ScalarAttributeType.S), },
        KeySchema = new List<KeySchemaElement>() { new KeySchemaElement("Id", KeyType.HASH), new KeySchemaElement("Name", KeyType.RANGE), },
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
    clientConfig.ServiceURL = dynamoDbContainer.GetEndpoint();
    clientConfig.UseHttp = true;
    var client =  new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"), clientConfig);

    // When
    _ = await client.CreateTableAsync(new CreateTableRequest()
      {
        TableName = tableName,
        AttributeDefinitions = new List<AttributeDefinition>() { new AttributeDefinition("Id", ScalarAttributeType.S), new AttributeDefinition("Name", ScalarAttributeType.S), },
        KeySchema = new List<KeySchemaElement>() { new KeySchemaElement("Id", KeyType.HASH), new KeySchemaElement("Name", KeyType.RANGE), },
        ProvisionedThroughput = new ProvisionedThroughput(1, 1),
        TableClass = TableClass.STANDARD,
      })
      .ConfigureAwait(false);

    _ = await client.PutItemAsync(new PutItemRequest(tableName, new Dictionary<string, AttributeValue>() { { "Id", new AttributeValue() { S = itemId } }, { "Name", new AttributeValue() { S = itemName } } })).ConfigureAwait(false);

    var getItemResponse = await client.GetItemAsync(new GetItemRequest(tableName, new Dictionary<string, AttributeValue>() { { "Id", new AttributeValue() { S = itemId } }, { "Name", new AttributeValue() { S = itemName } } }))
      .ConfigureAwait(false);

    // Then
    Assert.Equal(HttpStatusCode.OK, getItemResponse.HttpStatusCode);
  }
}
