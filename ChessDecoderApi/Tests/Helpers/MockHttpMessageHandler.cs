using System.Net;
using System.Text;

namespace ChessDecoderApi.Tests.Helpers;

/// <summary>
/// Mock HTTP message handler for testing HTTP client interactions.
/// Useful for mocking Google OAuth and other external API calls.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
    {
        _responseFunc = responseFunc;
    }

    /// <summary>
    /// Creates a handler that returns a successful response with the given JSON content.
    /// </summary>
    public static MockHttpMessageHandler CreateSuccess(string jsonContent)
    {
        return new MockHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
        });
    }

    /// <summary>
    /// Creates a handler that returns an error response with the given status code.
    /// </summary>
    public static MockHttpMessageHandler CreateError(HttpStatusCode statusCode, string message = "")
    {
        return new MockHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(message, Encoding.UTF8, "application/json")
            };
        });
    }

    /// <summary>
    /// Creates a handler that returns different responses based on the request URL.
    /// </summary>
    public static MockHttpMessageHandler CreateConditional(
        Dictionary<string, HttpResponseMessage> urlResponses)
    {
        return new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? "";
            foreach (var kvp in urlResponses)
            {
                if (url.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFunc(request));
    }
}

