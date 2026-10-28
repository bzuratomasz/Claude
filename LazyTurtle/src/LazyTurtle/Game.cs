using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LazyTurtle
{
    enum GameScreen { Title, Floors, Help, Intro, Playing, Dialog, Paused, FloorDone, Victory }

    enum Phase { Ask, Correct, Wrong, Reward, Info }

    sealed class Player
    {
        public double X, Y, Angle, JumpZ, Vz, Bob;
        public bool Moving;
    }

    sealed class Popup
    {
        public string Text;
        public Color Color;
        public double Time;
        public float X;
    }

    /// <summary>A conversation with a teacher or the Master Turtle.</summary>
    sealed class Dialog
    {
        public bool IsMaster;
        public Room Room;
        public Question Q;
        public Phase Phase;
        public int Selected, Chosen = -1, Gift = -1;
        public string Line;
        public double Time;
        public Entity Kid;              // talking to a classmate (null = teacher / Master Turtle)
        public bool Offer, Riddle;      // classmate: football invitation / riddle for coins
        public string Speaker => Kid != null ? Kid.Name : IsMaster ? L.T("master") : (Room.TeacherName ?? School.TeacherName(Room.Subject)) + " - " + School.Subjects[Room.Subject].Name;
    }

    /// <summary>Game rules and state machine (menus, walking around the school, quizzes, floors).</summary>
    sealed class Game : IDisposable
    {
        const double Radius = 0.25;
        const double WalkSpeed = 2.3, RunSpeed = 3.6, TurnSpeed = 2.4;
        const double JumpVelocity = 2.8, Gravity = 9.5;
        const double CamDistance = 2.1, CamHeight = 1.05, Horizon = 0.34;

        public readonly Art Art = new Art();
        public readonly Sounds Snd = new Sounds();
        public readonly Speech Voice = new Speech();
        public readonly SaveData Save = SaveData.Load();
        public readonly Player P = new Player();
        public readonly List<Popup> Popups = new List<Popup>();
        readonly Random rnd = new Random();

        public World World;
        public int FloorIndex;
        public GameScreen Screen;
        public double Time, StateTime;
        public int Menu;
        public bool BigMap, ExitRequested;
        public string Message;
        public double MessageTime;
        public Dialog Dialog;
        public int Mistakes, LastStars;
        public Room NearRoom;
        public bool NearMaster;
        public World.Painting NearPainting;
        public double CamX, CamY, CamAngle, CamDist = CamDistance;
        public double CamPitch = 0.32;   // radians the 3D camera looks down (mouse adjustable)
        // 3D camera in OpenGL coordinates: x = map X, y = up, z = map Y
        public V3 CamEye, CamTarget;
        public bool DebugKeys;   // "--debug": F5 = go to the next teacher, F6 = go to the Master Turtle
        public bool InYard;              // outside on the schoolyard
        public Football Match;           // the yard's ball game (always there in the yard; Active during a match)
        public Entity NearKid;
        public int LanguageVersion;      // bumped when the language changes (the 3D renderer rebuilds text textures)
        World schoolWorld;               // the floor we came from, kept while playing outside
        double camWant = CamDistance, moveVx, moveVy;
        Entity playerSprite;
        int debugPainting;

        public Game()
        {
            L.Pl = Save.Lang != "en";
            Snd.Enabled = Save.Sound;
            Voice.Enabled = Save.Speech;
            LoadFloor(0);
            Screen = GameScreen.Title;
        }

        public void Dispose()
        {
            Snd.Dispose();
            Voice.Dispose();
        }

        public FloorTheme Theme => School.Themes[FloorIndex];
        public bool CanTalk => NearRoom != null || NearMaster || NearKid != null;

        public string[] TitleItems => new[]
        {
            L.T("play"), L.T("floors"), L.T("help"), L.T("lang"), L.T(Snd.Enabled ? "sound_on" : "sound_off"), SpeechLabel, L.T("exit"),
        };

        public string[] PauseItems => new[]
        {
            L.T("resume"), L.T("restart"), L.T(Snd.Enabled ? "sound_on" : "sound_off"), SpeechLabel, L.T("menu"),
        };

        string SpeechLabel => !Voice.Available ? L.T("speech_none") : L.T(Voice.Enabled ? "speech_on" : "speech_off");

        // ------------------------------------------------------------------ floors

        void LoadFloor(int floor)
        {
            FloorIndex = floor;
            InYard = false;
            Match = null;
            schoolWorld = null;
            World = School.Build(floor, Art, Save.Classes[floor], Save.Master[floor]);
            playerSprite = World.Add(Kind.Player, 0, 0, 0, 0.6, 0.57, Art.PlayerIdle, 0);
            P.X = World.StartX;
            P.Y = World.StartY;
            P.Angle = World.StartAngle;
            P.JumpZ = P.Vz = 0;
            CamAngle = P.Angle;
            CamDist = camWant;
            UpdateCamera(1);
        }

        /// <summary>Through the always-open door in the lobby out to the schoolyard.</summary>
        void GoToYard()
        {
            schoolWorld = World;
            World = Yard.Build(Art);
            playerSprite = World.Add(Kind.Player, 0, 0, 0, 0.6, 0.57, Art.PlayerIdle, 0);
            P.X = World.StartX;
            P.Y = World.StartY;
            P.Angle = World.StartAngle;
            P.JumpZ = P.Vz = 0;
            CamAngle = P.Angle;
            CamDist = 0.5;
            InYard = true;
            Match = new Football(World);
            BigMap = false;
            Snd.Play("door");
            ShowMessage(L.T("yard_hello"), 6);
        }

        void BackToSchool()
        {
            if (Match != null) Match.Stop();
            Match = null;
            InYard = false;
            if (schoolWorld == null) LoadFloor(FloorIndex);
            else
            {
                World = schoolWorld;
                schoolWorld = null;
                playerSprite = World.Entities.Find(e => e.Kind == Kind.Player);
            }
            P.X = 1.7;
            P.Y = 10.5;
            P.Angle = 0;
            P.JumpZ = P.Vz = 0;
            CamAngle = 0;
            CamDist = 0.4;
            Snd.Play("door");
            ShowMessage(L.T("back_school"), 3);
        }

        void StartFloor(int floor)
        {
            if (Save.Master[floor])
            {
                // replaying a finished floor starts it fresh
                Save.Master[floor] = false;
                Save.Classes[floor] = 0;
                Save.Write();
            }
            LoadFloor(floor);
            Mistakes = 0;
            Popups.Clear();
            Message = null;
            BigMap = false;
            SetScreen(GameScreen.Intro);
        }

        int ContinueFloor()
        {
            for (int f = 0; f < Save.Unlocked; f++)
                if (!Save.Master[f]) return f;
            return Save.Unlocked >= School.Floors ? 0 : Save.Unlocked - 1;
        }

        void SetScreen(GameScreen s)
        {
            Screen = s;
            StateTime = 0;
        }

        void ShowMessage(string text, double seconds = 3.5)
        {
            Message = text;
            MessageTime = seconds;
        }

        void AddPopup(string text, Color color)
        {
            Popups.Add(new Popup { Text = text, Color = color, X = 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.1f });
        }

        void ToggleLanguage()
        {
            L.Pl = !L.Pl;
            Save.Lang = L.Pl ? "pl" : "en";
            Save.Write();
            LanguageVersion++;
            if (InYard)
            {
                schoolWorld = null;   // rebuilt with the new language when going back in
                return;
            }
            double x = P.X, y = P.Y, a = P.Angle;
            LoadFloor(FloorIndex);   // signs and banners contain text
            P.X = x;
            P.Y = y;
            P.Angle = a;
        }

        void ToggleSound()
        {
            Snd.Enabled = !Snd.Enabled;
            Save.Sound = Snd.Enabled;
            Save.Write();
            Snd.Play("select");
        }

        void ToggleSpeech()
        {
            if (!Voice.Available) return;
            Voice.Enabled = !Voice.Enabled;
            Save.Speech = Voice.Enabled;
            Save.Write();
            if (!Voice.Enabled) Voice.Stop();
        }

        /// <summary>Right mouse button held: turn the turtle and tilt the camera.</summary>
        public void MouseLook(double dx, double dy)
        {
            if (Screen != GameScreen.Playing) return;
            P.Angle += dx * 0.006;
            CamAngle += dx * 0.006;
            CamPitch = Math.Max(0.05, Math.Min(1.0, CamPitch + dy * 0.004));
        }

        /// <summary>Mouse wheel: camera closer / farther.</summary>
        public void Zoom(int wheelDelta)
        {
            camWant = Math.Max(1.2, Math.Min(4.0, camWant - wheelDelta / 120.0 * 0.25));
        }

        public void Play(int floor) => StartFloor(Math.Max(0, Math.Min(School.Floors - 1, floor)));

        public void Pause()
        {
            if (Screen != GameScreen.Playing) return;
            Menu = 0;
            SetScreen(GameScreen.Paused);
        }

        // ------------------------------------------------------------------ update

        public void Update(double dt, Input inp)
        {
            Time += dt;
            StateTime += dt;
            if (MessageTime > 0) MessageTime -= dt;
            foreach (var p in Popups) p.Time += dt;
            Popups.RemoveAll(p => p.Time > 1.5);

            switch (Screen)
            {
                case GameScreen.Title: UpdateTitle(dt, inp); break;
                case GameScreen.Floors: UpdateFloorSelect(dt, inp); break;
                case GameScreen.Help:
                    Orbit(dt);
                    if (inp.Back || inp.Accept || inp.Click >= 0) { Snd.Play("click"); Menu = 2; SetScreen(GameScreen.Title); }
                    break;
                case GameScreen.Intro:
                    Ambient(dt);
                    UpdateCamera(dt);
                    if (StateTime > 6 || (StateTime > 0.4 && (inp.Accept || inp.Click >= 0)))
                    {
                        SetScreen(GameScreen.Playing);
                        ShowMessage(L.T("find_class"), 5);
                    }
                    break;
                case GameScreen.Playing: UpdatePlaying(dt, inp); break;
                case GameScreen.Dialog: UpdateDialog(dt, inp); break;
                case GameScreen.Paused: UpdatePaused(inp); break;
                case GameScreen.FloorDone:
                    Ambient(dt);
                    if (StateTime > 1 && (inp.Accept || inp.Click >= 0))
                    {
                        Snd.Play("select");
                        if (FloorIndex + 1 < School.Floors) StartFloor(FloorIndex + 1);
                        else SetScreen(GameScreen.Victory);
                    }
                    break;
                case GameScreen.Victory:
                    Orbit(dt);
                    if (StateTime > 1.5 && (inp.Accept || inp.Back || inp.Click >= 0)) { Menu = 0; LoadFloor(0); SetScreen(GameScreen.Title); }
                    break;
            }
        }

        void Orbit(double dt)
        {
            P.Angle += dt * 0.15;
            Ambient(dt);
            UpdateCamera(dt);
        }

        bool MenuNav(Input inp, int count)
        {
            if (inp.Up) { Menu = (Menu + count - 1) % count; Snd.Play("click"); }
            if (inp.DownKey) { Menu = (Menu + 1) % count; Snd.Play("click"); }
            if (inp.Click >= 100 && inp.Click < 100 + count) { Menu = inp.Click - 100; Snd.Play("select"); return true; }
            if (inp.Pressed(Keys.Enter, Keys.Space)) { Snd.Play("select"); return true; }
            return false;
        }

        void UpdateTitle(double dt, Input inp)
        {
            Orbit(dt);
            if (!MenuNav(inp, TitleItems.Length)) return;
            switch (Menu)
            {
                case 0: StartFloor(ContinueFloor()); break;
                case 1: Menu = Math.Min(Save.Unlocked, School.Floors) - 1; SetScreen(GameScreen.Floors); break;
                case 2: SetScreen(GameScreen.Help); break;
                case 3: ToggleLanguage(); break;
                case 4: ToggleSound(); break;
                case 5: ToggleSpeech(); break;
                case 6: ExitRequested = true; break;
            }
        }

        void UpdateFloorSelect(double dt, Input inp)
        {
            Orbit(dt);
            int n = School.Floors, perRow = (n + 1) / 2;
            if (inp.Left) { Menu = (Menu + n - 1) % n; Snd.Play("click"); }
            if (inp.Right) { Menu = (Menu + 1) % n; Snd.Play("click"); }
            if (inp.Up || inp.DownKey) { Menu = (Menu + perRow) % n; Snd.Play("click"); }
            if (inp.Back) { Snd.Play("click"); Menu = 1; SetScreen(GameScreen.Title); return; }
            bool go = inp.Accept;
            if (inp.Click >= 200 && inp.Click < 200 + n) { Menu = inp.Click - 200; go = true; }
            if (!go) return;
            if (Menu < Save.Unlocked) { Snd.Play("select"); StartFloor(Menu); }
            else Snd.Play("bump");
        }

        void UpdatePaused(Input inp)
        {
            if (inp.Back || inp.Pressed(Keys.P)) { SetScreen(GameScreen.Playing); return; }
            if (!MenuNav(inp, PauseItems.Length)) return;
            switch (Menu)
            {
                case 0: SetScreen(GameScreen.Playing); break;
                case 1:
                    Save.Classes[FloorIndex] = 0;
                    Save.Master[FloorIndex] = false;
                    Save.Write();
                    StartFloor(FloorIndex);
                    break;
                case 2: ToggleSound(); break;
                case 3: ToggleSpeech(); break;
                case 4: Menu = 0; LoadFloor(0); SetScreen(GameScreen.Title); break;
            }
        }

        void UpdatePlaying(double dt, Input inp)
        {
            if (inp.Pressed(Keys.Escape, Keys.P)) { Pause(); return; }
            if (inp.Pressed(Keys.Tab, Keys.M)) BigMap = !BigMap;
            if (DebugKeys && !InYard) DebugTeleport(inp);

            double turn = (inp.Down(Keys.Right, Keys.D) ? 1 : 0) - (inp.Down(Keys.Left, Keys.A) ? 1 : 0);
            P.Angle += turn * TurnSpeed * dt;
            double fwd = (inp.Down(Keys.Up, Keys.W) ? 1 : 0) - (inp.Down(Keys.Down, Keys.S) ? 0.7 : 0);
            double strafe = (inp.Down(Keys.E) ? 1 : 0) - (inp.Down(Keys.Q) ? 1 : 0);
            P.Moving = fwd != 0 || strafe != 0;
            double speed = inp.Down(Keys.ShiftKey) ? RunSpeed : WalkSpeed;
            moveVx = moveVy = 0;
            if (P.Moving)
            {
                double dx = Math.Cos(P.Angle), dy = Math.Sin(P.Angle);
                double mx = dx * fwd - dy * strafe, my = dy * fwd + dx * strafe;
                double len = Math.Sqrt(mx * mx + my * my);
                if (len > 1) { mx /= len; my /= len; }
                double bx = P.X, by = P.Y;
                MovePlayer(mx * speed * dt, my * speed * dt);
                if (dt > 0) { moveVx = (P.X - bx) / dt; moveVy = (P.Y - by) / dt; }
                P.Bob += dt * speed * 4;
            }

            FindNearby();
            bool talk = CanTalk && (inp.Pressed(Keys.Enter, Keys.Space) || inp.Click == 20);
            if (talk)
            {
                StartDialog();
                return;
            }
            bool nearBall = InYard && Match != null && Match.Ball != null &&
                            Dist(P.X, P.Y, Match.Ball.X, Match.Ball.Y) < 0.9;
            bool kick = nearBall && inp.Pressed(Keys.Space);
            if (inp.Pressed(Keys.Space) && P.JumpZ <= 0 && !nearBall)
            {
                P.Vz = JumpVelocity;
                Snd.Play("jump");
            }
            if (P.JumpZ > 0 || P.Vz > 0)
            {
                P.Vz -= Gravity * dt;
                P.JumpZ += P.Vz * dt;
                if (P.JumpZ <= 0) { P.JumpZ = 0; P.Vz = 0; }
            }

            Explore();
            Ambient(dt);
            UpdateEntities();
            if (InYard) UpdateYard(dt, kick);
            UpdateCamera(dt);
            CheckDoors();
        }

        void UpdateYard(double dt, bool kick)
        {
            Yard.Update(World, dt, Time, rnd, Art);
            if (Match == null) return;
            var ev = Match.Update(dt, P, moveVx, moveVy, kick);
            switch (ev)
            {
                case FootballEvent.Kick: Snd.Play("jump"); break;
                case FootballEvent.GoalUs:
                    Snd.Play("win");
                    AddPopup(L.Pl ? "GOL!" : "GOAL!", Color.FromArgb(255, 235, 80));
                    break;
                case FootballEvent.GoalThem:
                    Snd.Play("wrong");
                    break;
                case FootballEvent.Finished:
                    int coins = Match.ScoreUs > Match.ScoreThem ? 10 : Match.ScoreUs == Match.ScoreThem ? 5 : 3;
                    Save.Coins += coins;
                    Save.Write();
                    Snd.Play(coins == 10 ? "gift" : "coin");
                    ShowMessage(Match.ScoreUs > Match.ScoreThem ? Chat.MatchWon : Match.ScoreUs == Match.ScoreThem ? Chat.MatchDraw : Chat.MatchLost, 6);
                    AddPopup(L.F("coins_plus", coins), Color.FromArgb(255, 230, 90));
                    break;
            }
        }

        void FindNearby()
        {
            NearRoom = null;
            NearMaster = false;
            NearKid = null;
            double bestKid = 1.5;
            foreach (var e in World.Entities)
            {
                if (e.Kind != Kind.Kid || !e.Alive) continue;
                if (Match != null && Match.Active && (e == Match.Teammate || Array.IndexOf(Match.Opponents ?? new Entity[0], e) >= 0)) continue;
                double d = Dist(P.X, P.Y, e.X, e.Y);
                if (d < bestKid) { bestKid = d; NearKid = e; }
            }
            foreach (var r in World.Rooms)
                if (r.Contains(P.X, P.Y) && Dist(P.X, P.Y, r.Teacher.X, r.Teacher.Y) < 2.1) NearRoom = r;
            if (World.Master != null && Dist(P.X, P.Y, World.Master.X, World.Master.Y) < 2.4) NearMaster = true;
            if (NearRoom != null || NearMaster) NearKid = null;   // teachers first
            NearPainting = null;
            double best = 1.4;
            foreach (var p in World.Paintings)
            {
                double d = Dist(P.X, P.Y, p.X, p.Y);
                if (d < best) { best = d; NearPainting = p; }
            }
        }

        static double Dist(double x0, double y0, double x1, double y1) => Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));

        void CheckDoors()
        {
            if (InYard)
            {
                if (World.SchoolDoorX >= 0 && Dist(P.X, P.Y, World.SchoolDoorX + 0.5, World.SchoolDoorY + 0.5) < 1.0) BackToSchool();
                return;
            }
            if (World.YardDoorX >= 0 && Dist(P.X, P.Y, World.YardDoorX + 0.5, World.YardDoorY + 0.5) < 1.0)
            {
                GoToYard();
                return;
            }
            if (!World.MasterDoorOpen && Dist(P.X, P.Y, World.MasterDoorX + 0.5, World.MasterDoorY + 0.5) < 0.95 && MessageTime < 1)
            {
                ShowMessage(L.F("need_gifts", World.GiftsDone));
                Snd.Play("bump");
            }
            if (Dist(P.X, P.Y, World.StairsX + 0.5, World.StairsY + 0.5) < 0.95)
            {
                if (World.StairsOpen) FinishFloor();
                else if (MessageTime < 1) ShowMessage(L.T("stairs_locked"));
            }
        }

        void FinishFloor()
        {
            LastStars = Mistakes <= 2 ? 3 : Mistakes <= 5 ? 2 : 1;
            Save.Stars[FloorIndex] = Math.Max(Save.Stars[FloorIndex], LastStars);
            Save.Write();
            Snd.Play("win");
            SetScreen(GameScreen.FloorDone);
        }

        void MovePlayer(double dx, double dy)
        {
            double ox = P.X, oy = P.Y;
            if (!Blocked(P.X + dx, P.Y)) P.X += dx;
            if (!Blocked(P.X, P.Y + dy)) P.Y += dy;

            foreach (var e in World.Entities)
            {
                if (!e.Alive) continue;
                double ex = P.X - e.X, ey = P.Y - e.Y;
                if (e.Radius > 0)
                {
                    double min = e.Radius + Radius, d2 = ex * ex + ey * ey;
                    if (d2 >= min * min || d2 < 1e-9) continue;
                    double d = Math.Sqrt(d2);
                    P.X = e.X + ex / d * min;
                    P.Y = e.Y + ey / d * min;
                }
                else if (e.HalfX > 0)
                {
                    double hx = e.HalfX + Radius, hy = (e.HalfY > 0 ? e.HalfY : 0.3) + Radius;
                    if (Math.Abs(ex) >= hx || Math.Abs(ey) >= hy) continue;
                    double px = hx - Math.Abs(ex), py = hy - Math.Abs(ey);
                    if (px < py) P.X += ex < 0 ? -px : px;
                    else P.Y += ey < 0 ? -py : py;
                }
            }
            if (Blocked(P.X, P.Y)) { P.X = ox; P.Y = oy; }
        }

        bool Blocked(double x, double y)
        {
            if (World.Blocked(x, y, Radius)) return true;
            return InYard && (Yard.IsWater(World, x - Radius, y - Radius) || Yard.IsWater(World, x + Radius, y - Radius) ||
                              Yard.IsWater(World, x - Radius, y + Radius) || Yard.IsWater(World, x + Radius, y + Radius));
        }

        /// <summary>Third-person camera behind the turtle; it slides closer instead of going through walls.</summary>
        void UpdateCamera(double dt)
        {
            double diff = Math.IEEERemainder(P.Angle - CamAngle, Math.PI * 2);
            CamAngle += diff * Math.Min(1, dt * 8);
            double bx = -Math.Cos(CamAngle), by = -Math.Sin(CamAngle);
            double d = 0.2;
            while (d < camWant && !World.Blocked(P.X + bx * (d + 0.05), P.Y + by * (d + 0.05), 0.18)) d += 0.05;
            if (d < CamDist) CamDist = d;
            else CamDist += (d - CamDist) * Math.Min(1, dt * 4);
            CamX = P.X + bx * CamDist;
            CamY = P.Y + by * CamDist;
            double lift = Math.Sin(CamPitch) * CamDist;
            CamEye = new V3(CamX, 0.55 + P.JumpZ * 0.5 + lift, CamY);
            CamTarget = new V3(P.X - bx * 0.6, 0.42 + P.JumpZ * 0.6, P.Y - by * 0.6);

            playerSprite.X = P.X;
            playerSprite.Y = P.Y;
            playerSprite.Z = P.JumpZ;
            playerSprite.Alive = CamDist > 0.75;
            if (P.JumpZ > 0) { playerSprite.Frames = Art.PlayerJump; playerSprite.AnimRate = 0; }
            else if (P.Moving && Screen == GameScreen.Playing) { playerSprite.Frames = Art.PlayerWalk; playerSprite.AnimRate = 9; }
            else { playerSprite.Frames = Art.PlayerIdle; playerSprite.AnimRate = 0; }
        }

        public double CamZ => CamHeight + P.JumpZ * 0.4;

        void Explore()
        {
            int cx = (int)P.X, cy = (int)P.Y;
            const int R = 4;
            for (int y = cy - R; y <= cy + R; y++)
                for (int x = cx - R; x <= cx + R; x++)
                    if (World.InBounds(x, y)) World.Explored[y * World.Wd + x] = true;
        }

        /// <summary>Animations that also run behind menus: classmates walking and sparkles.</summary>
        void Ambient(double dt)
        {
            foreach (var e in World.Entities)
            {
                if (!e.Alive) continue;
                if (e.Kind == Kind.Kid && !InYard)
                {
                    if (e.Timer > 0) { e.Timer -= dt; e.AnimRate = 0; continue; }
                    e.AnimRate = 4;
                    e.X += e.Dir * 0.7 * dt;
                    if (e.X < 2 || e.X > 39) e.Dir = -e.Dir;
                    e.Yaw = e.Dir > 0 ? 0 : Math.PI;
                    if (rnd.NextDouble() < dt * 0.15) e.Timer = 1 + rnd.NextDouble() * 2;
                }
                else if (e.Kind == Kind.Sparkle)
                {
                    e.X += e.Vx * dt;
                    e.Y += e.Vy * dt;
                    e.Vz -= 4 * dt;
                    e.Z = Math.Max(0, e.Z + e.Vz * dt);
                    e.Life -= dt;
                    if (e.Life <= 0) e.Alive = false;
                }
            }
            World.Entities.RemoveAll(e => e.Kind == Kind.Sparkle && !e.Alive);
        }

        void UpdateEntities()
        {
            for (int i = 0; i < World.Entities.Count; i++)
            {
                var e = World.Entities[i];
                if (!e.Alive || e.Kind != Kind.Coin) continue;
                if (Dist(P.X, P.Y, e.X, e.Y) < 0.5 && (e.Z < 0.3 || P.JumpZ > 0.18))
                {
                    e.Alive = false;
                    Save.Coins++;
                    Sparkles(e.X, e.Y, e.Z + 0.15, 4);
                    Snd.Play("coin");
                }
            }
        }

        void Sparkles(double x, double y, double z, int count)
        {
            for (int i = 0; i < count; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, s = 0.8 + rnd.NextDouble() * 1.2;
                var p = World.Add(Kind.Sparkle, x, y, z, 0.08, 0.08, Art.Sparkle, 0);
                p.Vx = Math.Cos(a) * s;
                p.Vy = Math.Sin(a) * s;
                p.Vz = 1 + rnd.NextDouble() * 1.5;
                p.Life = 0.5 + rnd.NextDouble() * 0.3;
            }
        }

        // ------------------------------------------------------------------ quiz dialogs

        void StartDialog()
        {
            P.Moving = false;
            UpdateCamera(0);
            var d = new Dialog { IsMaster = NearMaster && NearRoom == null, Room = NearRoom };
            if (NearKid != null && NearRoom == null && !NearMaster)
            {
                StartKidDialog(d, NearKid);
                Dialog = d;
                Snd.Play("talk");
                SetScreen(GameScreen.Dialog);
                return;
            }
            if (d.IsMaster)
            {
                if (Save.Master[FloorIndex]) SetLine(d, Phase.Info, L.T("master_already"));
                else AskNew(d);
            }
            else if (d.Room.Done) SetLine(d, Phase.Info, L.T("already"));
            else AskNew(d);
            Dialog = d;
            Snd.Play("talk");
            SetScreen(GameScreen.Dialog);
        }

        void StartKidDialog(Dialog d, Entity kid)
        {
            d.Kid = kid;
            kid.Timer = 4;                      // stop walking and look at us
            kid.AnimRate = 0;
            kid.Yaw = Math.Atan2(P.Y - kid.Y, P.X - kid.X);
            if (InYard && Match != null && !Match.Active && kid == Yard.Captain(World))
            {
                var others = World.Entities.FindAll(e => e.Kind == Kind.Kid && e != kid);
                others.Sort((a, b) => Dist(a.X, a.Y, 22, 13).CompareTo(Dist(b.X, b.Y, 22, 13)));
                if (others.Count >= 2)
                {
                    d.Offer = true;
                    d.Q = new Question { Text = Chat.FootballOffer(kid.Name, others[0].Name, others[1].Name), Answers = new[] { Chat.Yes, Chat.No }, Correct = 0 };
                    SetLine(d, Phase.Ask, Chat.Greeting(kid.Name));
                    return;
                }
            }
            if (rnd.Next(2) == 0)
            {
                d.Riddle = true;
                d.Q = Chat.Riddle(rnd);
                SetLine(d, Phase.Ask, Chat.Greeting(kid.Name) + " " + Chat.RiddleIntro);
            }
            else SetLine(d, Phase.Info, Chat.Greeting(kid.Name) + " " + Chat.Line(rnd, InYard));
        }

        void AskNew(Dialog d)
        {
            d.Q = d.IsMaster ? Questions.Master(FloorIndex, rnd) : Questions.Make(d.Room.Subject, FloorIndex + 1, rnd);
            d.Selected = 0;
            d.Chosen = -1;
            SetLine(d, Phase.Ask, L.T(d.IsMaster ? "master_greet" : "greet"));
        }

        void SetLine(Dialog d, Phase phase, string line)
        {
            d.Phase = phase;
            d.Line = line;
            d.Time = 0;
            ReadAloud(d);
        }

        void ReadAloud(Dialog d)
        {
            if (d.Phase != Phase.Ask) { Voice.Say(d.Line); return; }
            string text = d.Line + " " + d.Q.Text + " ";
            for (int i = 0; i < d.Q.Answers.Length; i++) text += (i + 1) + ": " + d.Q.Answers[i] + ". ";
            Voice.Say(text);
        }

        void UpdateDialog(double dt, Input inp)
        {
            var d = Dialog;
            d.Time += dt;
            Ambient(dt);
            if (inp.Back)
            {
                Voice.Stop();
                SetScreen(GameScreen.Playing);
                return;
            }
            if (inp.Pressed(Keys.R) || inp.Click == 11) ReadAloud(d);

            if (d.Phase == Phase.Ask)
            {
                int n = d.Q.Answers.Length;
                if (inp.Left) d.Selected = (d.Selected + n - 1) % n;
                if (inp.Right) d.Selected = (d.Selected + 1) % n;
                if (inp.Up) d.Selected = (d.Selected + n - 2 + n) % n;
                if (inp.DownKey) d.Selected = (d.Selected + 2) % n;
                int choose = -1;
                for (int i = 0; i < n; i++)
                    if (inp.Pressed(Keys.D1 + i, Keys.NumPad1 + i)) choose = i;
                if (inp.Click >= 0 && inp.Click < n) choose = inp.Click;
                if (inp.Pressed(Keys.Enter, Keys.Space) && d.Time > 0.3) choose = d.Selected;
                if (choose >= 0) Answer(d, choose);
                return;
            }

            if (d.Time < 0.5 || !(inp.Accept || inp.Click == 10)) return;
            switch (d.Phase)
            {
                case Phase.Correct:
                    if (d.IsMaster || d.Kid != null) CloseDialog();
                    else SetLine(d, Phase.Reward, L.F("reward", L.T("g" + d.Gift)));
                    break;
                case Phase.Wrong:
                    if (d.Kid != null) CloseDialog();
                    else AskNew(d);
                    break;
                default:
                    CloseDialog();
                    break;
            }
        }

        void CloseDialog()
        {
            Voice.Stop();
            SetScreen(GameScreen.Playing);
            if (Dialog.IsMaster && World.StairsOpen) ShowMessage(L.T("stairs_open"), 5);
        }

        void Answer(Dialog d, int choice)
        {
            d.Chosen = choice;
            d.Selected = choice;
            if (d.Offer)
            {
                if (choice == 0)
                {
                    var others = World.Entities.FindAll(e => e.Kind == Kind.Kid && e != d.Kid);
                    others.Sort((a, b) => Dist(a.X, a.Y, 22, 13).CompareTo(Dist(b.X, b.Y, 22, 13)));
                    Match.Start(d.Kid, others[0], others[1]);
                    P.X = 19;
                    P.Y = 13.5;
                    P.Angle = 0;
                    CamAngle = 0;
                    Snd.Play("select");
                }
                Voice.Stop();
                SetScreen(GameScreen.Playing);
                return;
            }
            if (d.Riddle)
            {
                if (choice == d.Q.Correct)
                {
                    Save.Coins += 3;
                    Save.Write();
                    Snd.Play("correct");
                    AddPopup(L.F("coins_plus", 3), Color.FromArgb(255, 230, 90));
                    SetLine(d, Phase.Correct, Chat.RiddleRight);
                }
                else
                {
                    Snd.Play("wrong");
                    SetLine(d, Phase.Wrong, Chat.RiddleWrong(d.Q.CorrectText));
                }
                return;
            }
            if (choice != d.Q.Correct)
            {
                Mistakes++;
                Snd.Play("wrong");
                SetLine(d, Phase.Wrong, L.F(d.IsMaster ? "master_wrong" : "wrong", d.Q.CorrectText));
                return;
            }

            Snd.Play("correct");
            if (d.IsMaster)
            {
                Save.Master[FloorIndex] = true;
                Save.Unlocked = Math.Max(Save.Unlocked, Math.Min(School.Floors, FloorIndex + 2));
                Save.Write();
                World.OpenStairs();
                World.MasterMarker.Frames = Art.MarkerDone;
                Snd.Play("stairs");
                SetLine(d, Phase.Correct, L.T(FloorIndex == School.Floors - 1 ? "master_last" : "master_correct"));
                return;
            }

            // the gift is won right away, so leaving the dialog early cannot lose it
            var room = d.Room;
            room.Done = true;
            room.Gift.Alive = false;
            room.Marker.Frames = Art.MarkerDone;
            d.Gift = room.Index;
            Save.Classes[FloorIndex] |= 1 << room.Index;
            Save.Write();
            Sparkles(room.Gift.X, room.Gift.Y, 0.6, 12);
            Snd.Play("gift");
            SetLine(d, Phase.Correct, L.T("correct"));
            if (World.GiftsDone == School.Classes && !World.MasterDoorOpen)
            {
                World.OpenMasterDoor();
                ShowMessage(L.T("master_open"), 6);
            }
        }

        void DebugTeleport(Input inp)
        {
            if (inp.Pressed(Keys.F5))
                foreach (var r in World.Rooms)
                    if (!r.Done)
                    {
                        double dir = r.Teacher.Y < 10 ? 1 : -1;
                        P.X = r.Teacher.X;
                        P.Y = r.Teacher.Y + dir * 1.9;
                        P.Angle = dir > 0 ? -Math.PI / 2 : Math.PI / 2;
                        CamAngle = P.Angle;
                        break;
                    }
            if (inp.Pressed(Keys.F7) && World.Paintings.Count > 0)
            {
                var p = World.Paintings[debugPainting++ % World.Paintings.Count];
                P.X = p.X;
                P.Y = p.Y;
                int cx = (int)Math.Floor(p.X), cy = (int)Math.Floor(p.Y);
                // face the wall the painting hangs on
                if (World.IsWall(cx, cy - 1)) P.Angle = -Math.PI / 2;
                else if (World.IsWall(cx, cy + 1)) P.Angle = Math.PI / 2;
                else if (World.IsWall(cx - 1, cy)) P.Angle = Math.PI;
                else P.Angle = 0;
                CamAngle = P.Angle;
            }
            if (inp.Pressed(Keys.F6))
            {
                World.OpenMasterDoor();
                P.X = 46.5;
                P.Y = 10.5;
                P.Angle = 0;
                CamAngle = 0;
            }
        }

        // ------------------------------------------------------------------ helpers for the HUD

        /// <summary>Where the compass points: the nearest classroom still to visit, then the Master Turtle, then the stairs.</summary>
        public bool CompassTarget(out double tx, out double ty, out bool master)
        {
            tx = ty = 0;
            master = false;
            if (InYard)
            {
                var target = Match != null && Match.Active ? Match.Ball : Yard.Captain(World);
                if (target == null) return false;
                tx = target.X;
                ty = target.Y;
                return true;
            }
            double best = double.MaxValue;
            foreach (var r in World.Rooms)
            {
                if (r.Done) continue;
                double d = Dist(P.X, P.Y, r.Teacher.X, r.Teacher.Y);
                if (d < best) { best = d; tx = r.Teacher.X; ty = r.Teacher.Y; }
            }
            if (best < double.MaxValue) return true;
            master = true;
            if (!Save.Master[FloorIndex]) { tx = World.Master.X; ty = World.Master.Y; }
            else { tx = World.StairsX + 0.5; ty = World.StairsY + 0.5; }
            return true;
        }

        public void Render(Renderer r) => r.Render(World, CamX, CamY, CamAngle, CamZ, Horizon, Time);
    }
}
