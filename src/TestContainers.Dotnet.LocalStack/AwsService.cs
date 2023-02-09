namespace TestContainers.Dotnet.LocalStack;

public interface IAwsService
{
    public string Name { get; }
    public ushort Port { get; }
}

public sealed class AwsService: IAwsService
{
    public string Name { get; }
    public ushort Port { get; }

    private AwsService(string name, ushort port)
    {
        Name = name;
        Port = port;
    }

    public static readonly AwsService ApiGateway = new("apigateway", 4567);
    public static readonly AwsService S3 = new("s3", 4572);
    public static readonly AwsService DynamoDB = new("dynamodb", 4569);
    public static readonly AwsService Sqs = new("sqs", 4576);
}