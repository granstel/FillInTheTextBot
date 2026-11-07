using System;
using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class RotationCampaignTests
    {
        [TestCase("03-01", "03-14", "2027-03-01", true)]
        [TestCase("03-01", "03-14", "2031-03-14", true)]
        [TestCase("03-01", "03-14", "2027-02-28", false)]
        [TestCase("03-01", "03-14", "2027-03-15", false)]
        [TestCase("12-20", "01-10", "2026-12-31", true)]
        [TestCase("12-20", "01-10", "2027-01-10", true)]
        [TestCase("12-20", "01-10", "2027-01-11", false)]
        [TestCase("12-20", "01-10", "2026-12-19", false)]
        [TestCase("02-29", "02-29", "2028-02-29", true)]
        [TestCase("2027-03-01", "2027-03-14", "2027-03-14", true)]
        [TestCase("2027-03-01", "2027-03-14", "2028-03-05", false)]
        [TestCase("13-01", "13-02", "2027-01-01", false)]
        public void IsActive_Today_InsideInclusivePeriod(string from, string to, string today, bool expected)
        {
            var campaign = new RotationCampaign { From = from, To = to };


            var isActive = campaign.IsActive(DateTime.Parse(today));


            Assert.That(isActive, Is.EqualTo(expected));
        }
    }
}
