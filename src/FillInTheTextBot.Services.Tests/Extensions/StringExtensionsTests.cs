using FillInTheTextBot.Services.Extensions;
using AutoFixture;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FillInTheTextBot.Services.Tests.Extensions
{
    [TestFixture]
    public class StringExtensionsTests
    {
        private readonly Fixture _fixture = new Fixture();

        #region Sanitize

        [Test]
        public void Sanitize_Null_Null()
        {
            string expected = null;


            // ReSharper disable once ExpressionIsAlwaysNull
            var result = expected.Sanitize();


            ClassicAssert.Null(result);
        }

        [Test]
        public void Sanitize_Empty_Empty()
        {
            var expected = string.Empty;


            var result = expected.Sanitize();


            ClassicAssert.True(string.IsNullOrEmpty(result));
        }
                    
        [Test]      
        public void Sanitize_AnyString_Same()
        {
            var expected = _fixture.Create<string>();


            var result = expected.Sanitize();


            ClassicAssert.AreEqual(expected, result);
        }
                    
        [Test]      
        public void Sanitize_QuotesAtAnswer_Success()
        {
            var tested = "This text is with &quot;quotes&quot;";


            var result = tested.Sanitize();


            var expected = "This text is with \"quotes\"";
            ClassicAssert.AreEqual(expected, result);
        }

        #endregion Sanitize
    }
}
