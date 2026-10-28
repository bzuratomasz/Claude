using System;
using System.Collections.Generic;

namespace LazyTurtle
{
    enum FootballEvent { None, Kick, GoalUs, GoalThem, Finished }

    /// <summary>
    /// The ball in the schoolyard and a friendly match: the turtle and a teammate attack the EAST goal (x = 32),
    /// two opponents attack the WEST goal (x = 12). First to 3 goals or 2 minutes. The ball physics run always,
    /// so the ball can be kicked around for fun outside a match too.
    /// </summary>
    sealed class Football
    {
        public const double X0 = 12, Y0 = 6, X1 = 32, Y1 = 20, CenterY = 13, GoalHalf = 1.5, CrossBar = 1.2;
        public const int WinGoals = 3;
        public const double MatchTime = 120;

        const double CenterX = (X0 + X1) / 2;
        const double BallR = 0.13, Gravity = 9.5, Friction = 1.4;
        const double TouchDist = 0.42, KickDist = 0.6, KickSpeed = 7, KickLift = 2.2;
        const double KidR = 0.22, ResetPause = 1.5;

        readonly World world;
        readonly Random rnd = new Random();
        public Entity Ball;
        public readonly List<Entity> Kids = new List<Entity>();
        public bool Active;
        public int ScoreUs, ScoreThem;
        public double TimeLeft;
        /// <summary>Last match message ("GOL!") and how many seconds it stays on screen (counts down to 0).</summary>
        public string LastEvent;
        public double EventTime;
        public Entity Teammate;
        public Entity[] Opponents;

        double pause;           // > 0 after a goal: everyone waits, then kick-off
        bool finishPending;
        readonly Dictionary<Entity, double> cooldown = new Dictionary<Entity, double>();

        public Football(World yard)
        {
            world = yard;
            foreach (var e in yard.Entities)
            {
                if (e.Kind == Kind.Ball && Ball == null) Ball = e;
                else if (e.Kind == Kind.Kid) Kids.Add(e);
            }
        }

        public void Start(Entity teammate, Entity opp1, Entity opp2)
        {
            Teammate = teammate;
            Opponents = new[] { opp1, opp2 };
            foreach (var k in Players()) Yard.SetPinned(k, true);
            ScoreUs = ScoreThem = 0;
            TimeLeft = MatchTime;
            pause = 0;
            finishPending = false;
            LastEvent = L.Pl ? "Mecz! Gramy do 3 goli." : "Kick-off! First to 3 goals.";
            EventTime = 2.5;
            Active = true;
            KickOff(null);
        }

        public void Stop()
        {
            if (Opponents != null)
                foreach (var k in Players())
                {
                    k.AnimRate = 0;
                    Yard.SetPinned(k, false);
                }
            Active = false;
            pause = 0;
            finishPending = false;
        }

        IEnumerable<Entity> Players()
        {
            if (Teammate != null) yield return Teammate;
            if (Opponents != null)
                foreach (var o in Opponents)
                    if (o != null) yield return o;
        }

        /// <summary>Ball to the centre spot, everyone to the starting places (the player too, when given).</summary>
        void KickOff(Player p)
        {
            if (Ball != null)
            {
                Ball.X = CenterX;
                Ball.Y = CenterY;
                Ball.Z = Ball.Vx = Ball.Vy = Ball.Vz = 0;
            }
            if (p != null)
            {
                p.X = CenterX - 3.5;
                p.Y = CenterY;
                p.Angle = 0;
            }
            Place(Teammate, CenterX - 4.5, CenterY - 3, 0);
            if (Opponents != null)
            {
                Place(Opponents[0], CenterX + 3, CenterY, Math.PI);
                Place(Opponents[1], X1 - 2.5, CenterY, Math.PI);
            }
            cooldown.Clear();
        }

        static void Place(Entity k, double x, double y, double yaw)
        {
            if (k == null) return;
            k.X = x;
            k.Y = y;
            k.Yaw = yaw;
            k.AnimRate = 0;
        }

