using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab.Dioramas
{
    /// <summary>Four tuned bells. Fixed animation state; demonstration pauses the puzzle timer.</summary>
    public sealed class BellDioramaController : MonoBehaviour
    {
        readonly Transform[] _bells = new Transform[4];
        readonly Material[] _pads = new Material[4], _orbs = new Material[4];
        readonly float[] _age = { 1f, 1f, 1f, 1f };
        readonly float[] _duration = new float[4];
        readonly Color[] _colors = { new Color(.88f,.24f,.24f), new Color(.22f,.48f,.92f),
            new Color(.18f,.72f,.48f), new Color(.95f,.74f,.20f) };
        PuzzleData _puzzle;
        int _sequenceIndex, _lastBell = -1;
        float _sequenceClock;
        public bool IsPlayingSequence { get; private set; }
        public int ActiveNoteBell { get; private set; }

        public void BuildGeometry()
        {
            if (DioramaPresentation.TryStage("BellStage", transform, out var stage))
            {
                for (int i = 0; i < 4; i++)
                {
                    _bells[i] = DioramaPresentation.Find(stage.transform, "BellPivot_" + (i + 1));
                    _pads[i] = OwnMaterial(DioramaPresentation.Find(stage.transform, "Pad_" + (i + 1)));
                    _orbs[i] = OwnMaterial(DioramaPresentation.Find(stage.transform, "NoteLight_" + (i + 1)));
                }
            }
            else BuildFallback();
            DioramaPresentation.AddLighting(transform);
            ResetRings();
        }

        static Material OwnMaterial(Transform part)
        {
            var renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null) return null;
            var material = new Material(renderer.sharedMaterial) { enableInstancing = true };
            renderer.sharedMaterial = material;
            return material;
        }

        void BuildFallback()
        {
            var wood = DioramaPresentation.Surface("WoodOak", new Color(.42f,.27f,.16f), .35f);
            var stone = DioramaPresentation.Surface("Limestone", new Color(.88f,.80f,.68f), .25f);
            Part("Plinth", PrimitiveType.Cube, new Vector3(0,.18f,0), new Vector3(3.4f,.36f,1.5f), wood);
            for (int side = -1; side <= 1; side += 2)
                Part("Column", PrimitiveType.Cylinder, new Vector3(side*1.42f,1.30f,0), new Vector3(.32f,1,.32f), stone);
            Part("Arch", PrimitiveType.Cube, new Vector3(0,2.35f,0), new Vector3(3.3f,.26f,.5f), stone);
            for (int i = 0; i < 4; i++)
            {
                float x = -.87f + i * .58f;
                var pivot = new GameObject("BellPivot_" + (i + 1)).transform;
                pivot.SetParent(transform, false); pivot.localPosition = new Vector3(x,2.17f,0);
                var bell = Part("Bell", PrimitiveType.Cylinder, new Vector3(x,1.65f,0), new Vector3(.42f,.26f,.42f),
                    GlbModelLoader.MakeMat(_colors[i], .75f, .65f));
                bell.transform.SetParent(pivot, true); _bells[i] = pivot;
                _pads[i] = Part("Pad", PrimitiveType.Cylinder, new Vector3(x,.42f,-.25f), new Vector3(.38f,.04f,.38f),
                    GlbModelLoader.MakeMat(_colors[i], .6f)).GetComponent<Renderer>().sharedMaterial;
                _orbs[i] = Part("Light", PrimitiveType.Sphere, new Vector3(x,2.22f,-.12f), Vector3.one*.16f,
                    GlbModelLoader.MakeMat(_colors[i], .8f)).GetComponent<Renderer>().sharedMaterial;
            }
        }

        public void LoadPuzzle(PuzzleData puzzle) { _puzzle = puzzle; ReplaySequence(); }
        public void ReplaySequence()
        {
            ResetRings();
            if (_puzzle?.ints == null || _puzzle.ints.Length == 0) return;
            _sequenceIndex = 0; _sequenceClock = .4f; IsPlayingSequence = true;
        }
        public void RingBellOnce(int bellNumber1To4) => Ring(Mathf.Clamp(bellNumber1To4 - 1, 0, 3), .38f);
        void Ring(int index, float duration)
        {
            _age[index] = 0f; _duration[index] = duration; _lastBell = index; ActiveNoteBell = index + 1;
            SynthAudio.Instance?.PlayBell(index + 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsPlayingSequence)
            {
                _sequenceClock -= dt;
                if (_sequenceClock <= 0f)
                {
                    if (_sequenceIndex < _puzzle.ints.Length)
                    {
                        Ring(Mathf.Clamp(_puzzle.ints[_sequenceIndex++] - 1, 0, 3), .46f);
                        _sequenceClock += .68f;
                    }
                    else { IsPlayingSequence = false; ActiveNoteBell = 0; }
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (_age[i] >= _duration[i]) continue;
                _age[i] = Mathf.Min(_age[i] + dt, _duration[i]);
                float pulse = 1f - _age[i] / _duration[i];
                if (_bells[i] != null)
                    _bells[i].localRotation = Quaternion.Euler(Mathf.Sin(_age[i]*28f)*Mathf.Exp(-_age[i]*4f)*24f*pulse, 0f, 0f);
                SetPulse(i, pulse);
                if (pulse <= 0f && _lastBell == i) ActiveNoteBell = 0;
            }
        }

        void SetPulse(int index, float pulse)
        {
            var color = Color.Lerp(_colors[index]*.62f, Color.Lerp(_colors[index],Color.white,.58f), pulse);
            if (_pads[index] != null) _pads[index].SetColor("_BaseColor", color);
            if (_orbs[index] != null)
            {
                _orbs[index].EnableKeyword("_EMISSION");
                _orbs[index].SetColor("_BaseColor", color);
                _orbs[index].SetColor("_EmissionColor", _colors[index]*(.25f+pulse*2.5f));
            }
        }
        void ResetRings()
        {
            IsPlayingSequence = false; ActiveNoteBell = 0; _lastBell = -1;
            for (int i = 0; i < 4; i++)
            {
                _age[i] = 1f; _duration[i] = 0f;
                if (_bells[i] != null) _bells[i].localRotation = Quaternion.identity;
                SetPulse(i, 0f);
            }
        }
        void OnDisable() => ResetRings();
        void OnDestroy()
        {
            for (int i = 0; i < 4; i++)
            { if (_pads[i] != null) Destroy(_pads[i]); if (_orbs[i] != null) Destroy(_orbs[i]); }
        }
        GameObject Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name;
            part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; Destroy(part.GetComponent<Collider>()); return part;
        }
    }
}
