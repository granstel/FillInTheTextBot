using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services.Mapping
{
    public static class InternalMapping
    {
        public static Response ToResponse(this Request source, Response response = null)
        {
            if (response == null)
            {
                response = new Response();
            }

            response.ChatHash = source.ChatHash;
            response.UserHash = source.UserHash;

            return response;
        }
    }
}
