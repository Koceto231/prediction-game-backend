using System.Net;

namespace BPFL.API.Exceptions
{

        public class BPFLDataClientException : Exception
        {
            public HttpStatusCode StatusCode { get; }
            public BPFLDataClientException(string message, HttpStatusCode statusCode) : base(message)
            {
                StatusCode = statusCode;
            }
        }
    
}
