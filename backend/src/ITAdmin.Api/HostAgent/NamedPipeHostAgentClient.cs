using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.Api.HostAgent;

public sealed class NamedPipeHostAgentClient : IHostAgentClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(20);

    public async Task<HostAgentResponse> SendAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new HostAgentUnavailableException("The ITAdmin Host Agent is available only on Windows.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                HostAgentProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            await pipe.ConnectAsync(ConnectTimeout, timeout.Token);
            await WriteFrameAsync(pipe, request.ToJson(), timeout.Token);
            var json = await ReadFrameAsync(pipe, timeout.Token);
            return HostAgentResponse.FromJson(json)
                ?? throw new HostAgentUnavailableException("The ITAdmin Host Agent returned an invalid response.");
        }
        catch (HostAgentUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
        {
            throw new HostAgentUnavailableException(
                "The ITAdmin Host Agent could not be reached on this server.",
                exception);
        }
    }

    private static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length is <= 0 or > HostAgentProtocol.MaxFrameBytes)
        {
            throw new HostAgentUnavailableException("The ITAdmin Host Agent request exceeded the protocol limit.");
        }

        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        await stream.WriteAsync(length, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = new byte[4];
        await stream.ReadExactlyAsync(length, cancellationToken);
        var size = BinaryPrimitives.ReadInt32LittleEndian(length);
        if (size is <= 0 or > HostAgentProtocol.MaxFrameBytes)
        {
            throw new HostAgentUnavailableException("The ITAdmin Host Agent response exceeded the protocol limit.");
        }

        var payload = new byte[size];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return Encoding.UTF8.GetString(payload);
    }
}
