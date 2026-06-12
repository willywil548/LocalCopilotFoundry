namespace FoundrySdkServer
{
    public class StreamingChatResult : IResult
    {
        private readonly IFoundryModelService _modelService;
        private readonly ChatCompletionRequest _request;
        private readonly CancellationToken _cancellationToken;
        private readonly ILogger _logger;

        public StreamingChatResult(IFoundryModelService modelService, ChatCompletionRequest request, CancellationToken cancellationToken, ILogger logger)
        {
            _modelService = modelService;
            _request = request;
            _cancellationToken = cancellationToken;
            _logger = logger;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.Add("Cache-Control", "no-cache");
            httpContext.Response.Headers.Add("Connection", "keep-alive");

            try
            {
                await foreach (var chunk in _modelService.CompleteChatStreamAsync(_request, _cancellationToken).WithCancellation(_cancellationToken))
                {
                    var line = $"data: {System.Text.Json.JsonSerializer.Serialize(chunk)}\n\n";
                    await httpContext.Response.WriteAsync(line, _cancellationToken);
                    await httpContext.Response.Body.FlushAsync(_cancellationToken);
                }
                var endLine = "data: [DONE]\n\n";
                await httpContext.Response.WriteAsync(endLine, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - expected, no need to log as error.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming chat completion");
            }
        }
    }
}
