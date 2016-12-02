﻿/* Copyright (C) <2016> <Jacob Shockley, Jitter Physics>
* 
*  This software is provided 'as-is', without any express or implied
*  warranty.  In no event will the authors be held liable for any damages
*  arising from the use of this software.
*
*  Permission is granted to anyone to use this software for any purpose,
*  including commercial applications, and to alter it and redistribute it
*  freely, subject to the following restrictions:
*
*  1. The origin of this software must not be misrepresented; you must not
*      claim that you wrote the original software. If you use this software
*      in a product, an acknowledgment in the product documentation would be
*      appreciated but is not required.
*  2. Altered source versions must be plainly marked as such, and must not be
*      misrepresented as being the original software.
*  3. This notice may not be removed or altered from any source distribution. 
*/

#region Using Statements
using System;
using System.Collections.Generic;

using Jitter.Dynamics;
using Jitter.LinearMath;
using Jitter.Collision.Shapes;
#endregion

namespace Jitter.Collision.Shapes
{

    /// <summary>
    /// A <see cref="Shape"/> representing a compoundShape consisting
    /// of several 'sub' shapes.
    /// </summary>
    public class DynamicShape : Multishape
    {
        #region public struct TransformedShape

        /// <summary>
        /// Holds a 'sub' shape and it's transformation. This TransformedShape can
        /// be added to the <see cref="CompoundShape"/>
        /// </summary>
        public struct TransformedShape
        {
            private Shape shape;
            internal JVector position;
            internal JMatrix orientation;
            internal JMatrix invOrientation;
            internal JBBox boundingBox;

            /// <summary>
            /// The 'sub' shape.
            /// </summary>
            public Shape Shape { get { return shape; } set { shape = value; } }

            /// <summary>
            /// The position of a 'sub' shape
            /// </summary>
            public JVector Position { get { return position; } set { position = value; UpdateBoundingBox(); } }

            public JBBox BoundingBox { get { return boundingBox; } }

            /// <summary>
            /// The inverse orientation of the 'sub' shape.
            /// </summary>
            public JMatrix InverseOrientation
            {
                get { return invOrientation; }
            }

            /// <summary>
            /// The orienation of the 'sub' shape.
            /// </summary>
            public JMatrix Orientation
            {
                get { return orientation; }
                set { orientation = value; JMatrix.Transpose(ref orientation, out invOrientation); UpdateBoundingBox(); }
            }

            public void UpdateBoundingBox()
            {
                Shape.GetBoundingBox(ref orientation, out boundingBox);

                boundingBox.Min += position;
                boundingBox.Max += position;
            }

            /// <summary>
            /// Creates a new instance of the TransformedShape struct.
            /// </summary>
            /// <param name="shape">The shape.</param>
            /// <param name="orientation">The orientation this shape should have.</param>
            /// <param name="position">The position this shape should have.</param>
            public TransformedShape(Shape shape, JMatrix orientation, JVector position)
            {
                this.position = position;
                this.orientation = orientation;
                JMatrix.Transpose(ref orientation, out invOrientation);
                this.shape = shape;
                this.boundingBox = new JBBox();
                UpdateBoundingBox();
            }
        }
        #endregion

        private List<TransformedShape> shapes;

        /// <summary>
        /// An array conaining all 'sub' shapes and their transforms.
        /// </summary>
        public List<TransformedShape> Shapes { get { return this.shapes; } }

        JVector shifted;
        public JVector Shift { get { return -1.0f * this.shifted; } }

        private JBBox mInternalBBox;

        /// <summary>
        /// Created a new instance of the CompountShape class.
        /// </summary>
        /// <param name="shapes">The 'sub' shapes which should be added to this 
        /// class.</param>
        public DynamicShape(List<TransformedShape> shapes)
        {
            this.shapes = shapes;
            if (!TestValidity())
                throw new ArgumentException("Multishapes are not supported!");

            this.UpdateShape();
        }

