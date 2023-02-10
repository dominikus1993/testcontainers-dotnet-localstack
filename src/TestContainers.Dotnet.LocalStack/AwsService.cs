namespace TestContainers.Dotnet.LocalStack;

public interface IAwsService
{
    public string Name { get; }
}

public sealed class AwsService: IAwsService
{
    public string Name { get; }

    private AwsService(string name)
    {
        Name = name;
    }

    public static AwsService Custom(string name) => new(name);
    public static readonly AwsService ApiGateway = new("apigateway");
    public static readonly AwsService EC2 = new AwsService("ec2");
    public static readonly AwsService Kinesis = new AwsService("kinesis");
    public static readonly AwsService DynamoDb = new("dynamodb");
    public static readonly AwsService DynamoDbStreams = new("dynamodbstreams");
    public static readonly AwsService S3 = new("s3");
    public static readonly AwsService Firehose = new("firehose");
    public static readonly AwsService Sqs = new("sqs");
}