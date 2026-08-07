using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace FillInTheTextBot.Messengers.Sber.Tests
{
    /// <summary>
    /// В бою фабрику логгеров выставляет Startup. Статические мапперы берут логгер
    /// из неё в статическом конструкторе, поэтому в тестах её тоже нужно выставить —
    /// иначе логирование внутри catch падает с ArgumentNullException.
    /// </summary>
    [SetUpFixture]
    public class LoggerSetUpFixture
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            InternalLoggerFactory.Factory = NullLoggerFactory.Instance;
        }
    }
}
