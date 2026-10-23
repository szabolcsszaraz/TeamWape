using Microsoft.Msagl.Core.Geometry;

namespace Microsoft.Msagl.Core.Layout
{
    internal class RectangleObstacle : IObstacle
    {
        public RectangleObstacle(Rectangle r)
        {
            this.Rectangle = r;
        }

        public RectangleObstacle(Rectangle r, object data)
        {
            this.Rectangle = r;
            this.Data = data;
        }

        public Rectangle Rectangle
        {
            get;
            set;
        }

        internal object Data
        {
            get;
            private set;
        }
    }
}