using System.Collections.Generic;
using FillInTheTextBot.Services.Extensions;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Extensions
{
    [TestFixture]
    public class EnumerableExtensionsTests
    {
        [Test]
        public void JoinToString_Items_DefaultSeparator()
        {
            var source = new[] { "a", "b", "c" };


            var result = source.JoinToString();


            Assert.AreEqual("a, b, c", result);
        }

        [Test]
        public void JoinToString_ItemsWithCustomSeparator_Joined()
        {
            var source = new[] { "a", "b" };


            var result = source.JoinToString("|");


            Assert.AreEqual("a|b", result);
        }

        [Test]
        public void JoinToString_Null_Null()
        {
            IEnumerable<string> source = null;


            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.JoinToString();


            Assert.Null(result);
        }

        [Test]
        public void JoinToString_Empty_EmptyString()
        {
            var source = new string[0];


            var result = source.JoinToString();


            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void JoinToString_NotStringItems_ToStringUsed()
        {
            var source = new[] { 1, 2, 3 };


            var result = source.JoinToString();


            Assert.AreEqual("1, 2, 3", result);
        }
    }
}
