using System.Net;

namespace Octockup.Server.Exceptions
{
    public class WebApiException : Exception
    {
        public string? Error { get; }
        public HttpStatusCode StatusCode { get; }

        public WebApiException(HttpStatusCode statusCode, string? error = null)
        {
            if (statusCode == HttpStatusCode.OK)
            {
                throw new ArgumentException(nameof(WebApiException) + " cannot be created with status code " + HttpStatusCode.OK);
            }
            StatusCode = statusCode;
            Error = error;
        }
    }
}
