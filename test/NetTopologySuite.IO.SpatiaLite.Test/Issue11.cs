using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.IO.SpatiaLite.Test;

class Issue11 
{
        [TestCase("POINT EMPTY", 17)]
        [TestCase("POINT(10 10)", 1)]
        [TestCase("POINT Z(10 10 1)", 1)]
        [TestCase("POINT M(10 10 2)", 1)]
        [TestCase("POINT ZM(10 10 1 2)", 1)]
        [TestCase("POINT(10 10)", 1)]
        [TestCase("LINESTRING EMPTY", 17)]
        [TestCase("LINESTRING(10 10, 10 20)", 3)]
        [TestCase("LINESTRING Z(10 10 1, 10 20 1)", 3)]
        [TestCase("LINESTRING M(10 10 2, 10 20 2)", 3)]
        [TestCase("LINESTRING ZM(10 10 1 2, 10 20 1 2)", 3)]
        public void TestHeaderFlags(string wkt, byte expectedFlags) 
        {
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory();
            var wktReader = new WKTReader() { IsOldNtsCoordinateSyntaxAllowed = false };
            var geom = wktReader.Read(wkt);
            var writer = new GeoPackageGeoWriter
            {
                HandleOrdinates = Ordinates.None
            };
            byte[] s = writer.Write(geom);
            
            Assert.That(s[3], Is.EqualTo(expectedFlags));

            var reader = new GeoPackageGeoReader(factory.CoordinateSequenceFactory, factory.PrecisionModel);
            var dgeom = reader.Read(s);
            Assert.That(dgeom, Is.EqualTo(geom));
        }

}