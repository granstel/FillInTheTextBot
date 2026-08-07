using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Extensions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FillInTheTextBot.Services.Tests.Extensions
{
    [TestFixture]
    public class SerializationExtensionsTests
    {
        [Test]
        public void Deserialize_JsonString_Object()
        {
            object serialized = "{\"Text\":\"Привет\",\"IsQuickReply\":true}";


            var result = serialized.Deserialize<Button>();


            ClassicAssert.AreEqual("Привет", result.Text);
            ClassicAssert.True(result.IsQuickReply);
        }

        [Test]
        public void Deserialize_AlreadyTargetType_SameInstance()
        {
            object source = new Button { Text = "Привет" };


            var result = source.Deserialize<Button>();


            ClassicAssert.AreSame(source, result);
        }

        [Test]
        public void Deserialize_UnsupportedType_Default()
        {
            object source = 42;


            var result = source.Deserialize<Button>();


            ClassicAssert.Null(result);
        }

        [Test]
        public void Deserialize_Null_Default()
        {
            object source = null;


            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.Deserialize<Button>();


            ClassicAssert.Null(result);
        }

        [Test]
        public void Deserialize_StringToString_SameString()
        {
            object source = "просто строка";


            var result = source.Deserialize<string>();


            ClassicAssert.AreEqual("просто строка", result);
        }
    }
}
