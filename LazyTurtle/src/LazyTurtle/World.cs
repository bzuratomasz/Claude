using System;
using System.Collections.Generic;

namespace LazyTurtle
{
    enum Kind { Player, Teacher, Master, Kid, Decor, Gift, Marker, Coin, Sparkle, Ball, Animal }

    /// <summary>Anything drawn as a billboard sprite: people, furniture, gifts, coins and particles.</summary>
    sealed class Entity
    {
        public Kind Kind;
        public double X, Y, Z;          // Z = height of the sprite bottom above the floor
        public double W, H;             // size in world units (1 = one map cell)
        public double Radius;           // round obstacle (> 0 = solid)
        public double HalfX, HalfY;     // box obstacle (> 0 = solid); HalfY defaults to 0.3
        public Tex[] Frames;
        public double AnimRate, Phase;
        public bool Alive = true, Bob;
        public double Vx, Vy, Vz, Timer, Dir, Life;
        public double Dist;             // used by the renderer for sorting

        // ---- 3D (OpenGL renderer). Model = key for Models.Build (null = draw Frames as a billboard).
        // Models face +X at Yaw 0; Yaw uses the same convention as Player.Angle (0 = +X / east, PI/2 = +Y / south).
        public string Model;
        public double Yaw, Scale = 1;
        public int Variant;
        public string Name;             // classmates' names
    }

    sealed class Room
    {
        public int Index, Subject;
        public string Label;
        public string TeacherName;      // e.g. "Pani Zofia Kowalska" (different on every floor)
        public bool Done;
        public int X0, Y0, X1, Y1;
        public Entity Teacher, Gift, Marker;

        public bool Contains(double x, double y) => x >= X0 && x < X1 + 1 && y >= Y0 && y < Y1 + 1;
    }

    sealed class World
    {
        public const int FaceN = 0, FaceS = 1, FaceW = 2, FaceE = 3;

        public readonly int Wd, Ht;
        public readonly byte[] Wall, Floor, Ceil, Face;
        public readonly bool[] Explored;
        public readonly List<Tex> WallTex = new List<Tex> { null };
        public readonly List<Tex> FloorTex = new List<Tex>();
        public readonly List<Tex> CeilTex = new List<Tex>();
        public readonly List<Entity> Entities = new List<Entity>();
        public readonly List<Room> Rooms = new List<Room>();

        public int FloorNumber;
        public FloorTheme Theme;
        public double StartX, StartY, StartAngle;
        public Entity Master, MasterMarker;
        public int MasterDoorX, MasterDoorY, StairsX, StairsY;
        public byte MasterDoorFloor, StairsLockedTex, StairsOpenTex;
        public bool MasterDoorOpen, StairsOpen;

        // ---- outdoor areas and doors
        public bool Outdoor;            // no ceiling, sky and sun instead of lamps
        public float[] Height;          // per-cell wall height (null = Renderer.WallH everywhere)
        public int YardDoorX = -1, YardDoorY = -1;       // school lobby: always-open door to the yard
        public int SchoolDoorX = -1, SchoolDoorY = -1;   // yard: door back into the school

        public float WallHeight(int i) => Height != null && Height[i] > 0 ? Height[i] : (float)Renderer.WallH;

        public World(int w, int h)
        {
            Wd = w;
            Ht = h;
            Wall = new byte[w * h];
            Floor = new byte[w * h];
            Ceil = new byte[w * h];
            Face = new byte[w * h * 4];
            Explored = new bool[w * h];
        }

        public int GiftsDone
        {
            get
            {
                int n = 0;
                foreach (var r in Rooms) if (r.Done) n++;
                return n;
            }
        }

        public byte AddWall(Tex t)
        {
            WallTex.Add(t);
            if (WallTex.Count > 255) throw new InvalidOperationException("Too many wall textures");
            return (byte)(WallTex.Count - 1);
        }

        public byte AddFloor(Tex t)
        {
            FloorTex.Add(t);
            return (byte)(FloorTex.Count - 1);
        }

        public byte AddCeil(Tex t)
        {
            CeilTex.Add(t);
            return (byte)(CeilTex.Count - 1);
        }

        public void Carve(int x0, int y0, int x1, int y1, byte floor)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Wall[y * Wd + x] = 0;
                    Floor[y * Wd + x] = floor;
                }
        }

        public void SetWall(int x, int y, byte tex)
        {
            int i = y * Wd + x;
            Wall[i] = tex;
            for (int f = 0; f < 4; f++) Face[i * 4 + f] = 0;
        }

        public void SetFace(int x, int y, int face, byte tex)
        {
            if (InBounds(x, y)) Face[(y * Wd + x) * 4 + face] = tex;
        }

        public sealed class Painting
        {
            public double X, Y;     // point in front of the painting
            public int Kind;
        }

        public readonly List<Painting> Paintings = new List<Painting>();

        /// <summary>Hangs a painting on one face of a wall cell and remembers where it can be looked at.</summary>
        public void SetPainting(int x, int y, int face, byte tex, int kind)
        {
            SetFace(x, y, face, tex);
            double px = x + 0.5, py = y + 0.5;
            switch (face)
            {
                case FaceN: py -= 1; break;
                case FaceS: py += 1; break;
                case FaceW: px -= 1; break;
                default: px += 1; break;
            }
            Paintings.Add(new Painting { X = px, Y = py, Kind = kind });
        }

        /// <summary>Texture of the face a ray hit (face override or the cell's default texture).</summary>
        public Tex WallAt(int i, int face)
        {
            int f = Face[i * 4 + face];
            return WallTex[f != 0 ? f : Wall[i]];
        }

        public void OpenMasterDoor()
        {
            if (MasterDoorOpen) return;
            MasterDoorOpen = true;
            Carve(MasterDoorX, MasterDoorY, MasterDoorX, MasterDoorY, MasterDoorFloor);
        }

        public void OpenStairs()
        {
            StairsOpen = true;
            SetWall(StairsX, StairsY, StairsOpenTex);
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Wd && y < Ht;

        public bool IsWall(double x, double y)
        {
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            return !InBounds(cx, cy) || Wall[cy * Wd + cx] != 0;
        }

        public bool Blocked(double x, double y, double r)
        {
            return IsWall(x - r, y - r) || IsWall(x + r, y - r) || IsWall(x - r, y + r) || IsWall(x + r, y + r);
        }

        public Entity Add(Kind kind, double x, double y, double z, double w, double h, Tex[] frames, double animRate)
        {
            var e = new Entity { Kind = kind, X = x, Y = y, Z = z, W = w, H = h, Frames = frames, AnimRate = animRate, Phase = (x * 7 + y * 13) % 10 };
            Entities.Add(e);
            return e;
        }

        public void AddCoin(Art art, double x, double y, double z)
        {
            Add(Kind.Coin, x, y, z, 0.28, 0.28, art.Coin, 8);
        }
    }
}
