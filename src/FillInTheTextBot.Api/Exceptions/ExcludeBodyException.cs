using System;

namespace FillInTheTextBot.Api.Exceptions
{
    public class ExcludeBodyException : Exception
    {
        public ExcludeBodyException()
        {
        }

        public ExcludeBodyException(string message) : base(message)
        {
        }

        public ExcludeBodyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
