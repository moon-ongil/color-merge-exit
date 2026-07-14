using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Fire-and-forget colored particle burst, built entirely in code (no prefab).
    /// Used for the exit "boost" and the clear celebration. Returns the system so
    /// the editor screenshot tool can Simulate() a frame for verification.
    /// </summary>
    public static class ParticleBurst
    {
        private static Material _mat;

        private static Material SharedMaterial()
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Sprites/Default"));
                _mat.mainTexture = VisualAssets.Square().texture;
            }
            return _mat;
        }

        public static ParticleSystem Emit(Vector3 position, Color color,
            int count = 26, float speed = 7f, float size = 0.28f, float life = 0.8f)
        {
            var go = new GameObject("Burst");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.duration = 0.1f;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 1.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = SharedMaterial();
            renderer.sortingOrder = 100;

            ps.Play();
            if (Application.isPlaying) Object.Destroy(go, life + 0.5f);
            return ps;
        }
    }
}
