using HTTPServer.Models;

namespace HTTPServer
{
    public enum HttpHeader
    {
        Location
    }

    public class HttpBuilder
    {
        #region Public Methods

        public static HttpResponse OK(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.OK,
            };
        }

        public static HttpResponse NotImplemented(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.NotImplemented,
            };
        }

        public static HttpResponse InternalServerError(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
            };
        }

        public static HttpResponse MovedPermanently(string url, bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.MovedPermanently,
                Headers = new Dictionary<string, string>()
                {
                    { HttpHeader.Location.ToString(), url }
                }
            };
        }

        public static HttpResponse PermanantRedirect(string url, bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.Permanent_Redirect,
                Headers = new Dictionary<string, string>()
                {
                    { HttpHeader.Location.ToString(), url }
                }
            };
        }

        public static HttpResponse Found(string url, bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.Found,
                Headers = new Dictionary<string, string>()
                {
                    { HttpHeader.Location.ToString(), url }
                }
            };
        }

        public static HttpResponse RedirectFromApacheRules(string url, int statuscode, bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = (HttpStatusCode)statuscode,
                Headers = new Dictionary<string, string>()
                {
                    { HttpHeader.Location.ToString(), url }
                }
            };
        }

        public static HttpResponse NoContent(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.No_Content,
            };
        }

        public static HttpResponse NotFound(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.Not_Found,
            };
        }

        public static HttpResponse NotAllowed(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.Forbidden,
            };
        }

        public static HttpResponse MethodNotAllowed(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.MethodNotAllowed,
            };
        }

        public static HttpResponse MissingParameters(bool KeepAlive = false)
        {
            return new HttpResponse(KeepAlive)
            {
                HttpStatusCode = HttpStatusCode.Missing_parameters,
            };
        }
        #endregion
    }
}
