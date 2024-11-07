using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    class Issue174
    {
        [Test, Category("Issue174")]
        public void ensure_NetTopologySuite_IO_SpatialLite_assembly_is_strongly_named()
        {
            AssertStronglyNamedAssembly(typeof(GaiaGeoReader));
        }

        private void AssertStronglyNamedAssembly(Type typeFromAssemblyToCheck)
        {
            Assert.That(typeFromAssemblyToCheck, Is.Not.Null, "Cannot determine assembly from null");
            var assembly = typeFromAssemblyToCheck.Assembly;
            Assert.That(assembly.FullName, Does.Not.Contain("PublicKeyToken=null"), "Strongly named assembly should have a PublicKeyToken in fully qualified name");
        }
    }
}
