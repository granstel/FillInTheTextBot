using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using Grpc.Core;
using Grpc.Net.Client;
using StoryDialog.Contracts.V1;
using RpcDialog = StoryDialog.Contracts.V1.Dialog;

namespace FillInTheTextBot.Services.StoryDialog;

public sealed class GrpcStoryDialogClient : IStoryDialogClient, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly RpcDialog.DialogClient _client;
    private readonly StoryDialogConfiguration _configuration;

    public GrpcStoryDialogClient(StoryDialogConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0
            || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/")
        {
            throw new InvalidOperationException("StoryDialog Endpoint must be an HTTP(S) origin without credentials or a path.");
        }

        if (configuration.TimeoutMilliseconds is < 100 or > 3000)
        {
            throw new InvalidOperationException("StoryDialog TimeoutMilliseconds must be between 100 and 3000.");
        }

        _configuration = configuration;
        _channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
            MaxReceiveMessageSize = 256 * 1024
        });
        _client = new RpcDialog.DialogClient(_channel);
    }

    public async Task<ListStoriesResponse> ListStoriesAsync(CancellationToken cancellationToken)
    {
        using var call = _client.ListStoriesAsync(new ListStoriesRequest(), Options(cancellationToken));
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return response;
    }

    public async Task<Reply> TurnAsync(TurnRequest request, CancellationToken cancellationToken)
    {
        using var call = _client.TurnAsync(request, Options(cancellationToken));
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return response;
    }

    public async Task<Reply> GetConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var request = new GetConversationRequest { ConversationId = conversationId };
        using var call = _client.GetConversationAsync(request, Options(cancellationToken));
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return response;
    }

    private CallOptions Options(CancellationToken cancellationToken)
    {
        var headers = new Metadata();
        if (!string.IsNullOrEmpty(_configuration.ApiKey))
        {
            headers.Add("authorization", "Bearer " + _configuration.ApiKey);
        }
        var deadline = DateTime.UtcNow.AddMilliseconds(_configuration.TimeoutMilliseconds);
        var options = new CallOptions(headers, deadline, cancellationToken);
        return options;
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
