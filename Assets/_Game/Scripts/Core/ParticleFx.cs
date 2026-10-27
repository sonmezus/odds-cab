using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Runtime-built Particle Systems for the game's bursts and the correct-answer celebration (no prefab or
    /// texture assets: textures are generated once, materials are shared). Replaces the old primitive cubes,
    /// which allocated new GameObjects and a new material on every burst.
    /// </summary>
    public static class ParticleFx
    {
        static Material s_glowMat, s_starMat;
        static Texture2D s_glowTex, s_starTex;

        // "Sprites/Default" is always included in builds; Shader.Find on the URP particle shaders is not.
        static Material MakeMat(Texture2D tex, string name)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = tex, renderQueue = 3100 };
            return m;
        }

        public static Material GlowMaterial => s_glowMat != null ? s_glowMat : (s_glowMat = MakeMat(GlowTexture, "FxGlow"));
        public static Material StarMaterial => s_starMat != null ? s_starMat : (s_starMat = MakeMat(StarTexture, "FxStar"));

        /// <summary>Soft round glow, bright core.</summary>
        static Texture2D GlowTexture
        {
            get
            {
                if (s_glowTex != null) return s_glowTex;
                const int s = 64;
                s_glowTex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxGlowTex" };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (0.6f + 0.4f * a);
                        s_glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                s_glowTex.Apply();
                return s_glowTex;
            }
        }

        /// <summary>Four-point twinkle star with a small halo.</summary>
        static Texture2D StarTexture
        {
            get
            {
                if (s_starTex != null) return s_starTex;
                const int s = 64;
                s_starTex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxStarTex" };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float rays = Mathf.Max(Mathf.Exp(-Mathf.Abs(u) * 22f) * (1f - Mathf.Abs(v)),
                                               Mathf.Exp(-Mathf.Abs(v) * 22f) * (1f - Mathf.Abs(u)));
                        float halo = Mathf.Clamp01(1f - r * 2.2f);
                        float a = Mathf.Clamp01(rays + halo * halo);
                        s_starTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                s_starTex.Apply();
                return s_starTex;
            }
        }

        static ParticleSystem NewSystem(string name, Transform parent, Material mat, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>
        /// One-shot burst (pickup, success, error, rewind, brake): bright sparks thrown up and out under gravity
        /// plus a soft flash, both fading in the burst colour. Lives in world space so it stays where it happened.
        /// </summary>
        public static ParticleSystem CreateBurst(Transform parent)
        {
            var ps = NewSystem("FxBurst", parent, StarMaterial, 64);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            // Soft flash at the centre, as a child so one Play() fires both
            var flash = NewSystem("Flash", ps.transform, GlowMaterial, 2);
            var fm = flash.main;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.startLifetime = 0.35f;
            fm.startSpeed = 0f;
            fm.startSize = 1.6f;
            var fs = flash.shape; fs.enabled = false;
            var fsz = flash.sizeOverLifetime; fsz.enabled = true;
            fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));
            var fc = flash.colorOverLifetime; fc.enabled = true;
            fc.color = new ParticleSystem.MinMaxGradient(FadeGradient());
            ps.gameObject.AddComponent<EventBurstRig>().Initialize(ps, flash);
            return ps;
        }

        /// <summary>Fires a burst: <paramref name="count"/> sparks in <paramref name="color"/> at <paramref name="pos"/>.</summary>
        public static void Fire(ParticleSystem burst, Vector3 pos, Color color, int count, float scale)
        {
            if (burst == null) return;
            burst.transform.position = pos;
            if (burst.TryGetComponent<EventBurstRig>(out var rig)) rig.Fire(color, count, scale);
            else burst.Emit(count);
        }

        public static ParticleSystem CreateReceiverMotes(Transform parent)
        {
            var ps = NewSystem("ReceiverMotes", parent, StarMaterial, 32);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new Color(0.40f, 1f, 0.82f, 0.7f);
            main.gravityModifier = -0.025f;
            var emission = ps.emission;
            emission.rateOverTime = DeviceTierDetector.CurrentTier == DeviceTier.Low ? 3f : 8f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.85f;
            shape.radiusThickness = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient();
            return ps;
        }

        /// <summary>
        /// Correct-answer celebration around the moving taxi: spinning 3D gold coins fountaining up from the taxi
        /// (local space: they ride along), and golden glitter that the taxi leaves behind (world space trail).
        /// </summary>
        public static ParticleSystem CreateCoinFountain(Transform parent)
        {
            // Polished gold with only a faint glow, so the spinning coins catch the light instead of reading as
            // flat yellow discs
            var coinMat = GlbModelLoader.MakeMat(new Color(1f, 0.76f, 0.2f), 0.78f, 0.9f);
            coinMat.EnableKeyword("_EMISSION");
            coinMat.SetColor("_EmissionColor", new Color(0.55f, 0.36f, 0.04f) * 0.35f);
            var ps = NewSystem("FxCoinFountain", parent, coinMat, 40);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 3.0f);
            main.startSize3D = true;
            main.startSizeX = 0.2f; main.startSizeY = 0.025f; main.startSizeZ = 0.2f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(Mathf.PI * 0.5f);
            main.gravityModifier = 0.75f;
            var em = ps.emission;
            em.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.55f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.position = new Vector3(0f, 1.3f, -0.2f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.y = new ParticleSystem.MinMaxCurve(5f, 9f);   // coins spin about their face normal axis
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var shrink = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, shrink);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = CoinMesh();
            r.alignment = ParticleSystemRenderSpace.World;

            // Glitter around the taxi, drifting back along it (local space: in world space the taxi at
            // 45+ km/h left it behind the camera at once)
            var glitter = NewSystem("Glitter", ps.transform, StarMaterial, 120);
            var gm = glitter.main;
            gm.simulationSpace = ParticleSystemSimulationSpace.Local;
            gm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            gm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            var gv = glitter.velocityOverLifetime; gv.enabled = true;
            gv.space = ParticleSystemSimulationSpace.Local;
            // (all three axes must use the same curve mode)
            gv.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f); gv.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            gv.z = new ParticleSystem.MinMaxCurve(-3.5f, -2f);
            gm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            gm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f), new Color(1f, 0.74f, 0.2f));
            gm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            gm.gravityModifier = -0.05f;
            var gem = glitter.emission; gem.rateOverTime = 70f;
            var gs = glitter.shape;
            gs.shapeType = ParticleSystemShapeType.Box;
            gs.scale = new Vector3(1.8f, 1.2f, 3.0f);
            gs.position = new Vector3(0f, 0.8f, 0f);
            var gsz = glitter.sizeOverLifetime; gsz.enabled = true;
            gsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));
            var gc = glitter.colorOverLifetime; gc.enabled = true;
            gc.color = new ParticleSystem.MinMaxGradient(FadeGradient());
            return ps;
        }

        static Material s_foamMat;
        static Texture2D s_foamTex;

        /// <summary>Lumpy foam blob: soft-edged with darker gaps, so overlapping puffs read as churned water.</summary>
        static Material FoamMaterial
        {
            get
            {
                if (s_foamMat != null) return s_foamMat;
                const int s = 64;
                s_foamTex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "FxFoamTex" };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.6f + Mathf.PerlinNoise(x * 0.45f + 9f, y * 0.45f + 3f) * 0.4f;
                        float a = Mathf.Clamp01((1f - r) * 2.2f) * Mathf.SmoothStep(0.25f, 0.6f, n);
                        s_foamTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                s_foamTex.Apply(true);
                return s_foamMat = MakeMat(s_foamTex, "FxFoam");
            }
        }

        /// <summary>
        /// A wave breaking against the quay wall: a curtain of white spray thrown up along the wall face and
        /// falling back, fine droplets flung higher, and foam that spreads flat on the water and fades.
        /// Local X runs along the quay, local +Z points from the sea toward the land. Fire with <see cref="FireWave"/>.
        /// </summary>
        public static ParticleSystem CreateWaveSplash(Transform parent)
        {
            var spray = NewSystem("FxWaveSpray", parent, FoamMaterial, 90);
            var m = spray.main;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            // Launch velocity comes from the velocity module (random per particle, local axes) rather than the
            // shape: a flattened Box emits along its face normals, which sent half the spray down into the sea.
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.95f), new Color(0.86f, 0.94f, 1f, 0.8f));
            m.gravityModifier = 0f;
            var sh = spray.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(4.0f, 0.1f, 0.3f);              // a strip along the wall (local X = along the quay)
            var vel = spray.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            // up, toward the land, spread along the wall; the y curve falls off like gravity
            // (all three axes must use the same mode, so the constant ranges are written as flat curves)
            vel.x = Range(-0.7f, 0.7f);
            vel.y = new ParticleSystem.MinMaxCurve(5.0f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, -1.2f)), new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(1f, -0.9f)));
            vel.z = Range(0.3f, 1.4f);
            var sz = spray.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f)));
            var rot = spray.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var col = spray.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            // fine droplets, faster and higher
            var drops = NewSystem("Droplets", spray.transform, GlowMaterial, 120);
            var dm = drops.main;
            dm.simulationSpace = ParticleSystemSimulationSpace.World;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            dm.startSpeed = 0f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            dm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.95f));
            dm.gravityModifier = 0f;
            var dsh = drops.shape; dsh.shapeType = ParticleSystemShapeType.Box; dsh.scale = new Vector3(3.6f, 0.1f, 0.2f);
            var dv = drops.velocityOverLifetime; dv.enabled = true; dv.space = ParticleSystemSimulationSpace.Local;
            dv.x = Range(-1.2f, 1.2f);
            dv.y = new ParticleSystem.MinMaxCurve(7.0f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, -1.3f)), new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(1f, -0.9f)));
            dv.z = Range(0.2f, 2.0f);
            var dc = drops.colorOverLifetime; dc.enabled = true; dc.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            // foam spreading flat on the water at the foot of the wall
            var foam = NewSystem("Foam", spray.transform, FoamMaterial, 24);
            var fm = foam.main;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.98f, 1f, 0.8f));
            var fsh = foam.shape; fsh.shapeType = ParticleSystemShapeType.Box; fsh.scale = new Vector3(4.4f, 0.02f, 1.2f); fsh.position = new Vector3(0f, 0f, -0.8f);
            var fsz = foam.sizeOverLifetime; fsz.enabled = true;
            fsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));
            var fc = foam.colorOverLifetime; fc.enabled = true; fc.color = new ParticleSystem.MinMaxGradient(FadeGradient());
            var fr = foam.GetComponent<ParticleSystemRenderer>(); fr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            foam.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            spray.gameObject.AddComponent<WaveSplashRig>().Initialize(drops, foam);
            return spray;
        }

        /// <summary>A random-between range expressed as two flat curves (TwoCurves mode).</summary>
        static ParticleSystem.MinMaxCurve Range(float min, float max) =>
            new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, min), AnimationCurve.Constant(0f, 1f, max));

        /// <summary>One wave; <paramref name="strength"/> 0..1 scales the amount of water thrown.</summary>
        public static void FireWave(ParticleSystem wave, Vector3 pos, float strength, int seaSide)
        {
            if (wave == null) return;
            wave.transform.position = pos;
            wave.transform.rotation = Quaternion.Euler(0f, -90f * seaSide, 0f);   // local X along the quay (world Z), local Z toward the land
            wave.Emit(Mathf.RoundToInt(Mathf.Lerp(24f, 56f, strength)));
            if (wave.TryGetComponent<WaveSplashRig>(out var rig)) rig.Emit(strength);
        }

        static Mesh s_coinMesh;

        /// <summary>Low-poly coin: a 14-sided disc with a raised rim, 1 m across (scaled by the particle size).</summary>
        static Mesh CoinMesh()
        {
            if (s_coinMesh != null) return s_coinMesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s_coinMesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            s_coinMesh.name = "FxCoin";
            Object.Destroy(tmp);
            return s_coinMesh;
        }

        static Gradient FadeGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