        private bool TestValidity()
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Shape is Multishape) return false;
            }

            return true;
        }

        public override void MakeHull(ref List<JVector> triangleList, int generationThreshold)
        {
            List<JVector> triangles = new List<JVector>();

            for (int i = 0; i < shapes.Count; i++)
            {
                shapes[i].Shape.MakeHull(ref triangles, 4);
                for (int e = 0; e < triangles.Count; e++)
                {
                    JVector pos = triangles[e];
                    TransformedShape shape = shapes[i];
                    JVector.Transform(ref pos, ref shape.orientation, out pos);
                    JVector.Add(ref pos, ref shape.position, out pos);
                    shapes[i] = shape;
                    triangleList.Add(pos);
                }
                triangles.Clear();
            }
        }

        /// <summary>
        /// Translate all subshapes in the way that the center of mass is
        /// in (0,0,0)
        /// </summary>
        private void DoShifting()
        {
            for (int i = 0; i < Shapes.Count; i++) shifted += Shapes[i].position;
            shifted *= (1.0f / shapes.Count);

            for (int i = 0; i < Shapes.Count; i++) {
                TransformedShape shape = Shapes[i];
                shape.position -= shifted;
                Shapes[i] = shape;
            }
        }

        public override void CalculateMassInertia()
        {
            base.inertia = JMatrix.Zero;
            base.mass = 0.0f;

            for (int i = 0; i < Shapes.Count; i++)
            {
                JMatrix currentInertia = Shapes[i].InverseOrientation * Shapes[i].Shape.Inertia * Shapes[i].Orientation;
                JVector p = Shapes[i].Position * -1.0f;
                double m = Shapes[i].Shape.Mass;

                currentInertia.M11 += m * (p.Y * p.Y + p.Z * p.Z);
                currentInertia.M22 += m * (p.X * p.X + p.Z * p.Z);
                currentInertia.M33 += m * (p.X * p.X + p.Y * p.Y);

                currentInertia.M12 += -p.X * p.Y * m;
                currentInertia.M21 += -p.X * p.Y * m;

                currentInertia.M31 += -p.X * p.Z * m;
                currentInertia.M13 += -p.X * p.Z * m;

                currentInertia.M32 += -p.Y * p.Z * m;
                currentInertia.M23 += -p.Y * p.Z * m;

                base.inertia += currentInertia;
                base.mass += m;
            }
        }


        internal DynamicShape()
        {
        }

        protected override Multishape CreateWorkingClone()
        {
            DynamicShape clone = new DynamicShape();
            clone.shapes = this.shapes;
            return clone;
        }


        /// <summary>
        /// SupportMapping. Finds the point in the shape furthest away from the given direction.
        /// Imagine a plane with a normal in the search direction. Now move the plane along the normal
        /// until the plane does not intersect the shape. The last intersection point is the result.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="result">The result.</param>
        public override void SupportMapping(ref JVector direction, out JVector result)
        {
            TransformedShape shape = shapes[currentShape];
            JVector.Transform(ref direction, ref shape.invOrientation, out result);
            shapes[currentShape].Shape.SupportMapping(ref direction, out result);
            JVector.Transform(ref result, ref shape.orientation, out result);
            JVector.Add(ref result, ref shape.position, out result);
            Shapes[currentShape] = shape;
        }

        /// <summary>
        /// Gets the axis aligned bounding box of the orientated shape. (Inlcuding all
        /// 'sub' shapes)
        /// </summary>
        /// <param name="orientation">The orientation of the shape.</param>
        /// <param name="box">The axis aligned bounding box of the shape.</param>
        public override void GetBoundingBox(ref JMatrix orientation, out JBBox box)
        {
            box.Min = mInternalBBox.Min;
            box.Max = mInternalBBox.Max;

            JVector localHalfExtents = 0.5f * (box.Max - box.Min);
            JVector localCenter = 0.5f * (box.Max + box.Min);

            JVector center;
            JVector.Transform(ref localCenter, ref orientation, out center);

            JMatrix abs; JMath.Absolute(ref orientation, out abs);
            JVector temp;
            JVector.Transform(ref localHalfExtents, ref abs, out temp);

            box.Max = center + temp;
            box.Min = center - temp;
        }

        int currentShape = 0;
        List<int> currentSubShapes = new List<int>();

        /// <summary>
        /// Sets the current shape. First <see cref="CompoundShape.Prepare"/> has to be called.
        /// After SetCurrentShape the shape immitates another shape.
        /// </summary>
        /// <param name="index"></param>
        public override void SetCurrentShape(int index)
        {
            currentShape = currentSubShapes[index];
            shapes[currentShape].Shape.SupportCenter(out geomCen);
            geomCen += shapes[currentShape].Position;
        }

        /// <summary>
        /// Passes a axis aligned bounding box to the shape where collision
        /// could occour.
        /// </summary>
        /// <param name="box">The bounding box where collision could occur.</param>
        /// <returns>The upper index with which <see cref="SetCurrentShape"/> can be 
        /// called.</returns>
        public override int Prepare(ref JBBox box)
        {
            currentSubShapes.Clear();

            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].boundingBox.Contains(ref box) != JBBox.ContainmentType.Disjoint)
                    currentSubShapes.Add(i);
            }

            return currentSubShapes.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rayOrigin"></param>
        /// <param name="rayEnd"></param>
        /// <returns></returns>
        public override int Prepare(ref JVector rayOrigin, ref JVector rayEnd)
        {
            JBBox box = JBBox.SmallBox;

            box.AddPoint(ref rayOrigin);
            box.AddPoint(ref rayEnd);

            return this.Prepare(ref box);
        }


        public override void UpdateShape()
        {
            DoShifting();
            UpdateInternalBoundingBox();
            base.UpdateShape();
        }

        protected void UpdateInternalBoundingBox()
        {
            mInternalBBox.Min = new JVector(double.MaxValue);
            mInternalBBox.Max = new JVector(double.MinValue);

            for (int i = 0; i < shapes.Count; i++)
            {
                shapes[i].UpdateBoundingBox();

                TransformedShape shape = shapes[i];
                JBBox.CreateMerged(ref mInternalBBox, ref shape.boundingBox, out mInternalBBox);
                shapes[i] = shape;
            }
        }

        public void AddShape(TransformedShape shape) {
            shapes.Add(shape);
        }
        public void ClearShapes() {
            shapes.Clear();
        }
    }
}
