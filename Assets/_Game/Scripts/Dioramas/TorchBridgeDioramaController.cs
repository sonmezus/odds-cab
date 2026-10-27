using System.Collections;
using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab.Dioramas
{
    /// <summary>World diorama and rules for the four-traveler, one-torch bridge puzzle.</summary>
    public sealed class TorchBridgeDioramaController : MonoBehaviour
    {
        const int AllTravelersMask = 0xF;
        static readonly int[] DefaultTimes = { 1, 2, 5, 10 };
        static readonly Color[] TravelerColors =
        {
            new Color(0.88f, 0.31f, 0.24f),
            new Color(0.20f, 0.56f, 0.84f),
            new Color(0.24f, 0.68f, 0.46f),
            new Color(0.86f, 0.65f, 0.22f),
        };

        readonly Transform[] _travelers = new Transform[4];
        readonly AmbientNpcAnimator[] _motion = new AmbientNpcAnimator[4];
        readonly Vector3[] _previousPositions = new Vector3[4];
        readonly Vector3[] _crossStarts = new Vector3[4], _crossEnds = new Vector3[4];
        Transform _torch;
        PuzzleData _puzzle;
        int _rightBankMask;
        int _selectedMask;
        int _elapsedMinutes;
        bool _torchOnRight;
        bool _crossing;

        public int SelectedMask => _selectedMask;
        public int ElapsedMinutes => _elapsedMinutes;
        public bool TorchOnRightBank => _torchOnRight;
        public bool IsCrossing => _crossing;
        public bool IsComplete => _rightBankMask == AllTravelersMask && _torchOnRight;
        public int TargetMinutes => _puzzle != null ? _puzzle.answerValue : 17;
        public int SelectedCrossingMinutes => LongestTravelerTime(_selectedMask);
        public bool CanCross => !_crossing && _selectedMask != 0 &&
            (_selectedMask & TravelersOnTorchBank()) == _selectedMask;

        public void BuildGeometry()
        {
            var waterMat = GlbModelLoader.MakeMat(new Color(0.10f, 0.39f, 0.55f), 0.72f);
            var stoneMat = GlbModelLoader.MakeMat(new Color(0.79f, 0.73f, 0.64f), 0.24f);
            var woodMat = GlbModelLoader.MakeMat(new Color(0.39f, 0.24f, 0.14f), 0.28f);
            var railMat = GlbModelLoader.MakeMat(new Color(0.16f, 0.20f, 0.21f), 0.5f);
            var torchMat = GlbModelLoader.MakeMat(new Color(0.94f, 0.62f, 0.13f), 0.55f, 0.12f);
            var flameMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.69f, 0.18f), 0.85f);

            bool authored = DioramaPresentation.TryStage("TorchBridgeStage", transform, out var stage);
            if (!authored)
            {
            MakePart("River", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0f), new Vector3(7.2f, 0.20f, 2.0f), waterMat);
            MakePart("NearBank", PrimitiveType.Cube, new Vector3(-2.48f, 0.43f, 0f), new Vector3(2.05f, 0.50f, 2.1f), stoneMat);
            MakePart("FarBank", PrimitiveType.Cube, new Vector3(2.48f, 0.43f, 0f), new Vector3(2.05f, 0.50f, 2.1f), stoneMat);
            MakePart("BridgeDeck", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(3.45f, 0.12f, 0.92f), woodMat);
            for (int side = -1; side <= 1; side += 2)
            {
                MakePart("BridgeRailPost_" + side, PrimitiveType.Cube, new Vector3(0f, 0.97f, side * 0.49f), new Vector3(3.45f, 0.40f, 0.07f), railMat);
                for (int post = -1; post <= 1; post++)
                    MakePart("BridgePost_" + side + "_" + post, PrimitiveType.Cylinder,
                        new Vector3(post * 1.35f, 0.91f, side * 0.49f), new Vector3(0.06f, 0.22f, 0.06f), torchMat);
            }

            }

            for (int i = 0; i < _travelers.Length; i++)
            {
                var person = new GameObject("Traveler_" + (i + 1));
                person.transform.SetParent(transform, false);
                person.transform.localPosition = TravelerPosition(i, false);
                var coatMat = GlbModelLoader.MakeMat(TravelerColors[i], 0.34f);
                MakeChild(person.transform, "Coat", PrimitiveType.Capsule, new Vector3(0f, 0.35f, 0f), new Vector3(0.22f, 0.26f, 0.22f), coatMat);
                MakeChild(person.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.73f, 0f), new Vector3(0.20f, 0.20f, 0.20f), stoneMat);
                MakeChild(person.transform, "Scarf", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0.11f), new Vector3(0.22f, 0.07f, 0.03f), torchMat);
                _travelers[i] = person.transform;
                // The game's own passengers cross the bridge (small scale) instead of capsule figures.
                if (LivingIstanbulAssets.TryCreateCharacter(i, person.transform, out var model, out _motion[i]))
                {
                        for (int c = person.transform.childCount - 1; c >= 0; c--)
                            if (person.transform.GetChild(c) != model.transform) Destroy(person.transform.GetChild(c).gameObject);
                        model.transform.localScale = Vector3.one * 0.52f;
                        model.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);   // turned towards the camera
                }
                _previousPositions[i] = person.transform.localPosition;
            }

            var torch = new GameObject("SharedTorch");
            torch.transform.SetParent(transform, false);
            _torch = torch.transform;
            MakeChild(_torch, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.07f, 0.24f, 0.07f), woodMat);
            MakeChild(_torch, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.47f, 0f), new Vector3(0.15f, 0.22f, 0.15f), flameMat);
            _torch.localPosition = TorchPosition(false);
            DioramaPresentation.AddLighting(transform, 7.2f);
        }

        void Update()
        {
            for (int i = 0; i < _travelers.Length; i++)
            {
                if (_travelers[i] == null || _motion[i] == null) continue;
                var position = _travelers[i].localPosition;
                float distance = Vector3.Distance(position, _previousPositions[i]);
                _motion[i].Tick(Time.deltaTime, distance, distance > .0001f ? AmbientNpcActivity.Walking : AmbientNpcActivity.Idle);
                _previousPositions[i] = position;
            }
        }

        public void LoadPuzzle(PuzzleData puzzle)
        {
            _puzzle = puzzle;
            _rightBankMask = 0;
            _selectedMask = 0;
            _elapsedMinutes = 0;
            _torchOnRight = false;
            _crossing = false;
            for (int i = 0; i < _travelers.Length; i++)
                if (_travelers[i] != null)
                {
                    _travelers[i].localPosition = TravelerPosition(i, false);
                    _previousPositions[i] = _travelers[i].localPosition;
                }
            if (_torch != null) _torch.localPosition = TorchPosition(false);
        }

        public bool IsOnTorchBank(int index)
        {
            if (index < 0 || index >= _travelers.Length) return false;
            bool onRight = (_rightBankMask & (1 << index)) != 0;
            return onRight == _torchOnRight;
        }

        public bool IsSelected(int index) => (_selectedMask & (1 << index)) != 0;

        public bool CanSelectTraveler(int index)
        {
            if (_crossing || !IsOnTorchBank(index)) return false;
            return IsSelected(index) || CountBits(_selectedMask) < 2;
        }

        public void ToggleTraveler(int index)
        {
            if (!CanSelectTraveler(index)) return;
            _selectedMask ^= 1 << index;
            SynthAudio.Instance?.PlayClick();
        }

        public void ClearSelection()
        {
            if (_crossing) return;
            _selectedMask = 0;
        }

        public bool SelectOptimalHint()
        {
            if (_crossing) return false;
            int nextMove = OptimalNextMoveMask();
            if (nextMove == 0) return false;
            _selectedMask = nextMove;
            return true;
        }

        public IEnumerator CrossSelected()
        {
            if (!CanCross) yield break;

            int moveMask = _selectedMask;
            bool fromRight = _torchOnRight;
            bool toRight = !fromRight;
            int crossingMinutes = LongestTravelerTime(moveMask);
            var starts = _crossStarts;
            var ends = _crossEnds;
            for (int i = 0; i < _travelers.Length; i++)
            {
                if ((moveMask & (1 << i)) == 0) continue;
                starts[i] = _travelers[i].localPosition;
                ends[i] = TravelerPosition(i, toRight);
                if (_motion[i] != null) _motion[i].transform.localRotation = Quaternion.Euler(0f, toRight ? 90f : -90f, 0f);
            }

            _crossing = true;
            _selectedMask = 0;
            const float animationDuration = 0.8f;
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));
                for (int i = 0; i < _travelers.Length; i++)
                    if ((moveMask & (1 << i)) != 0)
                        _travelers[i].localPosition = Vector3.Lerp(starts[i], ends[i], t);
                if (_torch != null) _torch.localPosition = Vector3.Lerp(TorchPosition(fromRight), TorchPosition(toRight), t);
                yield return null;
            }

            _rightBankMask = toRight ? (_rightBankMask | moveMask) : (_rightBankMask & ~moveMask);
            _torchOnRight = toRight;
            _elapsedMinutes += crossingMinutes;
            _crossing = false;
            if (_torch != null) _torch.localPosition = TorchPosition(_torchOnRight);
            for (int i = 0; i < _motion.Length; i++)
                if (_motion[i] != null) _motion[i].transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
        }

        int OptimalNextMoveMask()
        {
            int start = _rightBankMask | (_torchOnRight ? 16 : 0);
            const int goal = AllTravelersMask | 16;
            if (start == goal) return 0;

            const int stateCount = 32;
            var distance = new int[stateCount];
            var previous = new int[stateCount];
            var previousMove = new int[stateCount];
            var visited = new bool[stateCount];
            for (int i = 0; i < stateCount; i++)
            {
                distance[i] = int.MaxValue;
                previous[i] = -1;
            }
            distance[start] = 0;

            for (int step = 0; step < stateCount; step++)
            {
                int state = -1;
                for (int candidate = 0; candidate < stateCount; candidate++)
                    if (!visited[candidate] && (state < 0 || distance[candidate] < distance[state])) state = candidate;
                if (state < 0 || distance[state] == int.MaxValue || state == goal) break;
                visited[state] = true;

                int rightMask = state & AllTravelersMask;
                bool torchRight = (state & 16) != 0;
                int available = torchRight ? rightMask : (AllTravelersMask ^ rightMask);
                for (int move = 1; move <= AllTravelersMask; move++)
                {
                    int count = CountBits(move);
                    if (count > 2 || (move & available) != move) continue;
                    int nextRightMask = rightMask ^ move;
                    int nextState = nextRightMask | (torchRight ? 0 : 16);
                    int newDistance = distance[state] + LongestTravelerTime(move);
                    if (newDistance >= distance[nextState]) continue;
                    distance[nextState] = newDistance;
                    previous[nextState] = state;
                    previousMove[nextState] = move;
                }
            }

            if (previous[goal] < 0) return 0;
            int cursor = goal;
            while (previous[cursor] != start && previous[cursor] >= 0) cursor = previous[cursor];
            return previous[cursor] == start ? previousMove[cursor] : 0;
        }

        int TravelersOnTorchBank() => _torchOnRight ? _rightBankMask : (AllTravelersMask ^ _rightBankMask);

        int LongestTravelerTime(int mask)
        {
            int longest = 0;
            for (int i = 0; i < DefaultTimes.Length; i++)
                if ((mask & (1 << i)) != 0)
                    longest = Mathf.Max(longest, _puzzle != null && _puzzle.ints != null && i < _puzzle.ints.Length
                        ? _puzzle.ints[i] : DefaultTimes[i]);
            return longest;
        }

        static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        /// <summary>Two camera-readable rows, mirrored across the river, within each 2.05 x 2.1 m bank.</summary>
        public static Vector3 TravelerPosition(int index, bool rightBank)
        {
            index = Mathf.Clamp(index, 0, 3);
            float x = (index & 1) == 0 ? -2.95f : -2.12f;
            float z = index < 2 ? -.50f : .42f;
            return new Vector3(rightBank ? -x : x, .69f, z);
        }

        static Vector3 TorchPosition(bool rightBank) =>
            new Vector3(rightBank ? 1.90f : -1.90f, 0.79f, -0.78f);

        GameObject MakePart(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static Transform MakeChild(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go.transform;
        }
    }
}
