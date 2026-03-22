namespace BPFL.API.Exceptions
{
    public class AppException : Exception
    {
        public int StatusCode { get;  }

        protected AppException(string message, int statusCode) :base(message) 
        {
            StatusCode = statusCode; 
        }
    }
}
