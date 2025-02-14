﻿using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    [TestFixture, SingleThreaded]
    public abstract class AbstractIOFixture
    {
        protected readonly RandomGeometryHelper RandomGeometryHelper;

        protected AbstractIOFixture()
            : this(GeometryFactory.Default)
        { }

        protected AbstractIOFixture(GeometryFactory factory)
        {
            RandomGeometryHelper = new RandomGeometryHelper(factory);
        }

        [OneTimeSetUp]
        public virtual void OnFixtureSetUp()
        {
            try
            {
                CheckAppConfigPresent();
                CreateTestStore();
            }
            catch (Exception ex)
            {
                throw new IgnoreException("Fixture setup failed", ex);
            }
        }

        [OneTimeTearDown]
        public virtual void OnFixtureTearDown() { }

        private void CheckAppConfigPresent()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetTopologySuite.IO.SpatiaLite.Test.dll");
            if (!File.Exists(path + ".config"))
                CreateAppConfig(path);
            UpdateAppConfig(path);
            ReadAppConfig(path);
        }

        private void UpdateAppConfig(string path)
        {
            var config = ConfigurationManager.OpenExeConfiguration(path);
            var appSettings = config.AppSettings.Settings;

            AddAppConfigSpecificItems(appSettings);
            config.Save(ConfigurationSaveMode.Modified);
        }

        private void CreateAppConfig(string path)
        {
            var config = ConfigurationManager.OpenExeConfiguration(path);
            var appSettings = config.AppSettings.Settings;

            appSettings.Add("PrecisionModel", "Floating");
            appSettings.Add("Ordinates", "XY");
            appSettings.Add("MinX", "-180");
            appSettings.Add("MaxX", "180");
            appSettings.Add("MinY", "-90");
            appSettings.Add("MaxY", "90");
            appSettings.Add("Srid", "4326");

            config.Save(ConfigurationSaveMode.Modified);
        }

        protected abstract void AddAppConfigSpecificItems(KeyValueConfigurationCollection kvcc);

        private void ReadAppConfig(string path)
        {
            var config = ConfigurationManager.OpenExeConfiguration(path);
            var kvcc = config.AppSettings.Settings;
            SRID = int.Parse(kvcc["Srid"].Value);
            string pm = kvcc["PrecisionModel"].Value;
            PrecisionModel = int.TryParse(pm, out int scale)
                ? new PrecisionModel(scale)
                : new PrecisionModel((PrecisionModels)Enum.Parse(typeof(PrecisionModels), pm));
            MinX = double.Parse(kvcc["MinX"].Value, NumberFormatInfo.InvariantInfo);
            MaxX = double.Parse(kvcc["MaxX"].Value, NumberFormatInfo.InvariantInfo);
            MinY = double.Parse(kvcc["MinY"].Value, NumberFormatInfo.InvariantInfo);
            MaxY = double.Parse(kvcc["MaxY"].Value, NumberFormatInfo.InvariantInfo);
            string ordinatesString = kvcc["Ordinates"].Value;
            var ordinates = (Ordinates)Enum.Parse(typeof(Ordinates), ordinatesString);
            RandomGeometryHelper.Ordinates = ordinates;

            ReadAppConfigInternal(kvcc);
        }

        protected virtual void ReadAppConfigInternal(KeyValueConfigurationCollection kvcc) { }

        public string ConnectionString { get; protected set; }

        public int SRID
        {
            get
            {
                return RandomGeometryHelper.Factory.SRID;
            }
            protected set
            {
                if (RandomGeometryHelper == null || RandomGeometryHelper.Factory == null)
                    throw new InvalidOperationException();

                var oldPM = RandomGeometryHelper.Factory.PrecisionModel;
                RandomGeometryHelper.Factory = RandomGeometryHelper.Factory is GeometryFactoryEx
                    ? new GeometryFactoryEx(oldPM, value) 
                        { OrientationOfExteriorRing = LinearRingOrientation.CCW }
                    : new GeometryFactory(oldPM, value);
            }
        }

        public PrecisionModel PrecisionModel
        {
            get
            {
                return RandomGeometryHelper.Factory.PrecisionModel;
            }
            protected set
            {
                if (value == null)
                    return;

                if (value == PrecisionModel)
                    return;

                var factory = RandomGeometryHelper.Factory;
                int oldSrid = factory?.SRID ?? 0;
                var oldFactory = factory != null
                                     ? factory.CoordinateSequenceFactory
                                     : CoordinateArraySequenceFactory.Instance;

                if (RandomGeometryHelper.Factory is GeometryFactoryEx)
                    RandomGeometryHelper.Factory = new GeometryFactoryEx(value, oldSrid, oldFactory) 
                        {OrientationOfExteriorRing = LinearRingOrientation.CCW };
                else
                    RandomGeometryHelper.Factory = new GeometryFactory(value, oldSrid, oldFactory);
            }
        }

        public double MinX
        {
            get { return RandomGeometryHelper.MinX; }
            protected set { RandomGeometryHelper.MinX = value; }
        }

        public double MaxX
        {
            get { return RandomGeometryHelper.MaxX; }
            protected set { RandomGeometryHelper.MaxX = value; }
        }

        public double MinY
        {
            get { return RandomGeometryHelper.MinY; }
            protected set { RandomGeometryHelper.MinY = value; }
        }

        public double MaxY
        {
            get { return RandomGeometryHelper.MaxY; }
            protected set { RandomGeometryHelper.MaxY = value; }
        }

        public Ordinates InputOrdinates
        {
            get { return RandomGeometryHelper.Ordinates; }
            set
            {
                Debug.Assert((value & Ordinates.XY) == Ordinates.XY);
                RandomGeometryHelper.Ordinates = value;
            }
        }

        private Ordinates _clipOrdinates = Ordinates.AllOrdinates;
        public Ordinates ClipOrdinates
        {
            get => _clipOrdinates;
            set
            {
                Debug.Assert((value & Ordinates.XY) == Ordinates.XY);
                _clipOrdinates = value;
            }
        }

        /// <summary>
        /// Function to create the test table and add some data
        /// </summary>
        protected abstract void CreateTestStore();

        public void PerformTest(Geometry gIn)
        {
            var writer = new WKTWriter(4) { MaxCoordinatesPerLine = 3, };
            byte[] b = null;
            Assert.DoesNotThrow(() => b = Write(gIn), "Threw exception during write:\n{0}", writer.WriteFormatted(gIn));

            Geometry gParsed = null;
            Assert.DoesNotThrow(() => gParsed = Read(b), "Threw exception during read:\n{0}", writer.WriteFormatted(gIn));

            Assert.That(gParsed, Is.Not.Null, $"Could not be parsed\n{gIn}");
            CheckEquality(gIn, gParsed, writer);
        }

        protected virtual void CheckEquality(Geometry gIn, Geometry gParsed, WKTWriter writer)
        {
            Assert.That(gIn.EqualsExact(gParsed), Is.True, $"Instances are not equal\n{gIn}\n\n{gParsed}");
        }

        protected abstract Geometry Read(byte[] b);

        protected abstract byte[] Write(Geometry gIn);

        [Test]
        public virtual void TestPoint()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.Point);
        }
        [Test]
        public virtual void TestLineString()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.LineString);
        }
        [Test]
        public virtual void TestPolygon()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.Polygon);
        }
        [Test]
        public virtual void TestMultiPoint()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.MultiPoint);
        }
        [Test]
        public virtual void TestMultiLineString()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.MultiLineString);
        }

        [Test]
        public virtual void TestMultiPolygon()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.MultiPolygon);
        }

        [Test]

        public virtual void TestGeometryCollection()
        {
            for (int i = 0; i < 5; i++)
                PerformTest(RandomGeometryHelper.GeometryCollection);
        }
    }
}
