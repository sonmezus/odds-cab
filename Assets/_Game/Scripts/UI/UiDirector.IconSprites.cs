using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OddsCab
{
    public partial class UiDirector
    {
        static Sprite CreateMapPinSprite()
        {
            const int w = 64, h = 80;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - 31.5f) / 24f;
                    float ny = (y - 50f) / 24f;
                    float distHead = Mathf.Sqrt(nx * nx + ny * ny);
                    bool inHead = distHead <= 1.0f;
                    bool inHole = distHead <= 0.40f;

                    // Lower teardrop cone tapering down to (31.5, 6)
                    float coneT = Mathf.Clamp01((y - 6f) / 42f);
                    bool inCone = y >= 6 && y <= 48 && Mathf.Abs(nx) <= coneT * 0.96f;

                    bool on = (inHead || inCone) && !inHole;
                    tex.SetPixel(x, y, on ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateSpeedoGaugeSprite()
        {
            const int w = 96, h = 52;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 48f) / 40f;
                    float dy = (y - 8f) / 40f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool arc = dy >= 0f && r >= 0.74f && r <= 1.0f;

                    // Speedometer needle pointing up-right
                    float proj = dx * 0.65f + dy * 0.76f;
                    float perp = Mathf.Abs(-dx * 0.76f + dy * 0.65f);
                    bool needle = (proj >= 0f && proj <= 0.76f && perp <= 0.08f) || (r <= 0.16f && dy >= -0.05f);

                    tex.SetPixel(x, y, (arc || needle) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateStopwatchSprite()
        {
            const int w = 64, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 32f) / 23f;
                    float dy = (y - 29f) / 23f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = r >= 0.78f && r <= 1.0f;

                    // Clock hand pointing up and slightly right
                    bool handUp = Mathf.Abs(dx) <= 0.09f && dy >= -0.05f && dy <= 0.58f;
                    bool centerDot = r <= 0.14f;

                    // Top crown button & stem
                    bool stem = Mathf.Abs(x - 32f) <= 3.5f && y >= 52 && y <= 58;
                    bool crown = Mathf.Abs(x - 32f) <= 8.5f && y >= 58 && y <= 64;

                    // Side diagonal lug ears
                    bool lugL = Mathf.Abs((x - 14f) + (y - 48f)) <= 4f && x >= 10 && x <= 19 && y >= 44 && y <= 53;
                    bool lugR = Mathf.Abs((x - 50f) - (y - 48f)) <= 4f && x >= 45 && x <= 54 && y >= 44 && y <= 53;

                    bool on = ring || handUp || centerDot || stem || crown || lugL || lugR;
                    tex.SetPixel(x, y, on ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateDriverHandSprite()
        {
            // Procedural stylized 3D-shaded Taxi Driver Right Hand + Index Finger + Dark Jacket Cuff (media_1792764991705_654c83ca.jpg)
            const int w = 160, h = 200;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color skinBase = new Color(0.95f, 0.72f, 0.56f, 1f);
            Color skinLight = new Color(0.99f, 0.82f, 0.68f, 1f);
            Color skinShadow = new Color(0.80f, 0.54f, 0.40f, 1f);
            Color nailCol = new Color(0.99f, 0.86f, 0.78f, 1f);
            Color cuffCol = new Color(0.16f, 0.19f, 0.23f, 1f);
            Color cuffRib = new Color(0.11f, 0.13f, 0.16f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Extended index finger tilted slightly left from (x=66, y=105) up to fingertip (x=42, y=184)
                    float ft = Mathf.Clamp01((y - 98f) / 86f);
                    float fingerCenterX = Mathf.Lerp(66f, 42f, ft);
                    float fingerHalfW = Mathf.Lerp(15f, 11.5f, ft);
                    float fdx = (x - fingerCenterX) / fingerHalfW;
                    float fdy = (y - 182f) / 12f;
                    bool inIndex = (y >= 98 && y <= 182 && Mathf.Abs(fdx) <= 1f) ||
                                   (y > 182 && (fdx * fdx + fdy * fdy) <= 1f);

                    // Fingernail at the tip of the index finger
                    float ndx = (x - 42f) / 7.5f;
                    float ndy = (y - 183f) / 8.5f;
                    bool inNail = (ndx * ndx + ndy * ndy) <= 1f && y >= 176;

                    // Back of hand / palm body centered around (x=88, y=68)
                    float pdx = (x - 88f) / 42f;
                    float pdy = (y - 68f) / 44f;
                    bool inPalm = (pdx * pdx + pdy * pdy) <= 1f && y >= 26 && y <= 112;

                    // Thumb on the left side around (x=44, y=68)
                    float tdx = (x - 45f) / 16f;
                    float tdy = (y - 68f) / 28f;
                    bool inThumb = (tdx * tdx + tdy * tdy) <= 1f && y >= 42 && y <= 94;

                    // Curled middle, ring & pinky knuckles on the right side
                    float k1 = Mathf.Pow((x - 90f) / 14f, 2f) + Mathf.Pow((y - 106f) / 16f, 2f);
                    float k2 = Mathf.Pow((x - 110f) / 13f, 2f) + Mathf.Pow((y - 98f) / 15f, 2f);
                    float k3 = Mathf.Pow((x - 126f) / 12f, 2f) + Mathf.Pow((y - 86f) / 14f, 2f);
                    bool inKnuckles = k1 <= 1f || k2 <= 1f || k3 <= 1f;

                    // Dark knitted jacket cuff at bottom wrist (y < 34)
                    float cdx = (x - 96f) / 44f;
                    bool inCuff = y >= 0 && y <= 34 && Mathf.Abs(cdx) <= 1f;

                    if (inCuff)
                    {
                        bool rib = (x % 6) < 2;
                        tex.SetPixel(x, y, rib ? cuffRib : cuffCol);
                    }
                    else if (inNail)
                    {
                        tex.SetPixel(x, y, nailCol);
                    }
                    else if (inIndex || inPalm || inThumb || inKnuckles)
                    {
                        float shade = Mathf.Clamp01(0.55f - (x - 75f) / 110f + (y - 80f) / 220f);
                        Color c = Color.Lerp(skinShadow, skinLight, shade);
                        c = Color.Lerp(skinBase, c, 0.7f);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateStackedCoinsSprite()
        {
            // Procedural 3D-shaded Stacked Gold "$" Coins Icon (matching "+25 JETON" pill in media_1792765036993_fce7d4e5.jpg)
            const int w = 96, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color goldLight = new Color(1.0f, 0.88f, 0.32f, 1f);
            Color goldMid = new Color(0.96f, 0.72f, 0.14f, 1f);
            Color goldDark = new Color(0.74f, 0.48f, 0.06f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Front-right upright "$" gold coin centered at (62, 30), radius 22
                    float fdx = (x - 62f) / 22f;
                    float fdy = (y - 30f) / 22f;
                    float fr = Mathf.Sqrt(fdx * fdx + fdy * fdy);
                    bool inFrontCoin = fr <= 1f;
                    bool inFrontRim = fr >= 0.76f && fr <= 1f;

                    // Left stack of 4 horizontal coin discs at x=30, y = 14, 24, 34, 44
                    bool inStack = false;
                    bool stackTop = false;
                    for (int s = 0; s < 4; s++)
                    {
                        float cy = 14f + s * 10f;
                        float sdx = (x - 32f) / 20f;
                        float sdy = (y - cy) / 7f;
                        if ((sdx * sdx + sdy * sdy) <= 1f)
                        {
                            inStack = true;
                            stackTop = (s == 3 && sdy > -0.2f);
                        }
                    }

                    if (inFrontCoin)
                    {
                        bool dollarBar = Mathf.Abs(x - 62f) <= 2f && Mathf.Abs(y - 30f) <= 11f;
                        tex.SetPixel(x, y, dollarBar ? goldDark : (inFrontRim ? goldLight : goldMid));
                    }
                    else if (inStack)
                    {
                        tex.SetPixel(x, y, stackTop ? goldLight : ((y % 10 < 3) ? goldDark : goldMid));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateWrongCrossBadgeSprite()
        {
            // Glowing Red Circular "⊗" Cross Badge (matching left of "YANLIŞ CEVAP / YOLCU İNDİ" in media_1792765107419_582e5035.jpg)
            const int w = 96, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color glowCol = new Color(1.0f, 0.25f, 0.22f, 1f);
            Color rimCol = new Color(1.0f, 0.42f, 0.36f, 1f);
            Color bodyRed = new Color(0.90f, 0.22f, 0.20f, 1f);
            Color darkRed = new Color(0.72f, 0.14f, 0.13f, 1f);
            Color crossWhite = new Color(0.99f, 0.96f, 0.92f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 47.5f) / 38f;
                    float dy = (y - 47.5f) / 38f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Diagonal X inside circle
                    float d1 = Mathf.Abs(dx - dy) * 0.7071f;
                    float d2 = Mathf.Abs(dx + dy) * 0.7071f;
                    bool inCross = (r <= 0.48f) && (d1 <= 0.125f || d2 <= 0.125f);

                    if (inCross)
                    {
                        tex.SetPixel(x, y, crossWhite);
                    }
                    else if (r <= 0.80f)
                    {
                        tex.SetPixel(x, y, (dy > 0f) ? bodyRed : darkRed);
                    }
                    else if (r <= 0.92f)
                    {
                        tex.SetPixel(x, y, rimCol);
                    }
                    else if (r <= 1.18f)
                    {
                        float a = Mathf.Clamp01(1f - (r - 0.92f) / 0.26f) * 0.55f;
                        tex.SetPixel(x, y, new Color(glowCol.r, glowCol.g, glowCol.b, a));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateRetryArrowSprite()
        {
            // Counter-clockwise circular retry arrow icon "↺" (matching "TEKRAR DENE" button in media_1792765107419_582e5035.jpg)
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 32f) / 22f;
                    float dy = (y - 32f) / 22f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180..180

                    // Circular ring with a gap in the upper-left quadrant (ang between 110 and 165)
                    bool inArc = (r >= 0.68f && r <= 1.0f) && !(ang >= 112f && ang <= 165f);

            // Arrowhead at top-left pointing counter-clockwise (around x=17, y=45)
                    float ax = x - 17f;
                    float ay = y - 45f;
                    bool inHead = (ax >= -6f && ax <= 8f && ay >= -8f && ay <= 6f) && ((ax + ay) <= 3f);

                    tex.SetPixel(x, y, (inArc || inHead) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateMaidensTowerPostcardSprite()
        {
            // Procedural Sunset Postcard of Maiden's Tower (Kız Kulesi) on the Bosphorus (matching bottom-left of media_1792765197031_224281b2.jpg)
            const int w = 160, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color skyTop = new Color(0.32f, 0.56f, 0.88f, 1f);
            Color skyMid = new Color(0.86f, 0.58f, 0.68f, 1f);
            Color skyHorizon = new Color(0.99f, 0.76f, 0.38f, 1f);
            Color waterDeep = new Color(0.08f, 0.28f, 0.52f, 1f);
            Color waterGold = new Color(0.96f, 0.74f, 0.32f, 1f);
            Color silhouetteCol = new Color(0.22f, 0.20f, 0.34f, 1f);
            Color stoneLit = new Color(0.96f, 0.86f, 0.68f, 1f);
            Color stoneShade = new Color(0.68f, 0.54f, 0.42f, 1f);
            Color roofDark = new Color(0.22f, 0.24f, 0.30f, 1f);
            Color windowGlow = new Color(1.0f, 0.90f, 0.42f, 1f);
            Color flagRed = new Color(0.92f, 0.16f, 0.18f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (y >= 32)
                    {
                        // Sunset sky gradient
                        float st = (y - 32f) / (h - 32f);
                        c = (st < 0.45f)
                            ? Color.Lerp(skyHorizon, skyMid, st / 0.45f)
                            : Color.Lerp(skyMid, skyTop, (st - 0.45f) / 0.55f);

                        // Distant Istanbul skyline & mosque minarets along horizon (y = 32..44)
                        float cityH = 35f + Mathf.Sin(x * 0.18f) * 3f + ((x > 105 && x < 130) ? 5f : 0f);
                        bool minaret1 = Mathf.Abs(x - 108f) <= 1.2f && y <= 50;
                        bool minaret2 = Mathf.Abs(x - 124f) <= 1.2f && y <= 48;
                        if (y <= cityH || minaret1 || minaret2)
                            c = silhouetteCol;
                    }
                    else
                    {
                        // Bosphorus water with golden sunset shimmer beneath Maiden's Tower (around x = 58)
                        float wt = y / 32f;
                        c = Color.Lerp(waterDeep, new Color(0.14f, 0.38f, 0.64f, 1f), wt);
                        float reflDist = Mathf.Abs(x - 58f);
                        if (reflDist < 22f && ((y + x) % 4 < 2))
                        {
                            c = Color.Lerp(c, waterGold, (1f - reflDist / 22f) * 0.75f);
                        }
                    }

                    // Maiden's Tower Islet & Base Walls (centered at x = 58, y = 26..36)
                    if (y >= 24 && y <= 29 && x >= 26 && x <= 96)
                    {
                        c = roofDark;
                    }
                    else if (y >= 29 && y <= 38 && x >= 34 && x <= 82)
                    {
                        bool win = (y >= 32 && y <= 35) && ((x - 38) % 9 < 4);
                        c = win ? windowGlow : ((x < 58) ? stoneLit : stoneShade);
                    }
                    // Main Tower Shaft (x = 49..67, y = 38..62)
                    else if (y >= 38 && y <= 62 && x >= 49 && x <= 67)
                    {
                        bool win = (y == 46 || y == 54) && Mathf.Abs(x - 58) <= 2;
                        c = win ? windowGlow : ((x < 58) ? stoneLit : stoneShade);
                    }
                    // Balcony & Cupola Dome (y = 62..74)
                    else if (y >= 62 && y <= 66 && x >= 46 && x <= 70)
                    {
                        c = roofDark;
                    }
                    else if (y > 66 && y <= 75 && Mathf.Abs(x - 58f) <= (76f - y) * 0.95f)
                    {
                        c = roofDark;
                    }
                    // Flagpole & Red Turkish Flag at top (y = 75..88)
                    else if (y > 75 && y <= 88 && Mathf.Abs(x - 58f) <= 1f)
                    {
                        c = Color.white;
                    }
                    else if (y >= 82 && y <= 87 && x >= 59 && x <= 66)
                    {
                        c = flagRed;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
