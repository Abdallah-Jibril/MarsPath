using UnityEngine;

namespace MarsPath
{
    // Rocks and slopes use readable illustration thumbnails. Simple strokes
    // remain as a fallback and for life-support/general terrain symbols.
    internal static class MarsHazardIcons
    {
        internal enum Symbol { Rocks, Slope, LifeSupport, Terrain }

        private static bool picturesLoaded;
        private static Texture2D rockPicture,slopePicture,atlas;

        private static Texture2D Picture(Symbol symbol)
        {
            if(!picturesLoaded)
            {
                picturesLoaded=true;
                rockPicture=Resources.Load<Texture2D>("NASA/HudRockHazard");
                slopePicture=Resources.Load<Texture2D>("NASA/HudSlopeHazard");
                atlas=Resources.Load<Texture2D>("NASA/HudHazardAtlas");
                if(rockPicture==null)rockPicture=atlas;
                if(slopePicture==null)slopePicture=atlas;
            }
            return symbol==Symbol.Rocks?rockPicture:symbol==Symbol.Slope?slopePicture:null;
        }

        internal static bool HasPicture(Symbol symbol)=>Picture(symbol)!=null;

        internal static void Draw(Rect rect, Symbol symbol, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Texture2D picture=Picture(symbol);
            if(picture!=null)
            {
                // Preserve the image's rock/scree colours, rather than tinting
                // the whole thumbnail with the amber warning accent.
                GUI.color=new Color(1,1,1,previousColor.a);
                if(picture==atlas)
                {
                    // Atlas UVs select the two original transparent illustrations;
                    // no recoloring or destructive image edits are required.
                    Rect uv=symbol==Symbol.Rocks?new Rect(0,311f/1254,.5f,.5f):new Rect(.5f,303f/1254,.5f,.5f);
                    GUI.DrawTextureWithTexCoords(rect,picture,uv,true);
                }
                else GUI.DrawTexture(rect,picture,ScaleMode.ScaleToFit,true);
                GUI.color=previousColor;
                return;
            }
            GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0), Quaternion.identity,
                new Vector3(rect.width / 64f, rect.height / 64f, 1));
            GUI.color = color;
            switch (symbol)
            {
                case Symbol.Rocks:
                    Line(5, 49, 10, 29); Line(10, 29, 24, 20); Line(24, 20, 39, 27);
                    Line(39, 27, 44, 49); Line(44, 49, 5, 49);
                    Line(10, 29, 25, 34, 1.5f); Line(25, 34, 24, 20, 1.5f); Line(25, 34, 35, 49, 1.5f);
                    Line(40, 25, 48, 17); Line(48, 17, 58, 26); Line(58, 26, 60, 43);
                    Line(60, 43, 48, 43); Line(48, 43, 45, 31);
                    break;
                case Symbol.Slope:
                    Line(7, 51, 57, 15); Line(7, 51, 58, 51); Line(58, 51, 58, 15);
                    Line(37, 16, 37, 5, 3); Line(36, 23, 38, 23, 3);
                    Line(25, 50, 24, 45, 1.5f); Line(24, 45, 22, 41, 1.5f);
                    break;
                case Symbol.LifeSupport:
                    Line(19, 16, 19, 52); Line(19, 52, 24, 57); Line(24, 57, 43, 57);
                    Line(43, 57, 48, 52); Line(48, 52, 48, 16); Line(48, 16, 43, 11);
                    Line(43, 11, 24, 11); Line(24, 11, 19, 16);
                    Line(28, 11, 28, 5); Line(39, 11, 39, 5); Line(26, 5, 41, 5);
                    Line(33.5f, 25, 33.5f, 43, 4); Line(24.5f, 34, 42.5f, 34, 4);
                    break;
                default:
                    Line(32, 7, 59, 54); Line(59, 54, 5, 54); Line(5, 54, 32, 7);
                    Line(32, 23, 32, 37, 3); Line(31, 45, 33, 45, 3);
                    break;
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void Line(float x1, float y1, float x2, float y2, float width = 2.6f)
        {
            Vector2 delta = new Vector2(x2 - x1, y2 - y1);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(x1, y1, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.DrawTexture(new Rect(0, -width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = previous;
        }
    }
}
