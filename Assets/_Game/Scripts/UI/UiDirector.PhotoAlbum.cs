using System.IO;
using UnityEngine;
using UnityEngine.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        // One snapshot per district: taken from the live game camera the moment the district's 64th stop is solved,
        // saved next to the save file, and shown here. Locked slots show how far the player still has to drive.
        readonly Image[] _albumThumbs = new Image[GameConfig.DistrictCount];
        readonly UiText[] _albumTitles = new UiText[GameConfig.DistrictCount];
        readonly UiText[] _albumNotes = new UiText[GameConfig.DistrictCount];
        readonly Sprite[] _albumSprites = new Sprite[GameConfig.DistrictCount];

        static string AlbumPath(int district) => Path.Combine(Application.persistentDataPath, "album", $"district_{district + 1}.png");

        void BuildPhotoAlbumGrid(Transform card)
        {
            for (int d = 0; d < GameConfig.DistrictCount; d++)
            {
                int col = d % 2, row = d / 2;
                float x = col == 0 ? 0.28f : 0.72f;
                float y = 0.735f - row * 0.165f;
                var cell = MakePanel(card, "AlbumPhoto_" + d, ColNavySoft, new Vector2(x, y), new Vector2(x, y), Vector2.zero, new Vector2(444f, 230f));
                // Polaroid: cream frame, picture, caption under it
                var frame = MakePanel(cell.transform, "Polaroid", ColCream, new Vector2(0.27f, 0.5f), new Vector2(0.27f, 0.5f), Vector2.zero, new Vector2(186f, 186f));
                frame.transform.localRotation = Quaternion.Euler(0f, 0f, d % 2 == 0 ? -3f : 3f);
                var pic = MakePanel(frame.transform, "Picture", new Color(0.18f, 0.24f, 0.32f, 1f), new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(166f, 148f));
                _albumThumbs[d] = pic.GetComponent<Image>();
                _albumTitles[d] = MakeLabel(cell.transform, "Title", "", 28, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                SetRect(_albumTitles[d].rectTransform, new Vector2(0.75f, 0.62f), new Vector2(0.75f, 0.62f), Vector2.zero, new Vector2(196f, 120f));
                _albumNotes[d] = MakeLabel(cell.transform, "Note", "", 22, FontStyle.Italic, ColTeal, TextAnchor.LowerLeft);
                SetRect(_albumNotes[d].rectTransform, new Vector2(0.75f, 0.22f), new Vector2(0.75f, 0.22f), Vector2.zero, new Vector2(196f, 70f));
            }
        }

        void RefreshPhotoAlbum()
        {
            var solved = SaveSystem.Data.solvedStops;
            for (int d = 0; d < GameConfig.DistrictCount; d++)
            {
                int first = d * GameConfig.StopsPerDistrict + 1, last = first + GameConfig.StopsPerDistrict - 1;
                int done = 0;
                foreach (var s in solved) if (s >= first && s <= last) done++;
                bool unlocked = done >= GameConfig.StopsPerDistrict || SaveSystem.Data.campaignsCompleted > 0;
                _albumTitles[d].text = Loc.DistrictLandmark(d);
                if (unlocked)
                {
                    var passenger = PassengerCatalog.ForStop(last);
                    _albumNotes[d].text = Loc.F("album_note", passenger.DisplayName, last);
                    var sprite = LoadAlbumSprite(d);
                    _albumThumbs[d].sprite = sprite != null ? sprite : _maidenTowerPostcardSprite;
                    _albumThumbs[d].color = Color.white;
                }
                else
                {
                    _albumNotes[d].text = Loc.F("album_locked", done, GameConfig.StopsPerDistrict);
                    _albumThumbs[d].sprite = null;
                    _albumThumbs[d].color = new Color(0.18f, 0.24f, 0.32f, 1f);
                }
            }
        }

        Sprite LoadAlbumSprite(int d)
        {
            if (_albumSprites[d] != null) return _albumSprites[d];
            string path = AlbumPath(d);
            if (!File.Exists(path)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
                _albumSprites[d] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return _albumSprites[d];
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OddsCab] Album photo unreadable: " + e.Message);
                return null;
            }
        }

        /// <summary>Snap the 3D scene (no UI) as the district's album photo.</summary>
        public void CaptureDistrictPhoto(int district)
        {
            var cam = Camera.main;
            if (cam == null || district < 0 || district >= GameConfig.DistrictCount) return;
            const int w = 330, h = 296;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(AlbumPath(district)));
                File.WriteAllBytes(AlbumPath(district), tex.EncodeToJPG(88));
                _albumSprites[district] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OddsCab] Album photo not saved: " + e.Message);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