        /// <summary>
        /// One frame. playerVx/Vy = the turtle's velocity this frame (units/s); kickPressed = kick key went down.
        /// Returns what happened (Kick for the player's kick only, so Game can play a sound).
        /// </summary>
        public FootballEvent Update(double dt, Player p, double playerVx, double playerVy, bool kickPressed)
        {
            if (Ball == null || dt <= 0) return FootballEvent.None;
            dt = Math.Min(dt, 0.05);
            var ev = FootballEvent.None;
            if (EventTime > 0) EventTime -= dt;

            // ---- the player touches / kicks the ball
            if (p != null && pause <= 0)
            {
                double dx = Ball.X - p.X, dy = Ball.Y - p.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (kickPressed && d < KickDist && Ball.Z < 0.5)
                {
                    Ball.Vx = Math.Cos(p.Angle) * KickSpeed;
                    Ball.Vy = Math.Sin(p.Angle) * KickSpeed;
                    Ball.Vz = KickLift;
                    ev = FootballEvent.Kick;
                }
                else if (d < TouchDist && Ball.Z < 0.3)
                {
                    double nx = d > 1e-6 ? dx / d : Math.Cos(p.Angle), ny = d > 1e-6 ? dy / d : Math.Sin(p.Angle);
                    Ball.Vx = playerVx * 1.25 + nx * 1.2;
                    Ball.Vy = playerVy * 1.25 + ny * 1.2;
                    double bx = p.X + nx * TouchDist, by = p.Y + ny * TouchDist;
                    if (!BallBlocked(bx, by)) { Ball.X = bx; Ball.Y = by; }
                }
            }

            // ---- AI kids
            if (Active && pause <= 0)
            {
                TimeLeft -= dt;
                if (Teammate != null) Chase(Teammate, X1, 1.95, dt);
                if (Opponents != null)
                {
                    if (Opponents[0] != null) Chase(Opponents[0], X0, 1.75, dt);
                    if (Opponents[1] != null) Defend(Opponents[1], dt);
                }
                Separate();
            }
            else if (Active)
                foreach (var k in Players()) { k.AnimRate = 0; FaceBall(k); }

            var goal = BallPhysics(dt);

            if (Active)
            {
                if (pause > 0)
                {
                    pause -= dt;
                    if (pause <= 0)
                    {
                        if (finishPending) return Finish();
                        KickOff(p);
                    }
                }
                else if (goal != 0)
                {
                    bool us = goal > 0;
                    if (us) ScoreUs++;
                    else ScoreThem++;
                    LastEvent = (L.Pl ? "GOL! " : "GOAL! ") + (us ? UsName() : ThemName()) + "  " + ScoreUs + " : " + ScoreThem;
                    EventTime = 2.5;
                    pause = ResetPause;
                    finishPending = ScoreUs >= WinGoals || ScoreThem >= WinGoals;
                    return us ? FootballEvent.GoalUs : FootballEvent.GoalThem;
                }
                else if (TimeLeft <= 0)
                {
                    TimeLeft = 0;
                    return Finish();
                }
            }
            return ev;
        }

        FootballEvent Finish()
        {
            LastEvent = ScoreUs > ScoreThem ? (L.Pl ? "Koniec meczu! Wygrana!" : "Full time! You win!")
                : ScoreUs < ScoreThem ? (L.Pl ? "Koniec meczu!" : "Full time!")
                : (L.Pl ? "Koniec meczu! Remis!" : "Full time! A draw!");
            EventTime = 3;
            Stop();
            return FootballEvent.Finished;
        }

        /// <summary>+1 we won, 0 draw, -1 we lost (valid after Finished).</summary>
        public int Result => Math.Sign(ScoreUs - ScoreThem);

        // ------------------------------------------------------------------ ball

        bool BallBlocked(double x, double y) =>
            world.IsWall(x - BallR, y - BallR) || world.IsWall(x + BallR, y - BallR) || world.IsWall(x - BallR, y + BallR) || world.IsWall(x + BallR, y + BallR)
            || Yard.IsWater(world, x, y);

        /// <summary>Moves the ball; returns +1 / -1 when it went into the east / west goal during a match.</summary>
        int BallPhysics(double dt)
        {
            var b = Ball;
            double ox = b.X, oy = b.Y;

            // height: gravity and bounces
            if (b.Z > 0 || b.Vz > 0)
            {
                b.Vz -= Gravity * dt;
                b.Z += b.Vz * dt;
                if (b.Z <= 0)
                {
                    b.Z = 0;
                    b.Vz = b.Vz < -1.0 ? -b.Vz * 0.5 : 0;
                }
            }
            // rolling friction on the ground, a little air drag in flight
            double f = Math.Exp(-(b.Z > 0.01 ? 0.25 : Friction) * dt);
            b.Vx *= f;
            b.Vy *= f;
            double sp = Math.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy);
            if (sp < 0.03 && b.Z <= 0) { b.Vx = b.Vy = 0; sp = 0; }

            // move per axis, bouncing off walls, fence and water
            double nx = b.X + b.Vx * dt;
            if (BallBlocked(nx, b.Y)) b.Vx = -b.Vx * 0.6;
            else b.X = nx;
            double ny = b.Y + b.Vy * dt;
            if (BallBlocked(b.X, ny)) b.Vy = -b.Vy * 0.6;
            else b.Y = ny;

