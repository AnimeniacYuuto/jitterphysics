using System;
using System.Collections.Generic;
using System.Text;

namespace Jitter.Dynamics
{

    // TODO: Check values, Documenation
    // Maybe some default materials, aka Material.Soft?
    public class Material
    {

        internal double kineticFriction = 0.3f;
        internal double staticFriction = 0.6f;
        internal double restitution = 0.0f;

        public Material() { }

        public double Restitution
        {
            get { return restitution; }
            set { restitution = value; }
        }

        public double StaticFriction
        {
            get { return staticFriction; }
            set { staticFriction = value; }
        }

        public double KineticFriction
        {
            get { return kineticFriction; }
            set { kineticFriction = value; }
        }

    }
}
