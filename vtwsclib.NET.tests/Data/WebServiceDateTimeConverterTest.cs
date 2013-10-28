using Vtiger.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Vtiger.Data.Tests
{
    
    
    /// <summary>
    ///This is a test class for WebServiceDateTimeConverterTest and is intended
    ///to contain all WebServiceDateTimeConverterTest Unit Tests
    ///</summary>
    [TestClass()]
    public class WebServiceDateTimeConverterTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        [TestMethod()]
        [DeploymentItem("vtwsclib.NET.dll")]
        public void ToUnixTimeTest()
        {
            WebServiceDateTimeConverter_Accessor target = new WebServiceDateTimeConverter_Accessor();
            DateTime dateTime = new DateTime(1976, 7, 4, 0, 0, 0, DateTimeKind.Utc);
            int expected = 205286400;
            int actual = target.ToUnixTime(dateTime);
            Assert.AreEqual(expected, actual);
        }
    }
}