            // solid decorations (trees, fountain, benches, goal posts)
            foreach (var e in world.Entities)
            {
                if (!e.Alive || e.Kind != Kind.Decor) continue;
                double ex = b.X - e.X, ey = b.Y - e.Y;
                if (e.Radius > 0)
                {
                    double min = e.Radius + BallR, d2 = ex * ex + ey * ey;
                    if (d2 >= min * min || d2 < 1e-9) continue;
                    double d = Math.Sqrt(d2), ux = ex / d, uy = ey / d;
                    b.X = e.X + ux * min;
                    b.Y = e.Y + uy * min;
                    double vn = b.Vx * ux + b.Vy * uy;
                    if (vn < 0) { b.Vx -= 1.6 * vn * ux; b.Vy -= 1.6 * vn * uy; }
                }
                else if (e.HalfX > 0 && b.Z < 0.5)
                {
                    double hx = e.HalfX + BallR, hy = (e.HalfY > 0 ? e.HalfY : 0.3) + BallR;
                    if (Math.Abs(ex) >= hx || Math.Abs(ey) >= hy) continue;
                    double px = hx - Math.Abs(ex), py = hy - Math.Abs(ey);
                    if (px < py) { b.X += ex < 0 ? -px : px; b.Vx = -b.Vx * 0.6; }
                    else { b.Y += ey < 0 ? -py : py; b.Vy = -b.Vy * 0.6; }
                }
            }
            if (BallBlocked(b.X, b.Y)) { b.X = ox; b.Y = oy; }

            int goal = 0;
            if (Active)
            {
                bool mouth = Math.Abs(b.Y - CenterY) < GoalHalf - BallR && b.Z < CrossBar;
                if (pause > 0)
                {
                    // the ball rests in the net until kick-off
                    if (b.X < X0 - 0.75) { b.X = X0 - 0.75; b.Vx = 0; }
                    if (b.X > X1 + 0.75) { b.X = X1 + 0.75; b.Vx = 0; }
                }
                else if (b.X < X0)
                {
                    if (mouth && ox >= X0 - 0.2) goal = -1;
                    else if (ox >= X0) { b.X = X0; b.Vx = Math.Abs(b.Vx) * 0.5; }
                }
                else if (b.X > X1)
                {
                    if (mouth && ox <= X1 + 0.2) goal = 1;
                    else if (ox <= X1) { b.X = X1; b.Vx = -Math.Abs(b.Vx) * 0.5; }
                }
                if (pause <= 0 && goal == 0)
                {
                    if (b.Y < Y0) { b.Y = Y0; b.Vy = Math.Abs(b.Vy) * 0.5; }
                    else if (b.Y > Y1) { b.Y = Y1; b.Vy = -Math.Abs(b.Vy) * 0.5; }
                    // a ball that rolled outside the pitch before the match: bring it back over the goal line
                    if ((b.X < X0 - 0.2 || b.X > X1 + 0.2) && !mouth) b.X = b.X < X0 ? X0 : X1;
                }
                if (goal != 0) { b.Vx *= 0.3; b.Vy *= 0.3; }
            }

            // rolling animation: 3D renderer spins the ball by Phase around the motion direction
            if (sp > 0.05) b.Yaw = Math.Atan2(b.Vy, b.Vx);
            if (b.Z <= 0) b.Phase += sp * dt / BallR;
            return goal;
        }

        // ------------------------------------------------------------------ AI

        /// <summary>Runs behind the ball (seen from the target goal) and shoots when close.</summary>
        void Chase(Entity k, double goalX, double speed, double dt)
        {
            double gx = goalX - Ball.X, gy = CenterY - Ball.Y, gl = Math.Sqrt(gx * gx + gy * gy);
            if (gl < 1e-6) gl = 1;
            gx /= gl;
            gy /= gl;
            double tx = Ball.X - gx * 0.35, ty = Ball.Y - gy * 0.35;
            // in front of the ball: go round it instead of pushing it towards our own goal
            double kx = k.X - Ball.X, ky = k.Y - Ball.Y;
            if (kx * gx + ky * gy > 0.1 && kx * kx + ky * ky < 1.5 * 1.5)
            {
                double side = (kx * -gy + ky * gx) >= 0 ? 1 : -1;
                tx = Ball.X - gx * 0.6 - gy * side * 0.8;
                ty = Ball.Y - gy * 0.6 + gx * side * 0.8;
            }
            MoveTo(k, tx, ty, speed, dt);
            TryKick(k, goalX, dt);
        }

