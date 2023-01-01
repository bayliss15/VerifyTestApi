using System.Net.Mime;

namespace ExampleApi.Tests;

internal class TestHttpRequest
{
    public TestHttpRequest()
    {

    }

    public TestHttpRequest(string resourceName)
    {

    }

    public HttpMethod Method { get; init; }
    public string RequestUri { get; init; }
    public Dictionary<string, string> Headers { get; init; }
    public string Content { get; init; }
    public string ContentType { get; init; }

    public HttpRequestMessage ToHttpRequestMessage()
    {
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(RequestUri, UriKind.Relative),
            Method = Method
        };

        if (Content is not null)
        {
            request.Content = new StringContent(Content, System.Text.Encoding.UTF8, ContentType ?? MediaTypeNames.Application.Json);
        }

        if (Headers is not null)
        {
            Headers
                .ToList()
                .ForEach(h => request.Headers.Add(h.Key, h.Value));
        }

        return request;
    }
}
