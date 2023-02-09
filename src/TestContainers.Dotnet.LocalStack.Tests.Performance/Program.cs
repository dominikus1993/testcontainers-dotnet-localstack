using System;
using NBench;

namespace TestContainers.Dotnet.LocalStack.Tests.Performance
{
    class Program
    {
        static int Main(string[] args)
        {
            return NBenchRunner.Run<Program>();
        }
    }
}
