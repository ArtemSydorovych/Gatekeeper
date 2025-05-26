using Grpc.Core;

namespace Gatekeeper.Grpc.Services;

public class GreeterService(ILogger<GreeterService> logger) : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        logger.LogInformation("Hello World!");
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }
}