        /// <summary>The second opponent guards the east goal (the one they defend) and clears the ball westwards.</summary>
        void Defend(Entity k, double dt)
        {
            double bx = Ball.X, by = Ball.Y;
            double dx = bx - k.X, dy = by - k.Y;
            bool close = dx * dx + dy * dy < 2.2 * 2.2;
            if (bx > CenterX && close)
            {
                Chase(k, X0, 1.65, dt);
                return;
            }
            double tx, ty;
            if (bx > CenterX)
            {
                // between the ball and the goal centre, a little way out of the goal
                double gx = bx - X1, gy = by - CenterY, gl = Math.Sqrt(gx * gx + gy * gy);
                double r = Math.Min(2.2, Math.Max(0.6, gl - 0.8));
                tx = X1 + gx / Math.Max(gl, 1e-6) * r;
                ty = CenterY + gy / Math.Max(gl, 1e-6) * r;
            }
            else
            {
                tx = X1 - 5;
                ty = CenterY + (by - CenterY) * 0.4;
            }
            MoveTo(k, tx, ty, 1.65, dt);
            TryKick(k, X0, dt);
        }

        void TryKick(Entity k, double goalX, double dt)
        {
            double c;
            cooldown.TryGetValue(k, out c);
            c -= dt;
            cooldown[k] = c;
            double dx = Ball.X - k.X, dy = Ball.Y - k.Y;
            if (c > 0 || Ball.Z > 0.35 || dx * dx + dy * dy > 0.48 * 0.48) return;
            double ty = CenterY + (rnd.NextDouble() - 0.5) * 2.6;
            double ax = goalX - Ball.X, ay = ty - Ball.Y, al = Math.Sqrt(ax * ax + ay * ay);
            double a = Math.Atan2(ay, ax) + (rnd.NextDouble() - 0.5) * 0.35;
            // far from the goal: a softer pass forward, close: a real shot
            double s = al > 8 ? 3.8 + rnd.NextDouble() * 1.5 : 5 + rnd.NextDouble() * 1.5;
            Ball.Vx = Math.Cos(a) * s;
            Ball.Vy = Math.Sin(a) * s;
            Ball.Vz = 0.4 + rnd.NextDouble() * 1.2;
            cooldown[k] = 0.6;
        }

        void MoveTo(Entity k, double tx, double ty, double speed, double dt)
        {
            double dx = tx - k.X, dy = ty - k.Y, d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.05)
            {
                k.AnimRate = 0;
                FaceBall(k);
                return;
            }
            double step = Math.Min(d, speed * dt);
            double nx = k.X + dx / d * step, ny = k.Y + dy / d * step;
            nx = Math.Max(X0 + 0.3, Math.Min(X1 - 0.3, nx));
            ny = Math.Max(Y0 + 0.3, Math.Min(Y1 - 0.3, ny));
            if (!world.Blocked(nx, k.Y, KidR)) k.X = nx;
            if (!world.Blocked(k.X, ny, KidR)) k.Y = ny;
            k.Yaw = Math.Atan2(dy, dx);
            k.AnimRate = 6;
        }

        void FaceBall(Entity k) => k.Yaw = Math.Atan2(Ball.Y - k.Y, Ball.X - k.X);

        /// <summary>Keeps the AI kids from standing inside each other.</summary>
        void Separate()
        {
            var list = new List<Entity>(Players());
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    double dx = list[j].X - list[i].X, dy = list[j].Y - list[i].Y, d2 = dx * dx + dy * dy;
                    const double min = 0.5;
                    if (d2 >= min * min || d2 < 1e-9) continue;
                    double d = Math.Sqrt(d2), push = (min - d) / 2;
                    dx /= d;
                    dy /= d;
                    Nudge(list[i], -dx * push, -dy * push);
                    Nudge(list[j], dx * push, dy * push);
                }
        }

        void Nudge(Entity k, double dx, double dy)
        {
            double nx = Math.Max(X0 + 0.3, Math.Min(X1 - 0.3, k.X + dx)), ny = Math.Max(Y0 + 0.3, Math.Min(Y1 - 0.3, k.Y + dy));
            if (!world.Blocked(nx, ny, KidR)) { k.X = nx; k.Y = ny; }
        }

        // ------------------------------------------------------------------ texts

        string UsName() => (L.Pl ? "Ty i " : "You & ") + (Teammate != null ? Teammate.Name : "?");

        string ThemName() => Opponents == null ? "?" : (Opponents[0] != null ? Opponents[0].Name : "?") + (L.Pl ? " i " : " & ") + (Opponents[1] != null ? Opponents[1].Name : "?");

        public string ScoreText() => UsName() + " " + ScoreUs + " : " + ScoreThem + " " + ThemName();

        public string TimeText()
        {
            int s = (int)Math.Ceiling(Math.Max(0, TimeLeft));
            return s / 60 + ":" + (s % 60).ToString("00");
        }
    }
}
