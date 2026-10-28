using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LazyTurtle
{
    /// <summary>Extra per-world data of the schoolyard (see <see cref="Yard.Info"/>).</summary>
    sealed class YardInfo
    {
        /// <summary>Per cell (y * Wd + x): true = pond water. Water does not block in World.Blocked, so Game must check it.</summary>
        public bool[] Water;
        public Entity Fountain, Captain, Ball;
        public double DropAcc;      // fractional fountain droplets carried to the next frame
    }

    /// <summary>
    /// The schoolyard of SP 2 Kórnik: grass, stone paths, a football pitch, a pond with swans, the lion fountain
    /// (as in Teofila's garden), peacocks, trees of the Arboretum and Kórnik Castle over the lake in the distance.
    /// The school facade with the door back inside is the south edge.
    /// </summary>
    static class Yard
    {
        public const int MapW = 44, MapH = 32;
        public const double FountainX = 37.5, FountainY = 25.5, PondX = 6.5, PondY = 25.5, PondR = 3.05;
        const double PitchX0 = 12, PitchY0 = 6, PitchX1 = 32, PitchY1 = 20;

        static readonly ConditionalWeakTable<World, YardInfo> infos = new ConditionalWeakTable<World, YardInfo>();

        sealed class Home
        {
            public double X, Y;
            public double Heading, Wait;
            public bool Pinned;     // driven by someone else (football match)
            public bool Stay;       // stands on its spot (the captain)
        }

        static readonly ConditionalWeakTable<Entity, Home> homes = new ConditionalWeakTable<Entity, Home>();
        static Tex[] drop;

        /// <summary>Yard data of a world built by <see cref="Build"/>; null for any other world.</summary>
        public static YardInfo Info(World w)
        {
            YardInfo i;
            return w != null && infos.TryGetValue(w, out i) ? i : null;
        }

        /// <summary>True when (x, y) is in a pond cell of a yard world. Game should treat it like a wall for walking.</summary>
        public static bool IsWater(World w, double x, double y)
        {
            var i = Info(w);
            if (i == null) return false;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            return w.InBounds(cx, cy) && i.Water[cy * w.Wd + cx];
        }

        /// <summary>Like World.Blocked, but water counts as solid too (corner test with radius r).</summary>
        public static bool BlockedOrWater(World w, double x, double y, double r)
        {
            return w.Blocked(x, y, r) || IsWater(w, x - r, y - r) || IsWater(w, x + r, y - r) || IsWater(w, x - r, y + r) || IsWater(w, x + r, y + r);
        }

        /// <summary>The football captain (the classmate named Chat.CaptainName).</summary>
        public static Entity Captain(World w)
        {
            var i = Info(w);
            if (i != null && i.Captain != null) return i.Captain;
            foreach (var e in w.Entities)
                if (e.Kind == Kind.Kid && e.Name == Chat.CaptainName) return e;
            return null;
        }

        /// <summary>Pinned kids are not moved by <see cref="Update"/> (Football pins the players during a match).</summary>
        public static void SetPinned(Entity kid, bool pinned)
        {
            Home h;
            if (kid != null && homes.TryGetValue(kid, out h)) h.Pinned = pinned;
        }

        public static World Build(Art art)
        {
            var w = new World(MapW, MapH) { Outdoor = true, Theme = School.Themes[0], Height = new float[MapW * MapH] };
            var info = new YardInfo { Water = new bool[MapW * MapH] };
            infos.Add(w, info);

            // ---- textures
            byte fence = w.AddWall(Art.FenceTex());
            byte facade = w.AddWall(Art.Facade());
            byte door = w.AddWall(Art.SchoolDoor());
            byte grass = w.AddFloor(Art.GrassTex());
            byte stone = w.AddFloor(Art.StonePathTex());
            byte water = w.AddFloor(Art.PondWater());
            w.AddCeil(Art.CeilingTile(false));      // no ceiling outdoors, but the software renderer needs index 0

            // ---- borders: fence around, school facade on the south edge
            w.Carve(0, 0, MapW - 1, MapH - 1, grass);
            for (int y = 0; y < MapH; y++)
                for (int x = 0; x < MapW; x++)
                {
                    int i = y * MapW + x;
                    if (y == MapH - 1)
                    {
                        w.SetWall(x, y, x == 22 ? door : facade);
                        w.Height[i] = 4.5f;
                    }
                    else if (x == 0 || y == 0 || x == MapW - 1)
                    {
                        w.SetWall(x, y, fence);
                        w.Height[i] = 1.2f;
                    }
                }
            w.SchoolDoorX = 22;
            w.SchoolDoorY = MapH - 1;

            // ---- stone paths: door -> pitch, branch east to the fountain plaza
            w.Carve(21, 21, 23, 30, stone);
            w.Carve(24, 24, 34, 26, stone);
            w.Carve(34, 22, 41, 29, stone);
            // pond
            for (int y = 1; y < MapH - 1; y++)
                for (int x = 1; x < MapW - 1; x++)
                {
                    double dx = x + 0.5 - PondX, dy = y + 0.5 - PondY;
                    if (dx * dx + dy * dy <= PondR * PondR)
                    {
                        w.Floor[y * MapW + x] = water;
                        info.Water[y * MapW + x] = true;
                    }
                }

            // ---- the pitch, goals and the ball
            w.Add(Kind.Decor, 22, 13, 0, 0, 0, null, 0).Model = "pitch";
            w.Add(Kind.Decor, PitchX0, 13, 0, 0, 0, null, 0).Model = "goal";
            var g2 = w.Add(Kind.Decor, PitchX1, 13, 0, 0, 0, null, 0);
            g2.Model = "goal";
            g2.Yaw = Math.PI;
            foreach (var gx in new[] { PitchX0, PitchX1 })
                foreach (var gy in new[] { 13 - 1.5, 13 + 1.5 })
                    w.Add(Kind.Decor, gx, gy, 0, 0, 0, null, 0).Radius = 0.1;     // invisible goal posts
            var ball = w.Add(Kind.Ball, 22, 13, 0, 0.26, 0.26, null, 0);
            ball.Model = "ball";
            info.Ball = ball;

            // ---- Kórnik Castle over the lake, behind the north fence
            var bd = w.Add(Kind.Decor, 22, -2, 0, 0, 0, null, 0);
            bd.Model = "backdrop";
            bd.Yaw = Math.PI / 2;

            // ---- lion fountain
            var f = w.Add(Kind.Decor, FountainX, FountainY, 0, 0, 0, null, 0);
            f.Model = "fountain";
            f.Radius = 1.7;
            info.Fountain = f;

            // ---- Arboretum trees inside the fence (north row leaves a gap for the castle view)
            var rnd = new Random(1777);
            var spots = new List<double[]>();
            for (double x = 2; x <= 42; x += 3.5)
                if (x < 16.5 || x > 27.5) spots.Add(new[] { x, 1.6 });
            for (double y = 5.1; y <= 28.5; y += 3.5)
            {
                spots.Add(new[] { 1.6, y });
                spots.Add(new[] { 42.4, y });
            }
            foreach (var s in spots)
            {
                if (IsWater(w, s[0], s[1]) || w.Floor[(int)s[1] * MapW + (int)s[0]] == stone) continue;
                int v = rnd.Next(3);
                var t = w.Add(Kind.Decor, s[0], s[1], 0, 1.6, 2.4, art.Decor[(int)Art.D.Plant], 0);
                t.Model = "tree" + v;
                t.Radius = 0.35;
                t.Scale = 0.85 + rnd.NextDouble() * 0.3;
                t.Yaw = rnd.NextDouble() * Math.PI * 2;
            }

            // ---- benches: along the facade (facing north) and by the pitch sidelines (facing the pitch)
            foreach (var bx in new[] { 16.5, 28.5, 31.5 })
                Bench(w, art, bx, 30.25, -Math.PI / 2, 0.6, 0.25);
            Bench(w, art, 10.6, 10.5, 0, 0.25, 0.6);
            Bench(w, art, 10.6, 15.5, 0, 0.25, 0.6);
            Bench(w, art, 33.4, 10.5, Math.PI, 0.25, 0.6);
            Bench(w, art, 33.4, 15.5, Math.PI, 0.25, 0.6);
            // plants by the facade, next to the door
            foreach (var px in new[] { 20.4, 24.6, 13.5, 34.5 })
            {
                var p = w.Add(Kind.Decor, px, 30.35, 0, 0.6, 0.85, art.Decor[(int)Art.D.Plant], 0);
                p.Model = "plant";
                p.Radius = 0.25;
            }
            // pitch sign, front towards the player coming from the door
            var sign = w.Add(Kind.Decor, 19.5, 21.5, 0, 0, 0, null, 0);
            sign.Model = "sign";
            sign.Yaw = Math.PI / 2;
            sign.Radius = 0.25;

            // ---- animals
            for (int i = 0; i < 2; i++)
            {
                var pc = w.Add(Kind.Animal, i == 0 ? 36.5 : 39.5, i == 0 ? 10.5 : 16.5, 0, 0, 0, null, 0);
                pc.Model = "peacock";
                pc.Name = "peacock";
                pc.Variant = i;
                pc.Radius = 0.3;
                pc.Dir = pc.Yaw = rnd.NextDouble() * Math.PI * 2;
                pc.Timer = 1 + i * 2;
            }
            for (int i = 0; i < 2; i++)
            {
                var sw = w.Add(Kind.Animal, i == 0 ? 5.5 : 7.5, i == 0 ? 24.8 : 26.5, 0, 0, 0, null, 0);
                sw.Model = "swan";
                sw.Name = "swan";
                sw.Dir = sw.Yaw = i * Math.PI;
            }

            // ---- classmates: captain + one more by the pitch, one by the fountain, one by the pond
            var kidSpots = new[] { new[] { 24.6, 21.3 }, new[] { 14.5, 21.4 }, new[] { 35.6, 22.6 }, new[] { 10.4, 25.5 } };
            for (int i = 0; i < 4; i++)
            {
                int v = i % 3;
                var k = w.Add(Kind.Kid, kidSpots[i][0], kidSpots[i][1], 0, 0.5, 0.58, art.Kid[v], 4);
                k.Model = "kid" + v;
                k.Variant = v;
                k.Name = Chat.KidName(i);
                k.Dir = 0;              // Game.Ambient's corridor walk does nothing with Dir = 0
                k.Timer = 1e9;          // ... and nothing at all while Timer > 0: Yard.Update moves the kids
                k.Yaw = Math.PI / 2;    // facing the school door
                if (i == 0)
                {
                    k.AnimRate = 0;     // the captain stands still and waits for a match
                    info.Captain = k;
                }
                homes.Add(k, new Home { X = k.X, Y = k.Y, Heading = i * 1.7, Stay = i == 0, Wait = i * 0.7 });
            }

            // ---- coins along the paths (some floating: jump!)
            for (int y = 22; y <= 28; y += 2)
                w.AddCoin(art, 22.5, y + 0.5, y % 4 == 0 ? 0.5 : 0.08);
            for (int x = 25; x <= 33; x += 2)
                w.AddCoin(art, x + 0.5, 25.5, x % 4 == 1 ? 0.5 : 0.08);
            w.AddCoin(art, 11.5, 22.5, 0.08);
            w.AddCoin(art, 4.5, 20.5, 0.5);
            w.AddCoin(art, 40.5, 4.5, 0.08);

            w.StartX = 22.5;
            w.StartY = 29.6;
            w.StartAngle = -Math.PI / 2;
            return w;
        }

        static void Bench(World w, Art art, double x, double y, double yaw, double hx, double hy)
        {
            var b = w.Add(Kind.Decor, x, y, 0, 1.0, 0.5, art.Decor[(int)Art.D.Bench], 0);
            b.Model = "bench";
            b.Yaw = yaw;
            b.HalfX = hx;
            b.HalfY = hy;
        }

        /// <summary>Animals, fountain droplets and classmates strolling around their spots. Call every frame in the yard.</summary>
        public static void Update(World w, double dt, double time, Random rnd, Art art)
        {
            var info = Info(w);
            if (info == null || dt <= 0) return;
            dt = Math.Min(dt, 0.1);

            for (int n = 0; n < w.Entities.Count; n++)
            {
                var e = w.Entities[n];
                if (!e.Alive) continue;
                if (e.Kind == Kind.Animal && e.Name == "peacock") Peacock(w, e, dt, rnd);
                else if (e.Kind == Kind.Animal && e.Name == "swan") Swan(w, e, dt, rnd);
                else if (e.Kind == Kind.Kid) Stroll(w, e, dt, rnd);
            }

            // fountain: ~25 droplets per second from the 4 lion mouths, arcing into the basin
            if (info.Fountain != null)
            {
                if (drop == null) drop = new[] { Art.WaterDrop() };
                info.DropAcc += dt * 25;
                while (info.DropAcc >= 1)
                {
                    info.DropAcc -= 1;
                    int m = rnd.Next(4);
                    double dx = m == 0 ? 1 : m == 1 ? -1 : 0, dy = m == 2 ? 1 : m == 3 ? -1 : 0;
                    double side = (rnd.NextDouble() - 0.5) * 0.25, s = 0.85 + rnd.NextDouble() * 0.35;
                    var d = w.Add(Kind.Sparkle, info.Fountain.X + dx * 0.35, info.Fountain.Y + dy * 0.35, 1.0, 0.07, 0.07, drop, 0);
                    d.Vx = dx * s - dy * side;
                    d.Vy = dy * s + dx * side;
                    d.Vz = 0.7 + rnd.NextDouble() * 0.5;
                    d.Life = 0.8 + rnd.NextDouble() * 0.2;
                }
            }
        }

        static bool InPitch(double x, double y, double margin) =>
            x > PitchX0 - margin && x < PitchX1 + margin && y > PitchY0 - margin && y < PitchY1 + margin;

        /// <summary>Somewhere an animal or a strolling kid may step.</summary>
        static bool Free(World w, double x, double y, double r)
        {
            if (BlockedOrWater(w, x, y, r)) return false;
            double fx = x - FountainX, fy = y - FountainY;
            if (fx * fx + fy * fy < 2.0 * 2.0) return false;
            if (y > MapH - 2.2) return false;      // keep the facade benches and the door free
            return true;
        }

        static void Peacock(World w, Entity e, double dt, Random rnd)
        {
            e.Timer -= dt;
            if (e.Variant == 1)
            {
                // tail fan open: stand still and show off
                if (e.Timer <= 0) { e.Variant = 0; e.Timer = 3 + rnd.NextDouble() * 4; }
                return;
            }
            if (e.Timer <= 0)
            {
                if (rnd.Next(3) == 0) { e.Variant = 1; e.Timer = 2.5 + rnd.NextDouble() * 3; return; }
                e.Dir += (rnd.NextDouble() - 0.5) * 2.0;
                e.Timer = 2 + rnd.NextDouble() * 3;
            }
            const double speed = 0.45;
            double nx = e.X + Math.Cos(e.Dir) * speed * dt, ny = e.Y + Math.Sin(e.Dir) * speed * dt;
            if (Free(w, nx, ny, 0.35) && !InPitch(nx, ny, 1.2))
            {
                e.X = nx;
                e.Y = ny;
            }
            else e.Dir += Math.PI * (0.5 + rnd.NextDouble());
            e.Yaw = e.Dir;
        }

        static void Swan(World w, Entity e, double dt, Random rnd)
        {
            const double speed = 0.3;
            if (rnd.NextDouble() < dt * 0.3) e.Dir += (rnd.NextDouble() - 0.5) * 1.5;
            double nx = e.X + Math.Cos(e.Dir) * speed * dt, ny = e.Y + Math.Sin(e.Dir) * speed * dt;
            double lx = nx + Math.Cos(e.Dir) * 0.45, ly = ny + Math.Sin(e.Dir) * 0.45;
            double px = lx - PondX, py = ly - PondY;
            if (IsWater(w, lx, ly) && px * px + py * py < (PondR - 0.4) * (PondR - 0.4))
            {
                e.X = nx;
                e.Y = ny;
            }
            else e.Dir += Math.PI * 0.4 + rnd.NextDouble() * 0.6;   // turn gently away from the shore
            e.Yaw = e.Dir;
        }

        static void Stroll(World w, Entity e, double dt, Random rnd)
        {
            Home h;
            if (!homes.TryGetValue(e, out h) || h.Pinned) return;
            double hx = h.X - e.X, hy = h.Y - e.Y, hd2 = hx * hx + hy * hy;
            if (h.Stay)
            {
                // the captain walks back to his spot after a match, then waits there
                if (hd2 < 0.05 * 0.05) { e.AnimRate = 0; e.Yaw = Math.PI / 2; return; }
                double step = Math.Min(Math.Sqrt(hd2), 1.2 * dt), a = Math.Atan2(hy, hx);
                e.X += Math.Cos(a) * step;
                e.Y += Math.Sin(a) * step;
                e.Yaw = a;
                e.AnimRate = 4;
                return;
            }
            if (h.Wait > 0)
            {
                h.Wait -= dt;
                e.AnimRate = 0;
                return;
            }
            if (hd2 > 4) h.Heading = Math.Atan2(hy, hx);      // too far from home: walk back
            else if (rnd.NextDouble() < dt * 0.5) h.Heading += (rnd.NextDouble() - 0.5) * 2;
            double speed = hd2 > 6 ? 1.2 : 0.6;
            double nx = e.X + Math.Cos(h.Heading) * speed * dt, ny = e.Y + Math.Sin(h.Heading) * speed * dt;
            if (Free(w, nx, ny, 0.25) && (!InPitch(nx, ny, 0) || InPitch(e.X, e.Y, 0)))
            {
                e.X = nx;
                e.Y = ny;
            }
            else h.Heading += Math.PI * (0.5 + rnd.NextDouble());
            e.Yaw = h.Heading;
            e.AnimRate = 4;
            if (hd2 < 4 && rnd.NextDouble() < dt * 0.25) h.Wait = 1.5 + rnd.NextDouble() * 3;
        }
    }
}
