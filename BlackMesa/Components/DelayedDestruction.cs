using UnityEngine;

namespace BlackMesa.Components
{
    internal class DelayedDestruction : MonoBehaviour
    {
        public float timeUntilDestruction = 5;

        private void Update()
        {
            timeUntilDestruction -= Time.deltaTime;

            if (timeUntilDestruction <= 0)
                Destroy(gameObject);
        }
    }
}
