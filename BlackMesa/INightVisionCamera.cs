using UnityEngine;

namespace BlackMesa
{
    internal interface INightVisionCamera
    {
        public Camera Camera { get; }
        public Light NightVisionLight { get; }
    }
}
