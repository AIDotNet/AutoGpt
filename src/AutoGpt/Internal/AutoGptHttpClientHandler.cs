using AutoGpt.Options;

namespace AutoGpt.Internal;

public class AutoGptHttpClientHandler(AutoGptOptions options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 替换url的https://api.openai.com
        request.RequestUri =
            new Uri(
                request.RequestUri!.ToString().Replace("https://api.openai.com", options.Endpoint.TrimEnd('/')));

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return response;
    }
}