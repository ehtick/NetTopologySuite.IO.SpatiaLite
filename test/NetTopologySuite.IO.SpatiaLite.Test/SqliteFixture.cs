using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.IO;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    [TestFixture]
    [Category("Database.IO")]
    public class SqliteFixture
    {
        [SetUp]
        public virtual void OnFixtureSetUp() { }

        [TearDown]
        public virtual void OnFixtureTearDown() { }

        private static void DoTest(Action<SqliteConnection> action)
        {
            Assert.That(action, Is.Not.Null);

            string fileName = $"{Guid.NewGuid()}.gpkg";
            File.Copy("empty.gpkg", fileName);
            string cs = $"Data Source={fileName}";
            using (var conn = new SqliteConnection(cs))
            {
                conn.Open();

                conn.EnableExtensions(true);
                SpatialiteLoader.Load(conn);
                Debug.WriteLine("'mod_spatialite' extension loaded");

                action(conn);
            }
            //File.Delete(fileName);
        }

        [Test]
        public virtual void Existing_point_should_be_read()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, gpkgMakePoint(@px, @py, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", coord.X);
                    cmd.Parameters.AddWithValue("py", coord.Y);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader();
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.Not.Null);
                        Assert.That(geom, Is.InstanceOf<Point>());
                        Assert.That(geom, Is.EqualTo(geom));
                        Assert.That(point.EqualsExact(geom));
                        Assert.That(geom.Coordinate, Is.EqualTo(coord));
                        Assert.That(coord.Equals(geom.Coordinate));
                        Assert.That(double.IsNaN(geom.Coordinate.Z));
                    }
                }
            });
        }

        /// <summary>
        /// NOTE: this code fails if we configure WkbWriter with handleSRID = Ttrue
        /// see GeoPackageGeoWriter.cs => l.109 
        /// </summary>
        [Test]
        public virtual void New_point_should_be_written()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XY
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XY
                        };
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.AsBinary(), Is.EqualTo(point.AsBinary()));
                        Assert.That(!rdr.GetBoolean(1));
                        Assert.That(!rdr.GetBoolean(2));
                        string wkt = rdr.GetString(3);
                        Assert.That(!string.IsNullOrEmpty(wkt));
                        Assert.That(wkt, Is.EqualTo("POINT(11.11 22.22)"));
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointZ_should_be_read()
        {
            var coord = new CoordinateZ(11.11, 22.22, 33.33);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_z];
INSERT INTO [sample_feature_table_z] ([id], [geometry])
VALUES (1, gpkgMakePointZ(@px, @py, @pz, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", coord.X);
                    cmd.Parameters.AddWithValue("py", coord.Y);
                    cmd.Parameters.AddWithValue("pz", coord.Z);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_z]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader();
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.Not.Null);
                        Assert.That(geom, Is.InstanceOf<Point>());
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(point.EqualsExact(geom));
                        Assert.That(geom.Coordinate, Is.EqualTo(coord));
                        Assert.That(coord.Equals(geom.Coordinate));
                        Assert.That(geom.Coordinate, Is.InstanceOf<CoordinateZ>());
                        var coordZ = (CoordinateZ)geom.Coordinate;
                        Assert.That(coord.Equals3D(coordZ));
                        Assert.That(geom.Coordinate.Z, Is.EqualTo(coord.Z));
                    }
                }
            });
        }

        [Test]
        public virtual void New_pointZ_should_be_written()
        {
            var coord = new CoordinateZ(11.11, 22.22, 33.33);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_z];
INSERT INTO [sample_feature_table_z] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYZ
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);

                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_z]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XYZ
                        };
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.AsBinary(), Is.EqualTo(point.AsBinary()));
                        Assert.That(rdr.GetBoolean(1));
                        Assert.That(!rdr.GetBoolean(2));
                        string wkt = rdr.GetString(3);
                        Assert.That(!string.IsNullOrEmpty(wkt));
                        Assert.That(wkt, Is.EqualTo("POINT Z(11.11 22.22 33.33)"));
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointM_should_be_read()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 0.0 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.That(point.Z, Is.EqualTo(0.0));
            Assert.That(point.M, Is.EqualTo(44.44));

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_m];
INSERT INTO [sample_feature_table_m] ([id], [geometry])
VALUES (1, gpkgMakePointM(@px, @py, @pm, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", point.X);
                    cmd.Parameters.AddWithValue("py", point.Y);
                    cmd.Parameters.AddWithValue("pm", point.M);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_m]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating));
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.Not.Null);
                        Assert.That(geom, Is.InstanceOf<Point>());
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(point.EqualsExact(geom));
                        Assert.That(geom.Coordinate, Is.EqualTo(point.Coordinate));
                        Assert.That(point.Coordinate.Equals(geom.Coordinate));
                        Assert.That(((Point)geom).M, Is.EqualTo(point.M));
                    }
                }
            });
        }

        [Test]
        public virtual void New_pointM_should_be_written()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 0.0 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.That(point.Z, Is.EqualTo(0.0));
            Assert.That(point.M, Is.EqualTo(44.44));

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_m];
INSERT INTO [sample_feature_table_m] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYM
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_m]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating));
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.AsBinary(), Is.EqualTo(point.AsBinary()));
                        Assert.That(!rdr.GetBoolean(1));
                        Assert.That(rdr.GetBoolean(2));
                        string wkt = rdr.GetString(3);
                        Assert.That(!string.IsNullOrEmpty(wkt));
                        Assert.That(wkt, Is.EqualTo("POINT M(11.11 22.22 44.44)"));
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointZM_should_be_read()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYZM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 33.33 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.That(point.Z, Is.EqualTo(33.33));
            Assert.That(point.M, Is.EqualTo(44.44));

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_zm];
INSERT INTO [sample_feature_table_zm] ([id], [geometry])
VALUES (1, gpkgMakePointZM(@px, @py, @pz, @pm, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", point.X);
                    cmd.Parameters.AddWithValue("py", point.Y);
                    cmd.Parameters.AddWithValue("pz", point.Z);
                    cmd.Parameters.AddWithValue("pm", point.M);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_zm]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating));
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.Not.Null);
                        Assert.That(geom, Is.InstanceOf<Point>());
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(point.EqualsExact(geom));
                        Assert.That(geom.Coordinate, Is.EqualTo(point.Coordinate));
                        Assert.That(point.Coordinate.Equals(geom.Coordinate));
                        Assert.That(geom.Coordinate, Is.InstanceOf<CoordinateZM>());
                        var coordZ = (CoordinateZ)geom.Coordinate;
                        Assert.That(((CoordinateZ)point.Coordinate).Equals3D(coordZ));
                        Assert.That(((Point)geom).Z, Is.EqualTo(point.Z));
                        Assert.That(((Point)geom).M, Is.EqualTo(point.M));
                    }
                }
            });
        }

        [Test]
        public virtual void New_pointZM_should_be_written()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYZM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 33.33 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.That(point.Z, Is.EqualTo(33.33));
            Assert.That(point.M, Is.EqualTo(44.44));

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_zm];
INSERT INTO [sample_feature_table_zm] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYZM
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_zm]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating));
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.AsBinary(), Is.EqualTo(point.AsBinary()));
                        Assert.That(rdr.GetBoolean(1));
                        Assert.That(rdr.GetBoolean(2));
                        string wkt = rdr.GetString(3);
                        Assert.That(!string.IsNullOrEmpty(wkt));
                        Assert.That(wkt, Is.EqualTo("POINT ZM(11.11 22.22 33.33 44.44)"));
                    }
                }
            });
        }

        [Test]
        public virtual void Srid_should_NOT_be_read_as_default()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);
            point.SRID = 3004;

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XY
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XY
                        };
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.SRID, Is.Not.EqualTo(point.SRID));
                        Assert.That(geom.SRID, Is.EqualTo(-1));
                    }
                }
            });
        }

        [Test]
        public virtual void Srid_should_be_read_only_if_configured()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);
            point.SRID = 3004;

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XY
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.That(ret, Is.EqualTo(1));
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        Assert.That(rdr.Read());
                        byte[] blob = rdr.GetFieldValue<byte[]>(0);
                        var reader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XY,
                            HandleSRID = true
                        };
                        var geom = reader.Read(blob);
                        Assert.That(geom, Is.EqualTo(point));
                        Assert.That(geom.SRID, Is.EqualTo(point.SRID));
                    }
                }
            });
        }
    }
}
